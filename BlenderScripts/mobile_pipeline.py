"""
mobile_pipeline.py  --  Ice Legends Arena :: hero-character -> mobile-asset pipeline
====================================================================================

Turns a finished, rigged hero character (separate meshes + one texture per gear
piece) into a mobile-ready asset: ONE atlas texture, ONE merged skinned mesh, and
three LODs, exported to an FBX that Unity auto-wires into a LODGroup.

First built ad-hoc on Blaze (character #1) through Blender-MCP; this script is that
recipe made reusable + config-driven so characters 2-8 go through it the same way.
Every number below (weld count, LOD ratios, protect-group semantics) was validated
against Blaze's shipped `Blaze_mobile.blend` / `Blaze_Mobile.fbx`.

    Blaze results (the reference the config reproduces):
      as-merged   50,180 v / 39,408 tris
      welded      29,394 v / 39,404 tris
      LOD0        16,063 v / 19,702 tris   (head protected, ratio 0.50)
      LOD1        11,025 v / 11,820 tris   (uniform,        ratio 0.30)
      LOD2         6,349 v /  5,122 tris   (uniform,        ratio 0.13)
      textures    6x4096 + 1x1024  ->  1x2048 atlas   (7 draw calls -> 1)

--------------------------------------------------------------------------------
HOW TO RUN
--------------------------------------------------------------------------------
  * Blender's Scripting workspace: paste + Run (Alt+P), then in the console:
        run("Blaze")                      # process one character
        run_all()                         # every character in CONFIG
  * or headless:  blender --background --python mobile_pipeline.py -- Blaze
  * or via Blender-MCP execute_blender_code: exec(open(PATH).read()); run("Blaze")

MASTER -> DERIVED (non-negotiable): this pipeline is DESTRUCTIVE (merging + UV
rewrite destroy the separate meshes/materials). It ALWAYS opens the read-only
master `*_rigged.blend`, immediately saves it out as the derived `*_mobile.blend`,
and edits only the derived copy. It asserts it is not sitting on the master before
any mutation. The master stays editable (needed to re-run this + to build cosmetic
skins). Set `dry_run=True` / a scratch out-dir to validate without clobbering
shipped assets.

--------------------------------------------------------------------------------
GOTCHAS baked in (learned the hard way -- see mobile-character-pipeline memory)
--------------------------------------------------------------------------------
  * WELD before you decimate: ~41% of a Tripo mesh's verts are exact duplicates.
    Blender stores UVs per face-corner (loop), so welding does NOT damage UVs.
    Decimate can't collapse across split verts -> a fragmented mesh cracks.
  * The Decimate vertex group is a BINARY LOCK, not a dial: weight == 1.0 = fully
    protected, anything less = no protection. So you grade the SIZE of the
    protected region, never its strength. Protect group here is head+neck -> 1.0,
    everything else 0.0, and the modifier uses invert_vertex_group=False.
  * Protect ONLY LOD0. A protected head is an incompressible floor (~23% of tris);
    protecting it at LOD1/2 starves the body, which is backwards for a mesh viewed
    from 20 m. LOD1/2 decimate uniformly.
  * Atlas by DIRECT texture copy (no Cycles bake) -- valid ONLY while every UV sits
    in 0-1. The script asserts this; if a future char tiles its UVs the shortcut
    breaks and you'd need a real bake (not implemented).
  * Always downscale a COPY of each source image before foreach_get -- a 4096^2
    float RGBA buffer is ~268 MB.
  * 8 px edge-padded gutter per tile, UVs remapped into the INNER region only, or
    bilinear filtering + mipmaps bleed neighbouring tiles together.
  * FBX export: use_selection=True and EXPLICITLY select the armature (an earlier
    export once shipped a boneless FBX). Name the LOD objects `*_LOD0/1/2` so Unity
    auto-creates the LODGroup on import.
"""

import bpy
import bmesh
import os
import sys
import numpy as np

# Project root: <root>/Characters/<char>_rigged.blend and <root>/Ice Legends Arena/Assets/...
PROJECT_ROOT = os.environ.get(
    "ILA_PROJECT_ROOT", r"C:\Users\myapo\Documents\ice-legends-arena"
)

# Canonical FBX export settings -- do not change per character (see memory).
FBX_SETTINGS = dict(
    use_selection=True,          # + explicitly select the armature (below)
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=False,
    mesh_smooth_type="FACE",
    path_mode="STRIP",
    embed_textures=False,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
)

# ============================================================================
# PER-CHARACTER CONFIG
# ============================================================================
# Add an entry per character as they come off the Tripo->Blender->Unity pipeline.
# Everything else (material list, source images, mesh->material grouping) is
# DISCOVERED at runtime, so a new character usually only needs the paths, the
# hero mesh name, and (if the skeleton differs) the head bones.
#
#   master_blend / derived_blend : project-relative .blend paths
#   unity_dir                    : project-relative folder for the FBX + atlas PNG
#   hero_mesh                    : mesh whose material gets the big atlas tile
#                                  (it carries the face + skin, biggest on screen)
#   armature                     : the rig object name
#   head_bones                   : vertex groups whose verts define the LOD0
#                                  protected region (face + hair + mask ride along)
#   atlas_size / hero_tile / other_tile / pad : atlas layout, in pixels
#   lods                         : list of {suffix, ratio, protect_head}
CONFIG = {
    "Blaze": {
        "master_blend": r"Characters\Blaze_rigged.blend",
        "derived_blend": r"Characters\Blaze_mobile.blend",
        "unity_dir": r"Ice Legends Arena\Assets\Characters\Blaze",
        "fbx_name": "Blaze_Mobile.fbx",
        "atlas_name": "Blaze_Atlas.png",
        "merged_name": "Blaze_Mobile",
        "armature": "Blaze_Rig",
        "hero_mesh": "Blaze_Body",
        "head_bones": ["mixamorig:Head", "mixamorig:Neck"],
        "atlas_size": 2048,
        "hero_tile": 1024,
        "other_tile": 512,
        "pad": 8,
        # LOD0 protects the head, which makes `ratio` non-linear (the locked head
        # resists collapse), so target a TRI BUDGET and let the script bisect the
        # ratio. Uniform LODs use `ratio` directly (linear, predictable).
        "lods": [
            {"suffix": "LOD0", "target_tris": 19702, "protect_head": True},
            {"suffix": "LOD1", "ratio": 0.30, "protect_head": False},
            {"suffix": "LOD2", "ratio": 0.13, "protect_head": False},
        ],
    },
    # "Ragnar": { ... same shape; fill in when char #2 is rigged ... },
}


# ============================================================================
# small helpers
# ============================================================================
def _abs(rel):
    return os.path.normpath(os.path.join(PROJECT_ROOT, rel))


def _log(msg):
    print("[mobile_pipeline] " + msg)


def _deselect_all():
    for o in bpy.context.view_layer.objects:
        o.select_set(False)


def _ensure_object_mode():
    if bpy.context.mode != "OBJECT":
        try:
            bpy.ops.object.mode_set(mode="OBJECT")
        except RuntimeError:
            pass


def _duplicate(obj, new_name):
    """Data-level duplicate (no operator, so it works headless / via MCP with no
    3D viewport). Copies the object + its mesh; vertex groups and the modifier
    stack (incl. the Armature modifier) ride along on obj.copy()."""
    nd = obj.data.copy()
    no = obj.copy()
    no.data = nd
    no.name = new_name
    nd.name = new_name
    for c in obj.users_collection:
        c.objects.link(no)
    return no


def _tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def _target_meshes(cfg):
    """Skinned meshes to process: every MESH with an Armature modifier on this rig."""
    rig = cfg["armature"]
    out = []
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        if any(m.type == "ARMATURE" and m.object and m.object.name == rig for m in o.modifiers):
            out.append(o)
    return out


def _base_color_image(mat):
    """The base-color image feeding a material, or None."""
    if not mat or not mat.use_nodes:
        return None
    # Prefer the image wired into a Principled Base Color; else any image node.
    principled = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if principled:
        link = principled.inputs["Base Color"].links
        if link and link[0].from_node.type == "TEX_IMAGE" and link[0].from_node.image:
            return link[0].from_node.image
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image:
            return n.image
    return None


# ============================================================================
# 0. master -> derived guard
# ============================================================================
def open_master_save_derived(cfg, out_dir=None, derived_override=None):
    master = _abs(cfg["master_blend"])
    derived = derived_override or _abs(cfg["derived_blend"])
    if not os.path.exists(master):
        raise FileNotFoundError("master not found: " + master)

    bpy.ops.wm.open_mainfile(filepath=master)
    # Immediately move off the master so nothing below can mutate it.
    bpy.ops.wm.save_as_mainfile(filepath=derived)
    assert os.path.normpath(bpy.data.filepath) == os.path.normpath(derived), \
        "guard failed: not on the derived file after save_as"
    assert os.path.normpath(bpy.data.filepath) != os.path.normpath(master), \
        "guard failed: still sitting on the MASTER"
    _log("master -> derived: %s" % derived)
    return derived


# ============================================================================
# 1. atlas: precondition + shelf pack + pixel blit
# ============================================================================
def assert_uvs_in_unit(meshes):
    bad = []
    for o in meshes:
        uv = o.data.uv_layers.active
        if not uv:
            bad.append((o.name, "no UV layer"))
            continue
        # sample the loops; tolerate a hair over the edge from float error
        for d in uv.data:
            u, v = d.uv
            if u < -0.001 or u > 1.001 or v < -0.001 or v > 1.001:
                bad.append((o.name, "UV outside 0-1 (%.3f, %.3f)" % (u, v)))
                break
    if bad:
        raise RuntimeError(
            "UV precondition FAILED -- direct-copy atlas needs every UV in 0-1:\n  "
            + "\n  ".join("%s: %s" % b for b in bad)
            + "\nThis char needs a real Cycles bake (not implemented)."
        )


def _shelf_pack(sizes, atlas_size):
    """Next-fit shelf packer. `sizes` = [(key, tile_px), ...] (biggest first).
    Returns {key: (x, y, tile_px)} with (x, y) = bottom-left in pixels."""
    rects = {}
    x = y = shelf_h = 0
    for key, s in sizes:
        if x + s > atlas_size:            # wrap to next shelf
            x = 0
            y += shelf_h
            shelf_h = 0
        if y + s > atlas_size:
            raise RuntimeError("atlas too small to pack all tiles (%d px)" % atlas_size)
        rects[key] = (x, y, s)
        x += s
        shelf_h = max(shelf_h, s)
    return rects


def build_atlas(cfg, meshes):
    """Pack one tile per material into a single atlas image.
    Returns (atlas_image, {material_name: (x, y, tile_px)}) with the pixel copy
    already blitted. UVs are remapped separately in remap_uvs()."""
    S = cfg["atlas_size"]
    pad = cfg["pad"]
    hero_mat = meshes_hero_material(cfg, meshes)

    # unique materials, hero first, then stable order
    mats = []
    for o in meshes:
        for m in o.data.materials:
            if m and m.name not in [x.name for x in mats]:
                mats.append(m)
    mats.sort(key=lambda m: (m.name != hero_mat.name, m.name))

    sizes = [(m.name, cfg["hero_tile"] if m.name == hero_mat.name else cfg["other_tile"])
             for m in mats]
    rects = _shelf_pack(sizes, S)

    atlas = np.zeros((S, S, 4), dtype=np.float32)
    atlas[..., 3] = 1.0
    for m in mats:
        img = _base_color_image(m)
        if img is None:
            _log("WARNING: material '%s' has no base-color image; tile left black" % m.name)
            continue
        x, y, tile = rects[m.name]
        inner = tile - 2 * pad
        tmp = img.copy()
        tmp.scale(inner, inner)                       # downscale the COPY first
        buf = np.empty(inner * inner * 4, dtype=np.float32)
        tmp.pixels.foreach_get(buf)
        bpy.data.images.remove(tmp)
        cell = buf.reshape(inner, inner, 4)           # [y-from-bottom, x, rgba]
        cell = np.pad(cell, ((pad, pad), (pad, pad), (0, 0)), mode="edge")
        atlas[y:y + tile, x:x + tile, :] = cell
        _log("packed '%s' -> tile %dpx at (%d,%d)" % (m.name, tile, x, y))

    atlas_img = bpy.data.images.new(cfg["merged_name"] + "_Atlas", S, S, alpha=True)
    atlas_img.colorspace_settings.name = "sRGB"       # match the source base colors
    atlas_img.pixels.foreach_set(atlas.ravel())
    return atlas_img, rects, mats


def meshes_hero_material(cfg, meshes):
    hero = next((o for o in meshes if o.name == cfg["hero_mesh"]), None)
    if hero is None:
        raise RuntimeError("hero mesh '%s' not among target meshes" % cfg["hero_mesh"])
    mat = hero.data.materials[0] if hero.data.materials else None
    if mat is None:
        raise RuntimeError("hero mesh '%s' has no material" % cfg["hero_mesh"])
    return mat


def remap_uvs(cfg, meshes, rects):
    """Remap each mesh's UVs into its material's inner tile region."""
    S = float(cfg["atlas_size"])
    pad = cfg["pad"]
    for o in meshes:
        me = o.data
        uv = me.uv_layers.active
        # per-loop: look up the material of the loop's polygon
        mat_index_to_name = [m.name if m else None for m in me.materials]
        for poly in me.polygons:
            mname = mat_index_to_name[poly.material_index] if mat_index_to_name else None
            if mname not in rects:
                continue
            x, y, tile = rects[mname]
            inner = tile - 2 * pad
            for li in poly.loop_indices:
                u, v = uv.data[li].uv
                u = min(max(u, 0.0), 1.0)
                v = min(max(v, 0.0), 1.0)
                uv.data[li].uv = (
                    (x + pad + u * inner) / S,
                    (y + pad + v * inner) / S,
                )


def make_atlas_material(cfg, atlas_img):
    mat = bpy.data.materials.new(cfg["merged_name"])
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = atlas_img
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    out.location = (400, 0)
    bsdf.location = (100, 0)
    tex.location = (-300, 0)
    return mat


# ============================================================================
# 2. merge + weld
# ============================================================================
def merge_meshes(cfg, meshes, atlas_mat):
    # single material on every mesh
    for o in meshes:
        o.data.materials.clear()
        o.data.materials.append(atlas_mat)

    hero = next(o for o in meshes if o.name == cfg["hero_mesh"])
    _ensure_object_mode()
    _deselect_all()
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = hero      # keep hero's Armature modifier
    # join() polls for a proper object-mode context; supply it explicitly so this
    # works headless / through Blender-MCP (no 3D viewport).
    with bpy.context.temp_override(active_object=hero, object=hero,
                                   selected_objects=meshes,
                                   selected_editable_objects=meshes):
        bpy.ops.object.join()
    merged = hero
    merged.name = cfg["merged_name"]
    merged.data.name = cfg["merged_name"]
    _log("merged -> %s (%d verts / %d tris)"
         % (merged.name, len(merged.data.vertices), _tri_count(merged.data)))
    return merged


def weld(obj, dist=0.0001):
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(me)
    bm.free()
    me.update()
    _log("welded -> %d verts / %d tris" % (len(me.vertices), _tri_count(me)))


# ============================================================================
# 3. LODs
# ============================================================================
def build_protect_group(cfg, obj, name="Protect", threshold=0.5):
    """Binary head-protect group: verts with (head+neck) weight > threshold -> 1.0."""
    head_gis = [obj.vertex_groups[b].index for b in cfg["head_bones"]
                if b in obj.vertex_groups]
    if not head_gis:
        raise RuntimeError("none of head_bones %s found on %s" % (cfg["head_bones"], obj.name))
    if name in obj.vertex_groups:
        obj.vertex_groups.remove(obj.vertex_groups[name])
    vg = obj.vertex_groups.new(name=name)
    for v in obj.data.vertices:
        hw = sum(g.weight for g in v.groups if g.group in head_gis)
        if hw > threshold:
            vg.add([v.index], 1.0, "REPLACE")
    return vg.name


def _tris_at_ratio(obj, ratio, vgroup=None):
    """Non-destructively evaluate the tri count a COLLAPSE decimate would yield."""
    dec = obj.modifiers.new(name="._probe", type="DECIMATE")
    dec.decimate_type = "COLLAPSE"
    dec.ratio = ratio
    if vgroup:
        dec.vertex_group = vgroup
        dec.invert_vertex_group = True
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    n = _tri_count(ev.data)
    obj.modifiers.remove(dec)
    return n


def _solve_ratio_for_tris(obj, target_tris, vgroup=None, iters=18):
    """Bisect the decimate ratio to hit ~target_tris (needed because a protect
    group makes the ratio->tris curve non-linear)."""
    lo, hi = 0.01, 1.0
    best = hi
    for _ in range(iters):
        mid = 0.5 * (lo + hi)
        n = _tris_at_ratio(obj, mid, vgroup)
        best = mid
        if n > target_tris:
            hi = mid          # too many tris -> decimate harder
        else:
            lo = mid
        if abs(n - target_tris) <= max(50, target_tris * 0.005):
            break
    return best


def make_lods(cfg, welded):
    """Duplicate the welded mesh once per LOD, decimate, rename *_LOD0/1/2, apply."""
    protect_name = build_protect_group(cfg, welded)     # ride-along on all copies
    lod_objs = []
    for spec in cfg["lods"]:
        lod = _duplicate(welded, cfg["merged_name"] + "_" + spec["suffix"])

        vg = protect_name if spec["protect_head"] else None
        if "target_tris" in spec:
            ratio = _solve_ratio_for_tris(lod, spec["target_tris"], vgroup=vg)
        else:
            ratio = spec["ratio"]

        dec = lod.modifiers.new(name="Decimate", type="DECIMATE")
        dec.decimate_type = "COLLAPSE"
        dec.ratio = ratio
        if spec["protect_head"]:
            dec.vertex_group = protect_name
            # invert=True is REQUIRED: with invert=False the collapse floors and
            # ignores `ratio` entirely (verified 2026-07-17). invert=True keeps the
            # head (weight 1.0) dense while `ratio` drives the rest.
            dec.invert_vertex_group = True
        # apply the decimate; keep the Armature modifier
        _deselect_all()
        lod.select_set(True)
        bpy.context.view_layer.objects.active = lod
        with bpy.context.temp_override(active_object=lod, object=lod,
                                       selected_objects=[lod],
                                       selected_editable_objects=[lod]):
            bpy.ops.object.modifier_apply(modifier=dec.name)
        _log("%s: ratio %.3f%s -> %d verts / %d tris"
             % (lod.name, ratio, " (head-protected)" if spec["protect_head"] else "",
                len(lod.data.vertices), _tri_count(lod.data)))
        lod_objs.append(lod)
    return lod_objs


# ============================================================================
# 4. export
# ============================================================================
def export_fbx(cfg, lod_objs, out_dir=None):
    out_dir = out_dir or _abs(cfg["unity_dir"])
    os.makedirs(out_dir, exist_ok=True)
    fbx_path = os.path.join(out_dir, cfg["fbx_name"])
    rig = bpy.data.objects[cfg["armature"]]
    sel = lod_objs + [rig]                              # ALWAYS include the armature
    assert any(o.type == "ARMATURE" for o in sel), \
        "armature not in export selection -- would ship a boneless FBX"

    _deselect_all()
    for o in sel:
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    # use_selection reads context selection; supply it explicitly for headless/MCP.
    with bpy.context.temp_override(active_object=rig, object=rig,
                                   selected_objects=sel, selected_editable_objects=sel):
        bpy.ops.export_scene.fbx(filepath=fbx_path, **FBX_SETTINGS)
    _log("FBX -> %s" % fbx_path)
    return fbx_path


def save_atlas_png(cfg, atlas_img, out_dir=None):
    out_dir = out_dir or _abs(cfg["unity_dir"])
    os.makedirs(out_dir, exist_ok=True)
    png_path = os.path.join(out_dir, cfg["atlas_name"])
    atlas_img.filepath_raw = png_path
    atlas_img.file_format = "PNG"
    atlas_img.save()
    _log("atlas PNG -> %s" % png_path)
    return png_path


# ============================================================================
# orchestration
# ============================================================================
def run(char_key, dry_run=False):
    """Run the full pipeline for one character.
    dry_run=True writes the derived .blend / FBX / atlas to a scratch '_TEST'
    location so a shipped asset is never clobbered while validating."""
    if char_key not in CONFIG:
        raise KeyError("no CONFIG for '%s' (have: %s)" % (char_key, list(CONFIG)))
    cfg = CONFIG[char_key]
    _log("=== %s%s ===" % (char_key, "  [DRY RUN]" if dry_run else ""))

    out_dir = None
    derived_override = None
    if dry_run:
        out_dir = os.path.join(_abs(cfg["unity_dir"]), "_TEST")
        derived_override = _abs(cfg["derived_blend"]).replace(".blend", "_TEST.blend")

    open_master_save_derived(cfg, out_dir=out_dir, derived_override=derived_override)

    meshes = _target_meshes(cfg)
    _log("target meshes (%d): %s" % (len(meshes), [o.name for o in meshes]))
    assert_uvs_in_unit(meshes)

    atlas_img, rects, _mats = build_atlas(cfg, meshes)
    remap_uvs(cfg, meshes, rects)
    atlas_mat = make_atlas_material(cfg, atlas_img)

    merged = merge_meshes(cfg, meshes, atlas_mat)

    # keep a pre-weld backup object (matches the shipped derived file)
    backup = _duplicate(merged, cfg["merged_name"] + "_PREWELD")
    backup.hide_set(True)

    weld(merged)
    lod_objs = make_lods(cfg, merged)

    png = save_atlas_png(cfg, atlas_img, out_dir=out_dir)
    fbx = export_fbx(cfg, lod_objs, out_dir=out_dir)
    bpy.ops.wm.save_mainfile()

    _log("DONE %s: %s + %s" % (char_key, os.path.basename(fbx), os.path.basename(png)))
    return {"fbx": fbx, "atlas": png,
            "lods": {o.name: _tri_count(o.data) for o in lod_objs}}


def run_all(dry_run=False):
    return {k: run(k, dry_run=dry_run) for k in CONFIG}


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if argv:
        run(argv[0], dry_run=("--dry-run" in argv))
    else:
        _log("usage: blender -b -P mobile_pipeline.py -- <CharacterKey> [--dry-run]")
        _log("       or in Blender's console: run('Blaze')")

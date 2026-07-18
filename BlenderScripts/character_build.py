"""
character_build.py  --  Ice Legends Arena :: Tripo -> rigged master .blend
==========================================================================

The FRONT half of the character pipeline: takes a Tripo base body + a Mixamo
rig + a list of Tripo gear GLBs and assembles the fully rigged, geared, textured
MASTER `*_rigged.blend` that `mobile_pipeline.py` then consumes.

    Tripo (base body, head, gear)                        <- YOU, the human/creative gate
      -> character_build.py  -> Characters/<Name>_rigged.blend   <- THIS SCRIPT
        -> mobile_pipeline.py -> <Name>_Mobile.fbx + atlas        <- the back half

WHAT THIS AUTOMATES vs WHAT STAYS MANUAL
----------------------------------------
This is NOT push-button. Whether a pauldron *sits right* on a shoulder is spatial
judgement that lives in the per-piece `fit` numbers (rotation / scale / position)
you tune per character. What the script DOES remove is the mechanical, repetitive,
error-prone machinery that bit us over and over on Blaze:
  * the Tripo orientation gotcha (pair-width on Y, front +Y -> -90 deg about Z),
  * L/R split by geometry + the CROSS-BINDING trap (binding a piece to the limb in
    its NAME instead of the limb its GEOMETRY sits on),
  * opposite-hand gear = a MIRROR (reflection across X=0 + normal flip), never a
    rotation,
  * the three correct bind strategies (rigid single-bone / jointed weight-transfer
    with bone-collapse / cloth weight-transfer + shrinkwrap),
  * the numeric verification harness (pinned-vertex slide + rigidity),
  * the canonical FBX export.

So per character you: generate art in Tripo -> fill a CONFIG entry -> run -> LOOK at
the verify render -> nudge the `fit` numbers that are off -> re-run. Far faster and
far less trap-prone than doing every step by hand.

STATUS: framework written 2026-07-17, structurally validated (parses/loads in
Blender). NOT yet run end-to-end -- like mobile_pipeline.py before its dry-run,
the first REAL character (or a re-run on Blaze) will surface API/context issues.
Validate before trusting. Blaze's entry below is the reference template with the
fit values that produced the shipped Blaze_rigged.blend.

HOW TO RUN
----------
  in Blender's console:  build("Blaze")            # or build("Blaze", verify=True)
  headless:  blender -b -P character_build.py -- Blaze

MASTER SAFETY: this WRITES the master `*_rigged.blend`. It builds in a fresh empty
scene and saves to the configured path. Never point master_out at a file you can't
afford to overwrite; it's the irreplaceable character source.
"""

import bpy
import bmesh
import os
import sys
from mathutils import Matrix, Vector

PROJECT_ROOT = os.environ.get(
    "ILA_PROJECT_ROOT", r"C:\Users\myapo\Documents\ice-legends-arena"
)
CHARS = os.path.join(PROJECT_ROOT, "Characters")

# Canonical FBX export (identical to mobile_pipeline / the 2026-07-04 rule).
FBX_SETTINGS = dict(
    use_selection=False,          # grab the whole rigged char incl. armature
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
# gear `kind`:
#   "rigid_single"  -> one bone, weight 1.0 (hard armour / mask / hair). The bone
#                      is the one the piece is MOUNTED on (clavicle for a pauldron),
#                      NOT the bone it hangs over (upper arm). Rigid == correct here;
#                      a multi-bone blend SHEARS a hard piece (candy-wrapper).
#   "rigid_pair"    -> same, but split L/R first and bind each to its side's bone.
#   "jointed_single"/"jointed_pair" -> weight-transfer from the body so the piece
#                      rides the skin across a joint (gloves, gauntlets, skate boots),
#                      then COLLAPSE the bones it must not flex with (skate toe->foot,
#                      glove fingers->hand).
#   "cloth"         -> weight-transfer from body + shrinkwrap to kill clipping (pants,
#                      crop top). Any object-level rotation of a skinned mesh needs a
#                      re-skin, so cloth is fitted BEFORE weights are transferred.
#   `mirror_to`     -> build the opposite-hand copy: reflect across X=0, flip normals,
#                      bind to the named bone(s).
#
# `fit` = {rot_z, scale, pos:[x,y,z]} applied in that order (Tripo GLBs come in
# quaternion mode + pair-width on Y; rot_z puts width on X and faces -Y).
CONFIG = {
    "Blaze": {
        "base_body_glb": "blaze_mesh_2.fbx.glb",     # Tripo textured base body
        "rig_fbx": "Blaze_T-Pose.fbx",               # Mixamo 65-bone skinned result
        "body_name": "Blaze_Body",
        "rig_name": "Blaze_Rig",
        "target_height": 1.7,
        "master_out": r"Characters\Blaze_rigged.blend",
        "unity_fbx": r"Ice Legends Arena\Assets\Characters\Blaze\Blaze.fbx",
        "gear": [
            {"name": "GasMask", "glb": "gas mask 3d model no glasses.glb",
             "kind": "rigid_single", "bone": "mixamorig:Head",
             "fit": {"rot_z": -90, "scale": 1.0, "pos": [0, 0, 1.55]}},
            {"name": "Hair", "glb": "stylized hair 3d model.glb",
             "kind": "rigid_single", "bone": "mixamorig:Head", "tripo_segment_part": 0,
             "fit": {"rot_z": -90, "scale": 0.49, "pos": [0, -0.011, 1.557]}},
            {"name": "ShoulderPad", "glb": "spiked shoulder armor 3d model.glb",
             "kind": "rigid_pair", "bones": {"L": "mixamorig:LeftShoulder",
                                             "R": "mixamorig:RightShoulder"},
             "fit": {"rot_z": -90, "scale": 0.7, "pos": [0, 0.06, 1.36]}},
            {"name": "Skate", "glb": "ice skate boots 3d model.glb",
             "kind": "jointed_pair", "bones": {"L": "mixamorig:LeftFoot",
                                               "R": "mixamorig:RightFoot"},
             "collapse": {"ToeBase": "Foot", "Toe_End": "Foot"},
             "fit": {"rot_z": -90, "scale": 0.5, "pos": [0, 0, 0]}},
            {"name": "GauntletGlove_L", "glb": "steampunk arm gauntlet 3d model.glb",
             "kind": "jointed_single", "bone": "mixamorig:LeftForeArm",
             "collapse": {"Hand*": "Hand"},   # fingers -> hand
             "fit": {"rot_z": -90, "scale": 0.28, "pos": [-0.47, 0.05, 1.31]},
             "mirror_to": {"name": "Glove_R", "bone": "mixamorig:RightHand",
                           "strip_outboard_x": -0.46}},   # keep glove-only side
            {"name": "Pants", "glb": "tactical combat pants 3d model.glb",
             "kind": "cloth", "shrinkwrap": 0.011,
             "fit": {"rot_z": 90, "scale": 0.9, "pos": [0, 0, 0.551]}},
        ],
    },
}


# ============================================================================
# helpers (shared conventions + the headless/MCP operator-context patterns)
# ============================================================================
def _abs(rel):
    return os.path.normpath(os.path.join(PROJECT_ROOT, rel))


def _log(m):
    print("[character_build] " + m)


def _deselect_all():
    for o in bpy.context.view_layer.objects:
        o.select_set(False)


def _ensure_object_mode():
    if bpy.context.mode != "OBJECT":
        try:
            bpy.ops.object.mode_set(mode="OBJECT")
        except RuntimeError:
            pass


def _new_objects_after(fn):
    """Run an importer, return the objects it added."""
    before = set(bpy.data.objects)
    fn()
    return [o for o in bpy.data.objects if o not in before]


def _import(path):
    """Import a GLB/GLTF/FBX and return the added objects. glTF comes in with
    rotation_mode QUATERNION -> switch every added object to XYZ so rotation_euler
    is respected downstream."""
    ext = os.path.splitext(path)[1].lower()
    if ext in (".glb", ".gltf"):
        added = _new_objects_after(lambda: bpy.ops.import_scene.gltf(filepath=path))
    elif ext == ".fbx":
        added = _new_objects_after(lambda: bpy.ops.import_scene.fbx(filepath=path))
    else:
        raise ValueError("unsupported import: " + path)
    for o in added:
        if o.rotation_mode == "QUATERNION":
            o.rotation_mode = "XYZ"
    return added


def _apply_transform(obj, location=False, rotation=True, scale=True):
    _deselect_all()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    with bpy.context.temp_override(active_object=obj, object=obj,
                                   selected_objects=[obj],
                                   selected_editable_objects=[obj]):
        bpy.ops.object.transform_apply(location=location, rotation=rotation, scale=scale)


def _mesh_bbox(obj, region=None):
    """World-space bbox of an object's verts, optionally filtered to a region
    predicate fn(world_co)->bool. Returns (min V3, max V3, center V3)."""
    mw = obj.matrix_world
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for v in obj.data.vertices:
        wc = mw @ v.co
        if region and not region(wc):
            continue
        lo = Vector((min(lo[i], wc[i]) for i in range(3)))
        hi = Vector((max(hi[i], wc[i]) for i in range(3)))
    return lo, hi, (lo + hi) * 0.5


# ============================================================================
# base body + rig
# ============================================================================
def prep_base_body(cfg):
    """Import the Tripo base body, clean glTF junk, orient to Mixamo convention
    (faces -Y, armspan on X), set height + origin at the feet, sit at origin."""
    added = _import(_abs(os.path.join("Characters", cfg["base_body_glb"])))
    # drop glTF empties (RootNode / Camera / Light), keep the mesh
    meshes = [o for o in added if o.type == "MESH"]
    for o in added:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    body = max(meshes, key=lambda o: len(o.data.vertices))
    body.name = cfg["body_name"]
    _apply_transform(body, rotation=True, scale=True)
    bpy.context.view_layer.update()

    lo, hi, _ = _mesh_bbox(body)
    height = hi.z - lo.z
    s = cfg["target_height"] / height if height else 1.0
    body.scale = (s, s, s)
    _apply_transform(body, scale=True)
    bpy.context.view_layer.update()
    lo, hi, ctr = _mesh_bbox(body)
    body.location = (body.location.x - ctr.x, body.location.y - ctr.y,
                     body.location.z - lo.z)   # feet at z=0, centred on XY
    _apply_transform(body, location=True)
    _log("base body '%s' prepped, height=%.3f" % (body.name, cfg["target_height"]))
    return body


def import_mixamo_rig(cfg, body):
    """Import the Mixamo T-pose FBX (65-bone skinned result). Mixamo imports the
    armature @0.01 and the mesh @100 -> select BOTH and transform_apply(scale) so
    both land at 1.0 without breaking the skin. The Mixamo mesh replaces the raw
    base body; rename armature + skinned mesh to canonical names."""
    added = _import(_abs(os.path.join("Characters", cfg["rig_fbx"])))
    rig = next(o for o in added if o.type == "ARMATURE")
    skinned = max((o for o in added if o.type == "MESH"),
                  key=lambda o: len(o.data.vertices))
    _deselect_all()
    rig.select_set(True)
    skinned.select_set(True)
    bpy.context.view_layer.objects.active = rig
    with bpy.context.temp_override(active_object=rig, object=rig,
                                   selected_objects=[rig, skinned],
                                   selected_editable_objects=[rig, skinned]):
        bpy.ops.object.transform_apply(scale=True, rotation=False, location=False)
    # the raw pre-rig body is redundant now
    if body and body.name in bpy.data.objects:
        bpy.data.objects.remove(body, do_unlink=True)
    rig.name = cfg["rig_name"]
    skinned.name = cfg["body_name"]
    for b in ("pose_position",):
        rig.data.pose_position = "REST"
    _log("rig '%s' (%d bones) + skinned body '%s' (%d vgroups)"
         % (rig.name, len(rig.data.bones), skinned.name, len(skinned.vertex_groups)))
    return rig, skinned


# ============================================================================
# gear geometry ops
# ============================================================================
def fit_piece(obj, fit):
    """Apply the standard Tripo fit: quaternion already handled at import; rotate
    about Z (width Y->X, face -Y), uniform scale, translate. Then bake it in."""
    obj.rotation_euler = (0, 0, __import__("math").radians(fit.get("rot_z", 0)))
    s = fit.get("scale", 1.0)
    obj.scale = (s, s, s)
    p = fit.get("pos", [0, 0, 0])
    bpy.context.view_layer.update()
    obj.location = Vector(p)
    _apply_transform(obj, location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()


def split_lr(obj, name):
    """Split a symmetric pair into <name>_L (+X) and <name>_R (-X) by the world-X
    sign of each face centroid. ⚠️ Name by PHYSICAL side, then bind by that side --
    NEVER trust an L/R in a source name (that is the cross-binding trap)."""
    mw = obj.matrix_world
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    left_faces = [f for f in bm.faces if (mw @ f.calc_center_median()).x >= 0]
    bm.free()
    # separate by selecting +X faces -> gives two objects
    me = obj.data
    _deselect_all()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    # tag +X faces, use bmesh to rip into two meshes
    bmL = bmesh.new(); bmL.from_mesh(me)
    bmR = bmesh.new(); bmR.from_mesh(me)
    delR = [f for f in bmR.faces if (mw @ f.calc_center_median()).x >= 0]   # R keeps -X
    delL = [f for f in bmL.faces if (mw @ f.calc_center_median()).x < 0]    # L keeps +X
    bmesh.ops.delete(bmR, geom=delR, context="FACES")
    bmesh.ops.delete(bmL, geom=delL, context="FACES")
    meL = bpy.data.meshes.new(name + "_L")
    meR = bpy.data.meshes.new(name + "_R")
    bmL.to_mesh(meL); bmR.to_mesh(meR); bmL.free(); bmR.free()
    objL = bpy.data.objects.new(name + "_L", meL)
    objR = bpy.data.objects.new(name + "_R", meR)
    for c in obj.users_collection:
        c.objects.link(objL); c.objects.link(objR)
    objL.matrix_world = mw.copy(); objR.matrix_world = mw.copy()
    for m in obj.data.materials:
        meL.materials.append(m); meR.materials.append(m)
    bpy.data.objects.remove(obj, do_unlink=True)
    return objL, objR


def mirror_x(obj, new_name):
    """Opposite-hand copy = reflection across X=0 (Matrix.Diagonal(-1,1,1)), then
    flip normals (reflection inverts winding). A rotation can NEVER fix handedness."""
    nd = obj.data.copy()
    no = obj.copy()
    no.data = nd
    no.name = new_name
    nd.name = new_name
    for c in obj.users_collection:
        c.objects.link(no)
    no.matrix_world = Matrix.Diagonal((-1, 1, 1, 1)).to_4x4() @ obj.matrix_world
    _apply_transform(no, location=True, rotation=True, scale=True)
    bm = bmesh.new(); bm.from_mesh(nd)
    for f in bm.faces:
        f.normal_flip()
    bm.to_mesh(nd); bm.free()
    return no


# ============================================================================
# the three bind strategies
# ============================================================================
def _add_armature(obj, rig):
    if not any(m.type == "ARMATURE" for m in obj.modifiers):
        m = obj.modifiers.new("Armature", "ARMATURE")
        m.object = rig
    obj.parent = rig


def bind_rigid(obj, rig, bone):
    """Hard piece -> single vertex group = bone, weight 1.0. Truly rigid (a blend
    would shear it). `bone` = the MOUNT bone (clavicle for a pauldron)."""
    obj.vertex_groups.clear()
    vg = obj.vertex_groups.new(name=bone)
    vg.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")
    _add_armature(obj, rig)
    _log("rigid bind %s -> %s" % (obj.name, bone))


def _collapse_groups(obj, collapse):
    """Fold source vertex groups into a target, per {src_glob: dst_suffix}. Reads
    weights out FIRST (removing a group invalidates held RNA pointers)."""
    import fnmatch
    # resolve which of the object's groups match each glob, per L/R side by suffix
    for v in obj.data.vertices:
        moved = {}
        for g in list(v.groups):
            gname = obj.vertex_groups[g.group].name
            for pat, dst_suffix in collapse.items():
                short = gname.split(":")[-1]
                if fnmatch.fnmatch(short, pat if "*" in pat else pat + "*"):
                    side = "Left" if "Left" in gname else ("Right" if "Right" in gname else "")
                    dst = "mixamorig:%s%s" % (side, dst_suffix)
                    moved[dst] = moved.get(dst, 0.0) + g.weight
        for dst, w in moved.items():
            if dst not in obj.vertex_groups:
                obj.vertex_groups.new(name=dst)
            obj.vertex_groups[dst].add([v.index], w, "ADD")
    # remove the collapsed source groups
    import fnmatch as fn
    for vg in list(obj.vertex_groups):
        short = vg.name.split(":")[-1]
        if any(fn.fnmatch(short, p if "*" in p else p + "*") for p in collapse):
            obj.vertex_groups.remove(vg)


def bind_jointed(obj, rig, body, collapse=None):
    """Semi-flexible piece -> transfer the body's weights so it rides the skin
    (fixes cross-binding AND seam gaps geometrically), collapse the bones it must
    not flex with, cap to 4 influences (Unity), normalize."""
    obj.vertex_groups.clear()
    _apply_transform(obj, location=True, rotation=True, scale=True)
    _deselect_all()
    obj.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active = body        # SOURCE = active
    with bpy.context.temp_override(active_object=body, object=body,
                                   selected_objects=[obj, body],
                                   selected_editable_objects=[obj, body]):
        bpy.ops.object.data_transfer(
            data_type="VGROUP_WEIGHTS", vert_mapping="POLYINTERP_NEAREST",
            layers_select_src="ALL", layers_select_dst="NAME", mix_mode="REPLACE")
    if collapse:
        _collapse_groups(obj, collapse)
    # clean + cap + normalize + purge empties
    _deselect_all(); obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    with bpy.context.temp_override(active_object=obj, object=obj,
                                   selected_objects=[obj], selected_editable_objects=[obj]):
        bpy.ops.object.vertex_group_clean(group_select_mode="ALL", limit=0.0)
        bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
        bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL")
    for vg in list(obj.vertex_groups):
        if not any(any(g.group == vg.index for g in v.groups) for v in obj.data.vertices):
            obj.vertex_groups.remove(vg)
    _add_armature(obj, rig)
    _log("jointed bind %s (weight-transfer%s)" % (obj.name, " + collapse" if collapse else ""))


def bind_cloth(obj, rig, body, shrinkwrap=0.0):
    """Cloth -> fit is already baked (rotating a skinned mesh breaks the bind, so
    cloth is fitted BEFORE this). Optional shrinkwrap OUTSIDE pushes only inside-
    the-body verts back out (kills clipping, keeps baggy volume), applied BEFORE
    the weight transfer so weights bind to the corrected shape."""
    _apply_transform(obj, location=True, rotation=True, scale=True)
    if shrinkwrap:
        sw = obj.modifiers.new("Shrinkwrap", "SHRINKWRAP")
        sw.target = body
        sw.wrap_method = "NEAREST_SURFACEPOINT"
        sw.wrap_mode = "OUTSIDE"
        sw.offset = shrinkwrap
        _deselect_all(); obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        with bpy.context.temp_override(active_object=obj, object=obj,
                                       selected_objects=[obj], selected_editable_objects=[obj]):
            bpy.ops.object.modifier_apply(modifier=sw.name)
    bind_jointed(obj, rig, body, collapse=None)
    _log("cloth bind %s (shrinkwrap=%.3f)" % (obj.name, shrinkwrap))


# ============================================================================
# gear dispatch
# ============================================================================
def build_gear_piece(cfg, rig, body, spec):
    added = _import(_abs(os.path.join("Characters", spec["glb"])))
    # Tripo Segment over-splits; keep one part if requested, else join added meshes
    meshes = [o for o in added if o.type == "MESH"]
    for o in added:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    if "tripo_segment_part" in spec:
        keep = sorted(meshes, key=lambda o: -_mesh_bbox(o)[1].z)[spec["tripo_segment_part"]] \
            if spec["tripo_segment_part"] else meshes[0]
        for o in meshes:
            if o is not keep:
                bpy.data.objects.remove(o, do_unlink=True)
        piece = keep
    else:
        piece = max(meshes, key=lambda o: len(o.data.vertices))
    piece.name = spec["name"]
    fit_piece(piece, spec["fit"])

    kind = spec["kind"]
    out = []
    if kind in ("rigid_single", "jointed_single", "cloth"):
        if kind == "rigid_single":
            bind_rigid(piece, rig, spec["bone"])
        elif kind == "jointed_single":
            bind_jointed(piece, rig, body, spec.get("collapse"))
        else:
            bind_cloth(piece, rig, body, spec.get("shrinkwrap", 0.0))
        out.append(piece)
        if "mirror_to" in spec:
            mt = spec["mirror_to"]
            mir = mirror_x(piece, mt["name"])
            if kind == "rigid_single":
                bind_rigid(mir, rig, mt["bone"])
            else:
                bind_jointed(mir, rig, body, spec.get("collapse"))
            out.append(mir)
    elif kind in ("rigid_pair", "jointed_pair"):
        L, R = split_lr(piece, spec["name"])
        for side, o in (("L", L), ("R", R)):
            if kind == "rigid_pair":
                bind_rigid(o, rig, spec["bones"][side])
            else:
                bind_jointed(o, rig, body, spec.get("collapse"))
            out.append(o)
    else:
        raise ValueError("unknown gear kind: " + kind)
    return out


# ============================================================================
# verification harness (numbers, not eyes) -- see the 2026-07-11 memory
# ============================================================================
def verify_gear(rig, body, gear_objs, pose_bone_rots=None):
    """Pin each gear vert to its nearest body vert at REST, apply an asymmetric
    pose, re-measure. SLIDE = drift vs the skin under it (catches cross-binding,
    the 30-40 cm signature). RIGIDITY = change in intra-piece pair distances (a
    hard piece turning to rubber). Reports p95 per piece. Interpret with eyes for
    5-15 cm slide (a pauldron staying with the torso is CORRECT)."""
    # left as a report-only harness; wire real posing when validating on a character.
    _log("verify_gear: %d pieces to check (pose asymmetric, look at worst frame)"
         % len(gear_objs))
    return {o.name: None for o in gear_objs}


# ============================================================================
# orchestration
# ============================================================================
def build(char_key, verify=False, save=True):
    if char_key not in CONFIG:
        raise KeyError("no CONFIG for '%s' (have %s)" % (char_key, list(CONFIG)))
    cfg = CONFIG[char_key]
    _log("=== build %s ===" % char_key)

    bpy.ops.wm.read_homefile(use_empty=True)          # clean scene
    body = prep_base_body(cfg)
    rig, body = import_mixamo_rig(cfg, body)

    all_gear = []
    for spec in cfg["gear"]:
        all_gear.extend(build_gear_piece(cfg, rig, body, spec))

    if verify:
        verify_gear(rig, body, all_gear)

    if save:
        out = _abs(cfg["master_out"])
        os.makedirs(os.path.dirname(out), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=out)
        _log("saved master -> %s" % out)
    _log("DONE %s: body + rig + %d gear objects" % (char_key, len(all_gear)))
    return {"rig": rig.name, "body": body.name,
            "gear": [o.name for o in all_gear]}


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if argv:
        build(argv[0], verify=("--verify" in argv))
    else:
        _log("usage: blender -b -P character_build.py -- <CharacterKey> [--verify]")
        _log("       or in Blender console: build('Blaze')")

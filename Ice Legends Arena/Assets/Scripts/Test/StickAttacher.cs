using UnityEngine;

/// <summary>
/// Attaches a stick prefab to a humanoid character's right hand bone at runtime.
/// The grip offset is exposed for tuning live in the Inspector during Play —
/// so we can dial in the position/rotation, copy the values, then paste them
/// back into the prefab.
///
/// Setup:
///   1. Add this to the Y Bot (or its parent — it walks children for the Animator)
///   2. Drag Hockey_Stick.prefab into Stick Prefab
///   3. Press Play, adjust gripPositionOffset / gripRotationOffset until grip looks right
///   4. Copy the values back into the Inspector outside Play mode
/// </summary>
public class StickAttacher : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Hockey_Stick.prefab from Sports equipment - balls and outfit/Models/Prefabs/")]
    public GameObject stickPrefab;

    [Header("Target Bone")]
    public HumanBodyBones gripBone = HumanBodyBones.RightHand;

    [Tooltip("Fallback bone path if no Animator/humanoid rig is found (e.g. 'mixamorig:RightHand')")]
    public string fallbackBonePath = "";

    [Header("Grip Offset (live-tunable)")]
    [Tooltip("Local offset from the hand bone — start at 0, nudge until stick sits in palm")]
    public Vector3 gripPositionOffset = Vector3.zero;

    [Tooltip("Local rotation from the hand bone — typical hockey grip needs significant rotation")]
    public Vector3 gripRotationOffset = Vector3.zero;

    [Tooltip("Scale override for the stick (1 = prefab default)")]
    [Range(0.1f, 5f)]
    public float stickScale = 1f;

    [Header("Ground Clamp (keep blade above ice)")]
    [Tooltip("Pitch the stick up about the grip each frame so the blade tip never sinks below the ice. " +
             "Mainly matters during the shot swing-through, where the wrist sweeps the blade low.")]
    public bool clampBladeToIce = true;

    [Tooltip("Ice surface height. Blade is kept at or above this (plus clearance).")]
    public float iceY = 0f;

    [Tooltip("Small gap kept between the blade and the ice so it rests on the surface instead of z-fighting.")]
    public float bladeClearance = 0.01f;

    [Tooltip("Max degrees the stick may pitch up to lift the blade. Caps the correction so a deep dip " +
             "doesn't swing the stick away from the wrist (rubber-stick look) — beyond this the blade is " +
             "allowed to dip rather than break the grip illusion.")]
    [Range(0f, 80f)]
    public float maxClampAngle = 40f;

    [Header("Debug")]
    [Tooltip("Draw a gizmo at the grip point so you can see where the stick anchors")]
    public bool drawGizmo = true;

    [Tooltip("Draw a marker at the blade tip + a line from the grip, so puck contact is visible. " +
             "Enable the Gizmos toggle in the Game view to see it during play.")]
    public bool drawContactGizmo = true;

    private GameObject stickInstance;
    private Transform gripTransform;
    private Renderer[] stickRenderers;   // cached for the ground clamp's lowest-point search

    private void Start()
    {
        if (stickPrefab == null)
        {
            Debug.LogWarning("StickAttacher: no Stick Prefab assigned.");
            return;
        }

        gripTransform = ResolveGripBone();
        if (gripTransform == null)
        {
            Debug.LogError($"StickAttacher: could not find grip bone ({gripBone}) on {name}. Check that the model has a humanoid rig, or set fallbackBonePath.");
            return;
        }

        stickInstance = Instantiate(stickPrefab, gripTransform);
        stickInstance.name = stickPrefab.name + " (Attached)";

        // Strip any colliders from the visual stick — we don't want physics on
        // it, and a concave MeshCollider under a dynamic Rigidbody causes
        // per-frame warnings.
        foreach (var col in stickInstance.GetComponentsInChildren<Collider>())
        {
            Destroy(col);
        }

        ApplyGripTransform();

        // Cache the stick's renderers for the per-frame ground clamp, which finds the
        // lowest point from each renderer's localBounds (no mesh Read/Write needed).
        stickRenderers = stickInstance.GetComponentsInChildren<Renderer>();

        var bounds = ComputeWorldBounds(stickInstance);
        Debug.Log($"StickAttacher: attached {stickPrefab.name} to {gripTransform.name}. " +
                  $"Bone lossyScale={gripTransform.lossyScale}, world bounds size={bounds.size}, renderers={stickRenderers.Length}");
    }

    private Bounds ComputeWorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private void LateUpdate()
    {
        // Re-apply each frame so live Inspector tweaks take effect immediately
        // and so the stick stays glued to the bone through animations.
        if (stickInstance == null) return;
        ApplyGripTransform();
        if (clampBladeToIce) ClampBladeToIce();
    }

    /// <summary>
    /// Pitches the stick up about the grip point so its lowest point rests at (or above)
    /// the ice. Runs after ApplyGripTransform, which resets the rotation to the grip
    /// pose each frame — so this correction is recomputed fresh and never accumulates.
    /// FromToRotation gives the exact minimal rotation (no axis-sign guesswork); the
    /// angle is capped at maxClampAngle so a deep dip can't swing the stick off the
    /// wrist. The correction smoothly tends to zero as the low point nears the surface,
    /// so there's no pop at the threshold. Lifting "whichever point is lowest" avoids
    /// any need to identify the blade end (and handles the butt end too).
    /// </summary>
    private void ClampBladeToIce()
    {
        if (stickRenderers == null || stickRenderers.Length == 0) return;

        Vector3 pivot = stickInstance.transform.position;     // grip point (stick pivot)
        Vector3 lowest = LowestStickPointWorld();

        float targetY = iceY + bladeClearance;
        if (lowest.y >= targetY) return;                      // already clear

        Vector3 v = lowest - pivot;
        float r = v.magnitude;
        if (r < 1e-4f) return;                                // low point ~at pivot, nothing to pitch

        float targetRelY = Mathf.Min(targetY - pivot.y, r);   // can't lift above straight-up
        Vector3 h = new Vector3(v.x, 0f, v.z);
        Vector3 hDir = h.sqrMagnitude > 1e-8f ? h.normalized : Vector3.forward;
        float hLen = Mathf.Sqrt(Mathf.Max(0f, r * r - targetRelY * targetRelY));
        Vector3 desired = hDir * hLen + Vector3.up * targetRelY;   // same length r, raised to targetRelY

        Quaternion corr = Quaternion.FromToRotation(v.normalized, desired.normalized);
        corr.ToAngleAxis(out float ang, out Vector3 ax);           // ang in [0,180] for unit vectors
        ang = Mathf.Min(ang, maxClampAngle);
        stickInstance.transform.rotation = Quaternion.AngleAxis(ang, ax) * stickInstance.transform.rotation;
    }

    /// <summary>
    /// World position of the lowest point of the stick, taken as the lowest corner of
    /// each renderer's local bounding box transformed to world. Uses localBounds (always
    /// available) rather than mesh vertices, so it needs no mesh Read/Write. The OBB
    /// corner is a close-enough proxy for the blade tip for keeping it above the ice.
    /// </summary>
    private Vector3 LowestStickPointWorld()
    {
        Vector3 best = stickInstance.transform.position;
        float bestY = float.MaxValue;
        foreach (Renderer r in stickRenderers)
        {
            if (r == null) continue;
            Bounds lb = r.localBounds;
            Vector3 c = lb.center, e = lb.extents;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 cornerWorld = r.transform.TransformPoint(c + new Vector3(sx * e.x, sy * e.y, sz * e.z));
                        if (cornerWorld.y < bestY) { bestY = cornerWorld.y; best = cornerWorld; }
                    }
        }
        return best;
    }

    private void ApplyGripTransform()
    {
        stickInstance.transform.localPosition = gripPositionOffset;
        stickInstance.transform.localRotation = Quaternion.Euler(gripRotationOffset);

        // Compensate for parent scale so the stick's *world* size equals
        // stickScale, regardless of bone lossyScale. Mixamo bones often have
        // very small lossyScale (0.01 ish), which would shrink the stick to
        // invisibility if we just used localScale = 1.
        Vector3 ls = gripTransform.lossyScale;
        Vector3 inv = new Vector3(
            ls.x != 0f ? stickScale / ls.x : stickScale,
            ls.y != 0f ? stickScale / ls.y : stickScale,
            ls.z != 0f ? stickScale / ls.z : stickScale);
        stickInstance.transform.localScale = inv;
    }

    private Transform ResolveGripBone()
    {
        // Try the humanoid Animator path first
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(gripBone);
            if (bone != null) return bone;
        }

        // Fallback: search by name (Mixamo bones look like "mixamorig:RightHand")
        if (!string.IsNullOrEmpty(fallbackBonePath))
        {
            return FindChildRecursive(transform, fallbackBonePath);
        }

        // Last-ditch: search any child for "RightHand"
        return FindChildRecursive(transform, "RightHand")
            ?? FindChildRecursive(transform, "mixamorig:RightHand");
    }

    private Transform FindChildRecursive(Transform root, string nameToFind)
    {
        if (root.name == nameToFind || root.name.EndsWith(":" + nameToFind)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), nameToFind);
            if (found != null) return found;
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        if (drawGizmo && gripTransform != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 worldGrip = gripTransform.TransformPoint(gripPositionOffset);
            Gizmos.DrawWireSphere(worldGrip, 0.05f);
            Gizmos.DrawLine(gripTransform.position, worldGrip);
        }

        // Contact marker at the stick's lowest point (the blade during the swing), so
        // you can see it meet the puck (enable Gizmos in the Game view to see it there).
        // Yellow = above ice, red = touching/below (clamp at its cap).
        if (drawContactGizmo && stickInstance != null && stickRenderers != null && stickRenderers.Length > 0)
        {
            Vector3 low = LowestStickPointWorld();
            Gizmos.color = low.y <= iceY + bladeClearance + 0.005f ? Color.red : Color.yellow;
            Gizmos.DrawSphere(low, 0.04f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(stickInstance.transform.position, low);
        }
    }
}

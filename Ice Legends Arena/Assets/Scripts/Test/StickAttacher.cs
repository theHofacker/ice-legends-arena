using UnityEngine;

/// <summary>
/// Attaches a stick prefab to a humanoid character at runtime and keeps it in the
/// hands through animation.
///
/// Two modes:
///   • TwoHandAligned (default) — lays the stick along BOTH hand bones: the butt is
///     anchored at the top hand and the shaft points through the bottom hand, blade
///     beyond. Because the GA ("with stick") clips already pose both hands in a grip,
///     this makes the stick follow the natural two-handed grip in every clip (skating,
///     shooting, checking) instead of floating off one hand.
///   • SingleHandOffset — the older behavior: rigidly parent to one hand bone with a
///     fixed position/rotation offset. Kept as a fallback / for comparison.
///
/// Live-tuning: every offset is applied each LateUpdate, so tweak in Play, then copy
/// the component values back outside Play mode to persist them.
/// </summary>
public class StickAttacher : MonoBehaviour
{
    // Keep existing values 0/1 stable so already-serialized scenes don't shift.
    public enum AttachMode { TwoHandAligned = 0, SingleHandOffset = 1, RightHandPrimary = 2 }

    [Header("Prefab")]
    [Tooltip("Hockey_Stick.prefab from Sports equipment - balls and outfit/Models/Prefabs/")]
    public GameObject stickPrefab;

    [Header("Mode")]
    [Tooltip("RightHandPrimary (recommended for righty grip): pins the right hand at a fixed fraction " +
             "down the shaft and aims the rest at the left hand. Matches real hockey — the power hand " +
             "is the constant. TwoHandAligned: aims through both hands, butt anchored at left. " +
             "SingleHandOffset: rigid attach to one hand with a fixed offset (fallback).")]
    public AttachMode mode = AttachMode.TwoHandAligned;

    [Tooltip("Scale override for the stick (1 = prefab default)")]
    [Range(0.1f, 5f)]
    public float stickScale = 1f;

    [Header("Two-Hand Alignment (live-tunable)")]
    [Tooltip("Top hand — the stick's butt is anchored here. Usually the hand higher up the shaft.")]
    public HumanBodyBones topHandBone = HumanBodyBones.LeftHand;

    [Tooltip("Bottom hand — the shaft is aimed through here toward the blade.")]
    public HumanBodyBones bottomHandBone = HumanBodyBones.RightHand;

    [Tooltip("Flip if the stick points blade-up. Determines which end of the long axis is the blade.")]
    public bool flipShaft = false;

    [Tooltip("Roll about the shaft (degrees) — dial until the blade face sits flat on the ice.")]
    [Range(-180f, 180f)]
    public float rollDegrees = 0f;

    [Tooltip("Fine slide of the butt anchor in stick-local units, so the butt sits in the top hand.")]
    public Vector3 buttOffset = Vector3.zero;

    [Tooltip("Moves the grip from the wrist bone toward the fingers (0 = at the wrist, 1 = at the finger " +
             "bases). The Mixamo hand bone sits at the wrist, so a value around 0.5 seats the stick in the " +
             "palm/fingers instead of looking held by the forearm.")]
    [Range(0f, 1f)]
    public float handGripFactor = 0.5f;

    [Header("Right Hand Primary (used only in RightHandPrimary mode)")]
    [Tooltip("Where on the shaft the right (bottom) hand grips. 0 = at the butt, 1 = at the blade tip. " +
             "Real hockey grip is usually ~0.3-0.45 from the butt. Slide ±0.05 for shooting vs checking.")]
    [Range(0f, 1f)]
    public float bottomHandShaftFraction = 0.35f;

    [Tooltip("Fine offset of the right-hand anchor in stick-local space, before scale. Use to nudge the " +
             "grip slightly off the shaft centerline (e.g. to seat in the palm rather than on top).")]
    public Vector3 bottomHandOffset = Vector3.zero;

    [Header("Single-Hand Offset (used only in SingleHandOffset mode)")]
    public HumanBodyBones gripBone = HumanBodyBones.RightHand;

    [Tooltip("Fallback bone path if no Animator/humanoid rig is found (e.g. 'mixamorig:RightHand')")]
    public string fallbackBonePath = "";

    [Tooltip("Local offset from the hand bone — start at 0, nudge until stick sits in palm")]
    public Vector3 gripPositionOffset = Vector3.zero;

    [Tooltip("Local rotation from the hand bone — typical hockey grip needs significant rotation")]
    public Vector3 gripRotationOffset = Vector3.zero;

    [Header("Ground Clamp (keep blade above ice)")]
    [Tooltip("Pitch the stick up about the butt each frame so the blade never sinks below the ice. " +
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

    [Header("Manual Adjust (diagnostic)")]
    [Tooltip("When ON, LateUpdate stops re-positioning the stick — so you can pause play and drag the " +
             "stick into the hands manually in the Scene view. Right-click the component header → " +
             "'Capture Current Pose as Offsets' to back-solve rollDegrees + buttOffset from where you " +
             "placed it. Workflow: enter Play → pause → toggle this ON → drag the stick → Capture → copy " +
             "the logged values onto the component out of Play mode.")]
    public bool manualAdjust = false;

    [Header("Debug")]
    [Tooltip("Draw gizmos at the grip / hand anchor points. Cyan = top-hand grip, magenta = bottom-hand " +
             "grip, gray line = the axis the shaft is aimed along. Turn this on first to see whether the " +
             "grip points sit at the palms (right) or the wrists (handGripFactor too low).")]
    public bool drawGizmo = true;

    [Tooltip("Draw a marker at the stick's lowest point + a line from the anchor, so puck contact is " +
             "visible. Enable the Gizmos toggle in the Game view to see it during play.")]
    public bool drawContactGizmo = true;

    private GameObject stickInstance;
    private Transform parentT;           // bone the stick is parented under
    private Transform topHandT, bottomHandT;
    private Renderer[] stickRenderers;   // cached for the ground clamp's lowest-point search

    // Stick-local shaft geometry, derived from the renderer's localBounds at attach.
    private int shaftAxisIndex;          // 0=x, 1=y, 2=z — the long axis of the stick
    private float shaftAlong;            // half-length along that axis
    private Vector3 shaftBoundsCenter;   // localBounds center

    // Top-hand lock (for one-handed casts like MeteorStrike's sword swing). When engaged,
    // the top (left) hand stops driving the stick: its grip point is frozen as a fixed
    // offset in the bottom (right) hand's local space, captured at lock time. The shaft then
    // rides rigidly with the right hand — the swing stays identical to the two-handed grip,
    // but the left hand is free to follow the animation (it "lets go" of the stick). Used by
    // the two- and right-hand alignment paths; SingleHandOffset already ignores the top hand.
    private bool topHandLocked = false;
    private Vector3 lockedTopInBottomLocal;

    private void Start()
    {
        if (stickPrefab == null)
        {
            Debug.LogWarning("StickAttacher: no Stick Prefab assigned.");
            return;
        }

        // Resolve the hand bones we need and pick the parent for the stick.
        topHandT = ResolveBone(topHandBone, "LeftHand");
        bottomHandT = ResolveBone(bottomHandBone, "RightHand");
        parentT = (mode == AttachMode.TwoHandAligned)
            ? (topHandT != null ? topHandT : ResolveBone(gripBone, "RightHand"))
            : ResolveBone(gripBone, "RightHand");

        if (parentT == null)
        {
            Debug.LogError($"StickAttacher: could not resolve a parent bone on {name}. Check the humanoid rig or set fallbackBonePath.");
            return;
        }

        stickInstance = Instantiate(stickPrefab, parentT);
        stickInstance.name = stickPrefab.name + " (Attached)";

        // Strip colliders from the visual stick — no physics on it, and a concave
        // MeshCollider under a dynamic Rigidbody spams warnings.
        foreach (var col in stickInstance.GetComponentsInChildren<Collider>())
            Destroy(col);

        stickRenderers = stickInstance.GetComponentsInChildren<Renderer>();
        ComputeShaftGeometry();
        ApplyTransform();

        var bounds = ComputeWorldBounds(stickInstance);
        Debug.Log($"StickAttacher: attached {stickPrefab.name} ({mode}) under {parentT.name}. " +
                  $"renderers={stickRenderers.Length}, shaftAxis={shaftAxisIndex}, " +
                  $"top={(topHandT != null ? topHandT.name : "null")}, bottom={(bottomHandT != null ? bottomHandT.name : "null")}");
    }

    private void LateUpdate()
    {
        // Re-apply each frame so the stick tracks the animated bones and live tweaks
        // take effect immediately.
        if (stickInstance == null) return;
        // Manual-adjust mode: skip everything so the user can drag the stick freely
        // in pause mode. Re-enable to resume tracking.
        if (manualAdjust) return;
        ApplyTransform();
        if (clampBladeToIce) ClampBladeToIce();
    }

    /// <summary>
    /// Diagnostic helper. With manualAdjust ON, pause Play, drag the stick into the
    /// hands in the Scene view, then run this from the component's context menu.
    /// Back-solves the rollDegrees + buttOffset that would reproduce your placement,
    /// and logs them so you can paste the values onto the component out of Play mode.
    /// (handGripFactor and stickScale are not changed — iterate those separately if needed.)
    /// </summary>
    [ContextMenu("Capture Current Pose as Offsets")]
    private void CaptureCurrentPoseAsOffsets()
    {
        if (stickInstance == null)
        {
            Debug.LogWarning("StickAttacher: enter Play mode first — the stick is instantiated in Start().");
            return;
        }
        if (topHandT == null || bottomHandT == null)
        {
            Debug.LogWarning("StickAttacher: hand bones not resolved — check the humanoid rig.");
            return;
        }
        if (mode == AttachMode.SingleHandOffset)
        {
            Debug.LogWarning("StickAttacher: Capture not implemented for SingleHandOffset mode.");
            return;
        }

        Vector3 left = GripPointWorld(topHandT);
        Vector3 right = GripPointWorld(bottomHandT);
        Vector3 shaftLocal = ShaftAxisLocal();
        Quaternion R_current = stickInstance.transform.rotation;
        Vector3 P_current = stickInstance.transform.position;
        float scale = Mathf.Max(1e-4f, stickScale);

        // Both modes use the same butt→blade shaft direction in world (right - left). Capture
        // pulls the no-roll baseline from ComputeBaseShaftRotation so it stays consistent with
        // whatever stable reference the live path uses (the fix for the 180° blade flip).
        Vector3 shaftDir = right - left;
        if (shaftDir.sqrMagnitude < 1e-8f) { Debug.LogWarning("StickAttacher: hands coincident; can't capture."); return; }
        shaftDir.Normalize();
        Quaternion R_noRoll = ComputeBaseShaftRotation(shaftDir);
        float capturedRoll = SignedRollAngle(R_current, R_noRoll, shaftDir);

        if (mode == AttachMode.RightHandPrimary)
        {
            // Right-hand anchor in stick-local: rot^-1 * (right - P_current) / scale.
            // Decompose into along-shaft (drives bottomHandShaftFraction) + perpendicular (bottomHandOffset).
            Vector3 anchorLocal = Quaternion.Inverse(R_current) * (right - P_current) / scale;
            Vector3 buttCornerLocal = shaftBoundsCenter - shaftLocal * shaftAlong;
            Vector3 fromButt = anchorLocal - buttCornerLocal;
            float alongShaft = Vector3.Dot(fromButt, shaftLocal);                      // in stick-local units
            float capturedFraction = Mathf.Clamp01(alongShaft / Mathf.Max(1e-4f, 2f * shaftAlong));
            Vector3 alongComponent = shaftLocal * alongShaft;
            Vector3 capturedBottomOffset = fromButt - alongComponent;                  // pure perpendicular nudge

            Debug.Log(
                "StickAttacher [RightHandPrimary]: Captured pose — paste these onto the component (out of Play mode):\n" +
                $"  rollDegrees             = {capturedRoll:F2}\n" +
                $"  bottomHandShaftFraction = {capturedFraction:F3}\n" +
                $"  bottomHandOffset        = ({capturedBottomOffset.x:F4}, {capturedBottomOffset.y:F4}, {capturedBottomOffset.z:F4})\n" +
                $"  (unchanged: handGripFactor={handGripFactor:F2}, stickScale={stickScale:F2}, " +
                $"flipShaft={flipShaft}, top={topHandBone}, bottom={bottomHandBone})\n" +
                $"  diag: left={left}, right={right}, P_current={P_current}, alongShaft={alongShaft:F4} of {2f * shaftAlong:F4}");
        }
        else // TwoHandAligned
        {
            // ApplyTwoHandAlignment: position = top - rot * ((boundsAnchor + buttOffset) * scale)
            Vector3 boundsAnchor = shaftBoundsCenter - shaftLocal * shaftAlong;
            Vector3 buttLocalTarget = Quaternion.Inverse(R_current) * (left - P_current) / scale;
            Vector3 capturedButtOffset = buttLocalTarget - boundsAnchor;

            Debug.Log(
                "StickAttacher [TwoHandAligned]: Captured pose — paste these onto the component (out of Play mode):\n" +
                $"  rollDegrees = {capturedRoll:F2}\n" +
                $"  buttOffset  = ({capturedButtOffset.x:F4}, {capturedButtOffset.y:F4}, {capturedButtOffset.z:F4})\n" +
                $"  (unchanged: handGripFactor={handGripFactor:F2}, stickScale={stickScale:F2}, " +
                $"flipShaft={flipShaft}, top={topHandBone}, bottom={bottomHandBone})\n" +
                $"  diag: left={left}, right={right}, P_current={P_current}");
        }
    }

    /// <summary>Signed angle, about <paramref name="axis"/>, of the rotation that takes R_noRoll → R_current.</summary>
    private static float SignedRollAngle(Quaternion R_current, Quaternion R_noRoll, Vector3 axis)
    {
        Quaternion R_rollOnly = R_current * Quaternion.Inverse(R_noRoll);
        R_rollOnly.ToAngleAxis(out float ang, out Vector3 ax);
        if (Vector3.Dot(ax, axis) < 0f) ang = -ang;
        return Mathf.Repeat(ang + 180f, 360f) - 180f;
    }

    private void ApplyTransform()
    {
        ApplyScale();
        bool haveBothHands = topHandT != null && bottomHandT != null;
        switch (mode)
        {
            case AttachMode.RightHandPrimary when haveBothHands:
                ApplyRightHandPrimary();
                break;
            case AttachMode.TwoHandAligned when haveBothHands:
                ApplyTwoHandAlignment();
                break;
            default:
                ApplySingleHandOffset();
                break;
        }
    }

    /// <summary>
    /// Frees the top (left) hand from the stick: captures the current top-hand grip point as
    /// a fixed offset in the bottom (right) hand's local space, so from now on the shaft is
    /// oriented by the right hand alone and the left hand can move independently. Call when a
    /// one-handed cast/swing begins (e.g. MeteorStrike's SpellCast). No-op in SingleHandOffset
    /// mode (already one-handed) or before the bones resolve. Idempotent.
    /// </summary>
    public void LockTopHandToBottom()
    {
        if (topHandLocked) return;
        if (bottomHandT == null || topHandT == null) return;
        lockedTopInBottomLocal = bottomHandT.InverseTransformPoint(GripPointWorld(topHandT));
        topHandLocked = true;
    }

    /// <summary>Restores normal two-hand tracking (the left hand drives the stick again).</summary>
    public void ReleaseTopHandLock()
    {
        topHandLocked = false;
    }

    /// <summary>
    /// The top-hand grip point used by the alignment math. When locked, it's the captured
    /// offset rigidly following the bottom (right) hand; otherwise the live left-hand grip.
    /// </summary>
    private Vector3 TopGripWorld()
    {
        if (topHandLocked && bottomHandT != null)
            return bottomHandT.TransformPoint(lockedTopInBottomLocal);
        return GripPointWorld(topHandT);
    }

    /// <summary>Blade-roll reference bone's up axis — follows the right hand while locked so the
    /// blade face stays stable as the freed left hand moves.</summary>
    private Vector3 RefBoneUp()
    {
        if (topHandLocked && bottomHandT != null) return bottomHandT.up;
        return topHandT != null ? topHandT.up : transform.up;
    }

    /// <summary>
    /// Sets localScale so the stick's *world* size equals stickScale, compensating for
    /// parent (Mixamo bone) lossyScale, which is often ~0.01 and would shrink it away.
    /// </summary>
    private void ApplyScale()
    {
        Vector3 ls = parentT.lossyScale;
        stickInstance.transform.localScale = new Vector3(
            ls.x != 0f ? stickScale / ls.x : stickScale,
            ls.y != 0f ? stickScale / ls.y : stickScale,
            ls.z != 0f ? stickScale / ls.z : stickScale);
    }

    /// <summary>
    /// Lays the stick along the line through both hands: butt anchored at the top hand,
    /// shaft aimed through the bottom hand (blade beyond), with a tunable roll for the
    /// blade face. Sets world position/rotation directly so it doesn't matter which bone
    /// the stick is parented under.
    /// </summary>
    private void ApplyTwoHandAlignment()
    {
        Vector3 top = TopGripWorld();
        Vector3 bottom = GripPointWorld(bottomHandT);
        Vector3 shaftDir = bottom - top;          // butt → blade direction in world
        if (shaftDir.sqrMagnitude < 1e-8f) return;
        shaftDir.Normalize();

        Quaternion rot = ComputeShaftRotation(shaftDir);
        stickInstance.transform.rotation = rot;

        // Anchor the butt (end opposite the blade) at the top hand, plus a tunable slide.
        Vector3 shaftLocal = ShaftAxisLocal();
        Vector3 buttLocal = shaftBoundsCenter - shaftLocal * shaftAlong + buttOffset;
        Vector3 worldButtRel = rot * (buttLocal * stickScale);
        stickInstance.transform.position = top - worldButtRel;
    }

    private void ApplySingleHandOffset()
    {
        stickInstance.transform.localPosition = gripPositionOffset;
        stickInstance.transform.localRotation = Quaternion.Euler(gripRotationOffset);
    }

    /// <summary>
    /// Right-hand primary anchoring: pins a fixed point on the shaft (bottomHandShaftFraction
    /// from butt → blade) at the right hand world position, and aims the shaft so that going
    /// from the right hand toward the butt direction points at the left hand. The right hand
    /// is rock-solid — the left hand "rides" the shaft wherever the animation places it on
    /// that line. Matches a real hockey grip where the right (power) hand is the constant.
    /// </summary>
    private void ApplyRightHandPrimary()
    {
        Vector3 left = TopGripWorld();                 // aim target (frozen to right hand when locked)
        Vector3 right = GripPointWorld(bottomHandT);   // anchor
        Vector3 shaftDir = right - left;               // butt → blade direction in world
        if (shaftDir.sqrMagnitude < 1e-8f) return;
        shaftDir.Normalize();

        Quaternion rot = ComputeShaftRotation(shaftDir);
        stickInstance.transform.rotation = rot;

        // Stick-local position of the right-hand anchor along the shaft.
        // butt corner = shaftBoundsCenter - shaftLocal * shaftAlong
        // blade tip   = shaftBoundsCenter + shaftLocal * shaftAlong
        // fraction f: butt + f * (blade - butt) = butt + 2 * f * shaftLocal * shaftAlong
        Vector3 shaftLocal = ShaftAxisLocal();
        Vector3 buttCornerLocal = shaftBoundsCenter - shaftLocal * shaftAlong;
        Vector3 rightAnchorLocal = buttCornerLocal + shaftLocal * (2f * shaftAlong * bottomHandShaftFraction) + bottomHandOffset;
        Vector3 worldAnchorRel = rot * (rightAnchorLocal * stickScale);
        stickInstance.transform.position = right - worldAnchorRel;
    }

    /// <summary>
    /// Builds the stick's world rotation: aligns the long axis (shaftLocal) to <paramref name="shaftDir"/>
    /// AND orients the perpendicular (blade-face) axis using a player-frame reference so the blade
    /// roll doesn't flip when the player turns 180°. <see cref="rollDegrees"/> is applied on top.
    ///
    /// The bug being fixed: <c>Quaternion.FromToRotation(A, B)</c> picks the shortest rotation,
    /// but its axis is <c>A × B</c> — when the player turns 180° in world, <c>aim</c> flips by 180°
    /// and the cross product chooses a different "no-roll" baseline. Adding the same rollDegrees
    /// on top then leaves the blade rolled 180° from before. Using a two-axis LookRotation anchored
    /// to the top-hand bone's frame (which rotates with the player) makes the baseline stable.
    /// </summary>
    private Quaternion ComputeShaftRotation(Vector3 shaftDir)
    {
        Quaternion baseRot = ComputeBaseShaftRotation(shaftDir);
        return Quaternion.AngleAxis(rollDegrees, shaftDir) * baseRot;
    }

    /// <summary>Same as ComputeShaftRotation but without rollDegrees — used by Capture to back-solve roll.</summary>
    private Quaternion ComputeBaseShaftRotation(Vector3 shaftDir)
    {
        Vector3 shaftLocal = ShaftAxisLocal();
        Vector3 perpLocal = GetSecondaryShaftAxis();

        // Player-frame reference: the top hand bone's "up" axis. Rotates with the player so
        // the blade orientation stays consistent under player rotation. Fall back to the
        // player-root and finally world up if either is degenerate against shaftDir.
        Vector3 refWorld = RefBoneUp();
        Vector3 perpRef = Vector3.ProjectOnPlane(refWorld, shaftDir);
        if (perpRef.sqrMagnitude < 1e-4f)
        {
            refWorld = topHandT != null ? topHandT.forward : transform.forward;
            perpRef = Vector3.ProjectOnPlane(refWorld, shaftDir);
            if (perpRef.sqrMagnitude < 1e-4f)
                perpRef = Vector3.ProjectOnPlane(Vector3.up, shaftDir);
            if (perpRef.sqrMagnitude < 1e-4f)
                perpRef = Vector3.ProjectOnPlane(Vector3.forward, shaftDir);
        }
        perpRef.Normalize();

        // Build the rotation as targetBasis * sourceBasis^-1, where each basis is a
        // LookRotation: this is the unique rotation that takes (shaftLocal, perpLocal)
        // to (shaftDir, perpRef) — deterministic and stable across player orientations.
        Quaternion sourceBasis = Quaternion.LookRotation(shaftLocal, perpLocal);
        Quaternion targetBasis = Quaternion.LookRotation(shaftDir, perpRef);
        return targetBasis * Quaternion.Inverse(sourceBasis);
    }

    /// <summary>
    /// A stick-local axis perpendicular to the shaft, used as the "blade-face" reference
    /// for two-axis rotation. Picks the second-largest bounds extent — for a hockey stick
    /// this is usually parallel to the blade face, which gives intuitive rollDegrees values.
    /// </summary>
    private Vector3 GetSecondaryShaftAxis()
    {
        if (stickRenderers == null || stickRenderers.Length == 0) return Vector3.up;
        Vector3 e = stickRenderers[0].localBounds.extents;
        int second = shaftAxisIndex switch
        {
            0 => (e.y >= e.z) ? 1 : 2,
            1 => (e.x >= e.z) ? 0 : 2,
            _ => (e.x >= e.y) ? 0 : 1,
        };
        return second == 0 ? Vector3.right : (second == 1 ? Vector3.up : Vector3.forward);
    }

    /// <summary>
    /// The grip point for a hand: the wrist bone moved toward the finger bases by
    /// handGripFactor, so the stick sits in the palm/fingers rather than at the wrist.
    /// Falls back to the bone position if the hand has no finger children.
    /// </summary>
    private Vector3 GripPointWorld(Transform hand)
    {
        if (hand.childCount == 0 || handGripFactor <= 0f) return hand.position;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < hand.childCount; i++) sum += hand.GetChild(i).position;
        Vector3 fingersCentroid = sum / hand.childCount;
        return Vector3.Lerp(hand.position, fingersCentroid, handGripFactor);
    }

    /// <summary>Stick-local unit vector along the long axis, pointing toward the blade.</summary>
    private Vector3 ShaftAxisLocal()
    {
        Vector3 a = shaftAxisIndex == 0 ? Vector3.right : (shaftAxisIndex == 1 ? Vector3.up : Vector3.forward);
        return flipShaft ? -a : a;
    }

    private void ComputeShaftGeometry()
    {
        if (stickRenderers == null || stickRenderers.Length == 0) return;
        Bounds lb = stickRenderers[0].localBounds;
        shaftBoundsCenter = lb.center;
        Vector3 e = lb.extents;
        if (e.x >= e.y && e.x >= e.z) { shaftAxisIndex = 0; shaftAlong = e.x; }
        else if (e.y >= e.z) { shaftAxisIndex = 1; shaftAlong = e.y; }
        else { shaftAxisIndex = 2; shaftAlong = e.z; }
    }

    /// <summary>
    /// Pitches the stick up about its anchor so its lowest point rests at (or above) the
    /// ice. Runs after ApplyTransform (which resets the pose each frame), so it never
    /// accumulates. FromToRotation gives the exact minimal rotation; the angle is capped
    /// at maxClampAngle so a deep dip can't swing the stick off the hands. The correction
    /// tends to zero as the low point nears the surface, so there's no pop at the threshold.
    /// </summary>
    private void ClampBladeToIce()
    {
        if (stickRenderers == null || stickRenderers.Length == 0) return;

        Vector3 pivot = stickInstance.transform.position;     // anchor (butt at top hand)
        Vector3 lowest = LowestStickPointWorld();

        float targetY = iceY + bladeClearance;
        if (lowest.y >= targetY) return;                      // already clear

        Vector3 v = lowest - pivot;
        float r = v.magnitude;
        if (r < 1e-4f) return;

        float targetRelY = Mathf.Min(targetY - pivot.y, r);   // can't lift above straight-up
        Vector3 h = new Vector3(v.x, 0f, v.z);
        Vector3 hDir = h.sqrMagnitude > 1e-8f ? h.normalized : Vector3.forward;
        float hLen = Mathf.Sqrt(Mathf.Max(0f, r * r - targetRelY * targetRelY));
        Vector3 desired = hDir * hLen + Vector3.up * targetRelY;

        Quaternion corr = Quaternion.FromToRotation(v.normalized, desired.normalized);
        corr.ToAngleAxis(out float ang, out Vector3 ax);
        ang = Mathf.Min(ang, maxClampAngle);
        stickInstance.transform.rotation = Quaternion.AngleAxis(ang, ax) * stickInstance.transform.rotation;
    }

    /// <summary>
    /// World position of the blade's contact point with the ice (the stick's lowest world
    /// point — blade tip during carry, sweeping low point during a shot swing). External
    /// callers (e.g. <c>TestPuckController</c>) use this to pin the puck to the blade rather
    /// than to a fixed offset from the player. Returns the StickAttacher's own position if
    /// the stick hasn't been instantiated yet.
    /// </summary>
    public Vector3 GetBladeContactPoint()
    {
        if (stickInstance == null || stickRenderers == null || stickRenderers.Length == 0)
            return transform.position;
        return LowestStickPointWorld();
    }

    /// <summary>
    /// World position of the stick's lowest point, taken as the lowest corner of each
    /// renderer's localBounds (always available — no mesh Read/Write needed).
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
                        Vector3 w = r.transform.TransformPoint(c + new Vector3(sx * e.x, sy * e.y, sz * e.z));
                        if (w.y < bestY) { bestY = w.y; best = w; }
                    }
        }
        return best;
    }

    private Bounds ComputeWorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private Transform ResolveBone(HumanBodyBones bone, string nameHint)
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t != null) return t;
        }
        if (!string.IsNullOrEmpty(fallbackBonePath))
        {
            Transform t = FindChildRecursive(transform, fallbackBonePath);
            if (t != null) return t;
        }
        return FindChildRecursive(transform, nameHint)
            ?? FindChildRecursive(transform, "mixamorig:" + nameHint);
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
        if (drawGizmo)
        {
            if (mode == AttachMode.TwoHandAligned || mode == AttachMode.RightHandPrimary)
            {
                // Spheres at the actual grip points (palm), line = the axis the shaft lies on.
                // In RightHandPrimary, draw the right-hand anchor as a filled sphere so it's visually
                // obvious which hand is the "constant" — the other one rides the shaft.
                bool rightPrimary = mode == AttachMode.RightHandPrimary;
                if (topHandT != null)
                {
                    Gizmos.color = Color.cyan;
                    if (rightPrimary) Gizmos.DrawWireSphere(GripPointWorld(topHandT), 0.04f);
                    else Gizmos.DrawWireSphere(GripPointWorld(topHandT), 0.04f);
                }
                if (bottomHandT != null)
                {
                    Gizmos.color = Color.magenta;
                    if (rightPrimary) Gizmos.DrawSphere(GripPointWorld(bottomHandT), 0.035f);
                    else Gizmos.DrawWireSphere(GripPointWorld(bottomHandT), 0.04f);
                }
                if (topHandT != null && bottomHandT != null) { Gizmos.color = Color.gray; Gizmos.DrawLine(GripPointWorld(topHandT), GripPointWorld(bottomHandT)); }
            }
            else if (parentT != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 worldGrip = parentT.TransformPoint(gripPositionOffset);
                Gizmos.DrawWireSphere(worldGrip, 0.05f);
                Gizmos.DrawLine(parentT.position, worldGrip);
            }
        }

        // Contact marker at the stick's lowest point (the blade during the swing). Yellow
        // = above ice, red = touching/below (clamp at its cap). Enable Game-view Gizmos.
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

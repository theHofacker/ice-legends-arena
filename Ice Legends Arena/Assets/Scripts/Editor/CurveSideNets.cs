using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Replace the straight <c>Net_Left</c> / <c>Net_Right</c> box triggers on the test nets with a chain
/// of short angled box-trigger segments that follow the ACTUAL curve of the arena goal frame.
///
/// Why this instead of just using the goal's MeshCollider: a concave MeshCollider can't be a trigger
/// in PhysX (so it can't be a deaden / catch zone), and a convex one fills the net's hollow and reads
/// as solid — which is exactly why <see cref="TestNetSetup"/> DISABLES the goal's blanket mesh collider.
/// So we get the exact shape a different way: sample the goal mesh's outer silhouette ONCE here in the
/// editor and bake it into a few cheap box triggers. Exact curve, mobile-friendly runtime cost.
///
/// The footprint is sampled top-down (X across rink width, Z goal-to-goal) in a low height band so we
/// trace the bottom frame rail's curve, then each segment is raised to full net height. Re-run anytime;
/// it rebuilds the side nets from scratch and preserves the existing TestNetDeaden tuning. If the goal
/// mesh can't be found/measured it falls back to a smooth inward arc so you still get a curved net.
///
/// Coordinate system: X = rink width, Z = goal-to-goal length, Y = height. Opponent net at +Z, own at -Z.
/// </summary>
public class CurveSideNets : ScriptableWizard
{
    [Tooltip("Box segments per side. More = smoother curve, slightly more (still cheap) triggers.")]
    [Range(2, 24)] public int segmentsPerSide = 8;

    [Tooltip("Low/high fraction of net height to sample the frame footprint at. Keep low so we trace " +
             "the bottom rail's curve and skip floor flares / the open top.")]
    [Range(0f, 1f)] public float sampleBandLow = 0.1f;
    [Range(0f, 1f)] public float sampleBandHigh = 0.55f;

    [Tooltip("Curve the opponent net (+Z).")] public bool curveOpponentNet = true;
    [Tooltip("Curve your own net (-Z).")] public bool curveOwnNet = true;

    private const float NetThickness = 0.10f; // matches TestNetSetup netting thinness
    private const float FallbackBackInset = 0.30f; // inward pull at the back if no mesh to measure

    [MenuItem("Ice Legends/Curve Side Nets (fit to goal mesh)")]
    public static void Open()
    {
        DisplayWizard<CurveSideNets>("Curve Side Nets", "Build Curved Side Nets");
    }

    private void OnWizardCreate()
    {
        // Locate the arena goals the same way TestNetSetup does, so opponent/own pair up correctly.
        Transform oppGoal = null, ownGoal = null;
        GameObject rink = GameObject.Find("hockey_arena_rink");
        if (rink != null)
        {
            Transform g1 = rink.transform.Find("hockey_arena_goal");
            Transform g2 = rink.transform.Find("hockey_arena_goal (1)");
            if (g1 != null && g2 != null)
            {
                if (g1.position.z >= g2.position.z) { oppGoal = g1; ownGoal = g2; }
                else                                { oppGoal = g2; ownGoal = g1; }
            }
            else oppGoal = ownGoal = g1 ?? g2;
        }

        int built = 0;
        if (curveOpponentNet) built += CurveNet("TestNet_Opponent", oppGoal) ? 1 : 0;
        if (curveOwnNet)      built += CurveNet("TestNet_Own", ownGoal) ? 1 : 0;

        if (built > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"Curve Side Nets: rebuilt curved side netting on {built} net(s). Save the scene to keep it.");
    }

    private bool CurveNet(string netName, Transform goal)
    {
        GameObject net = GameObject.Find(netName);
        if (net == null)
        {
            Debug.LogWarning($"Curve Side Nets: '{netName}' not found — skipped.");
            return false;
        }
        Transform root = net.transform;

        // --- Read the net's tuned geometry from its existing children. ---
        Transform postL = root.Find("Post_L");
        Transform postR = root.Find("Post_R");
        if (postL == null || postR == null)
        {
            Debug.LogWarning($"Curve Side Nets: '{netName}' is missing Post_L/Post_R — skipped.");
            return false;
        }
        float h = NetHeight(postL);
        float frontXL = postL.localPosition.x; // outer X at the mouth, left (negative)
        float frontXR = postR.localPosition.x; // outer X at the mouth, right (positive)
        float mouthZ = postL.localPosition.z;  // local Z of the mouth (sign differs per net)

        // Preserve any TestNetDeaden tuning the user dialed into the current side nets.
        TestNetDeaden tuning = null;
        Transform oldL = root.Find("Net_Left");
        Transform oldR = root.Find("Net_Right");
        if (oldL != null) tuning = oldL.GetComponentInChildren<TestNetDeaden>();
        if (tuning == null && oldR != null) tuning = oldR.GetComponentInChildren<TestNetDeaden>();

        // --- Trace the goal frame's outer silhouette (per side) in net-local space. ---
        List<Vector3> leftPts, rightPts;
        bool measured = TryTraceSilhouette(goal, root, h, frontXL, frontXR, mouthZ, out leftPts, out rightPts);
        if (!measured)
        {
            Debug.LogWarning($"Curve Side Nets: couldn't measure '{netName}' goal mesh — using a " +
                             "fallback inward arc. Nudge in-scene if needed.");
            BuildFallbackArc(root, frontXL, frontXR, out leftPts, out rightPts);
        }

        // --- Rebuild the side nets. ---
        if (oldL != null) Undo.DestroyObjectImmediate(oldL.gameObject);
        if (oldR != null) Undo.DestroyObjectImmediate(oldR.gameObject);
        BuildSide(root, "Net_Left", leftPts, h, tuning);
        BuildSide(root, "Net_Right", rightPts, h, tuning);
        return true;
    }

    /// <summary>Net height from the post collider (respecting any manual scale the user applied).</summary>
    private static float NetHeight(Transform post)
    {
        BoxCollider b = post.GetComponent<BoxCollider>();
        if (b != null) return Mathf.Max(0.1f, b.size.y * post.localScale.y);
        return 1.22f; // regulation-ish fallback
    }

    /// <summary>
    /// Sample every mesh vertex under <paramref name="goal"/>, project into the net root's local XZ,
    /// and for each Z-slice take the outermost X on each side — that outer envelope is the frame curve.
    /// </summary>
    private bool TryTraceSilhouette(Transform goal, Transform root, float h, float frontXL, float frontXR,
                                    float mouthZ, out List<Vector3> leftPts, out List<Vector3> rightPts)
    {
        leftPts = new List<Vector3>();
        rightPts = new List<Vector3>();
        if (goal == null) return false;

        MeshFilter[] filters = goal.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0) return false;

        float yLow = sampleBandLow * h;
        float yHigh = Mathf.Max(yLow + 0.01f, sampleBandHigh * h);

        // First pass: collect in-band verts in net-local space and find the depth (Z) range.
        var pts = new List<Vector3>();
        float zMin = float.MaxValue, zMax = float.MinValue;
        foreach (MeshFilter mf in filters)
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null) continue;
            Transform mt = mf.transform;
            foreach (Vector3 v in mesh.vertices)
            {
                Vector3 local = root.InverseTransformPoint(mt.TransformPoint(v));
                if (local.y < yLow || local.y > yHigh) continue;
                pts.Add(local);
                if (local.z < zMin) zMin = local.z;
                if (local.z > zMax) zMax = local.z;
            }
        }
        if (pts.Count < 8 || zMax - zMin < 0.05f) return false;

        // Bucket by Z slice; keep the outermost X per side per slice.
        int n = segmentsPerSide;
        float[] leftX = new float[n + 1];   // most negative X
        float[] rightX = new float[n + 1];  // most positive X
        bool[] leftSet = new bool[n + 1], rightSet = new bool[n + 1];
        for (int i = 0; i <= n; i++) { leftX[i] = float.MaxValue; rightX[i] = float.MinValue; }

        float span = zMax - zMin;
        foreach (Vector3 p in pts)
        {
            int idx = Mathf.Clamp(Mathf.RoundToInt((p.z - zMin) / span * n), 0, n);
            if (p.x < 0f) { if (p.x < leftX[idx]) { leftX[idx] = p.x; leftSet[idx] = true; } }
            else          { if (p.x > rightX[idx]) { rightX[idx] = p.x; rightSet[idx] = true; } }
        }

        // Anchor the mouth slice (nearest the posts, NOT always slice 0 — the mouth is at +Z for the
        // own net and -Z for the opponent net) to the actual posts so the net meets the frame cleanly.
        int mouthIdx = Mathf.Clamp(Mathf.RoundToInt((mouthZ - zMin) / span * n), 0, n);
        leftX[mouthIdx] = frontXL; leftSet[mouthIdx] = true;
        rightX[mouthIdx] = frontXR; rightSet[mouthIdx] = true;

        FillGaps(leftX, leftSet);
        FillGaps(rightX, rightSet);

        for (int i = 0; i <= n; i++)
        {
            float z = zMin + span * i / n;
            leftPts.Add(new Vector3(leftX[i], 0f, z));
            rightPts.Add(new Vector3(rightX[i], 0f, z));
        }
        return true;
    }

    /// <summary>Linearly interpolate any slices that caught no vertices from their filled neighbors.</summary>
    private static void FillGaps(float[] x, bool[] set)
    {
        int n = x.Length;
        for (int i = 0; i < n; i++)
        {
            if (set[i]) continue;
            int prev = i - 1; while (prev >= 0 && !set[prev]) prev--;
            int next = i + 1; while (next < n && !set[next]) next++;
            if (prev >= 0 && next < n) x[i] = Mathf.Lerp(x[prev], x[next], (float)(i - prev) / (next - prev));
            else if (prev >= 0) x[i] = x[prev];
            else if (next < n) x[i] = x[next];
            set[i] = true;
        }
    }

    /// <summary>Smooth inward arc used when the goal mesh can't be measured.</summary>
    private void BuildFallbackArc(Transform root, float frontXL, float frontXR,
                                  out List<Vector3> leftPts, out List<Vector3> rightPts)
    {
        leftPts = new List<Vector3>();
        rightPts = new List<Vector3>();
        Transform post = root.Find("Post_L");
        Transform back = root.Find("Backstop");
        float mouthZ = post != null ? post.localPosition.z : 0f;
        float backZ = back != null ? back.localPosition.z : (mouthZ == 0f ? 1f : mouthZ * 2f);
        int n = segmentsPerSide;
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            float ease = t * t; // narrow mostly near the back
            float z = Mathf.Lerp(mouthZ, backZ, t);
            // Pull each side inward toward center X as we go back (frontXL is -, frontXR is +).
            leftPts.Add(new Vector3(frontXL + FallbackBackInset * ease, 0f, z));
            rightPts.Add(new Vector3(frontXR - FallbackBackInset * ease, 0f, z));
        }
    }

    /// <summary>Build one curved side as a chain of angled box-trigger segments under a named parent.</summary>
    private void BuildSide(Transform root, string sideName, List<Vector3> pts, float h, TestNetDeaden tuning)
    {
        GameObject side = new GameObject(sideName);
        Undo.RegisterCreatedObjectUndo(side, "Build Curved Side Net");
        side.transform.SetParent(root, false);
        side.transform.localPosition = Vector3.zero;
        side.transform.localRotation = Quaternion.identity;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[i + 1];
            Vector3 chord = b - a; chord.y = 0f;
            float len = chord.magnitude;
            if (len < 1e-4f) continue;

            GameObject seg = new GameObject($"{sideName}_{i}");
            seg.transform.SetParent(side.transform, false);
            Vector3 mid = (a + b) * 0.5f; mid.y = h * 0.5f;
            seg.transform.localPosition = mid;
            seg.transform.localRotation = Quaternion.LookRotation(chord.normalized, Vector3.up);

            BoxCollider box = seg.AddComponent<BoxCollider>();
            box.isTrigger = true;
            // +NetThickness pads the length so angled segments overlap at the joints (no puck slips through).
            box.size = new Vector3(NetThickness, h, len + NetThickness);

            TestNetDeaden d = seg.AddComponent<TestNetDeaden>();
            if (tuning != null)
            {
                d.velocityReduction = tuning.velocityReduction;
                d.slowdownRate = tuning.slowdownRate;
                d.maxSpeedInNet = tuning.maxSpeedInNet;
            }
        }
    }
}

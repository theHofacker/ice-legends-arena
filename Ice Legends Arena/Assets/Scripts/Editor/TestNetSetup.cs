using UnityEngine;
using UnityEditor;

/// <summary>
/// One-click "real net" for the 1v1 test scene. Replaces the thin <see cref="TestGoalTrigger"/>
/// fat-box stand-in with proper hockey-net geometry at each arena goal:
///
///   • Frame (SOLID, non-trigger BoxColliders): two posts + a crossbar the puck DINGS off
///     ("off the iron") — a post shot stays out for free, because the puck's physics material
///     bounces (bounceCombine = Maximum, see TestPuckController).
///   • Netting (TRIGGER + <see cref="TestNetDeaden"/>): back / two sides / top that CATCH and
///     settle the puck instead of rebounding it like a wall.
///   • Goal line (TRIGGER + reused <see cref="TestGoalTrigger"/>): a thin slab strictly between
///     the posts and under the crossbar, so a goal counts ONLY on a clean shot through the mouth
///     — not a post ding or an over-the-bar shot.
///
/// The arena's imported goal mesh is wrapped in a blanket auto-generated MeshCollider that made
/// the WHOLE net solid (hard shots pinged back out). This tool DISABLES that collider (keeping the
/// visual mesh) and lets the generated colliders define collision cleanly.
///
/// Mirrors the procedural-setup pattern of <see cref="TestGoalSetup"/> / RinkWallGenerator. The
/// geometry is gizmo-visible and nudgeable in-scene afterward. Coordinate system: X = rink width,
/// Z = goal-to-goal length, Y = height. Opponent net at +Z, own net at -Z.
/// </summary>
public static class TestNetSetup
{
    private const float DefaultGoalZ = 23.7f; // arena's natural goal line (matches TestSceneSetup)

    // Fallback net dimensions if the goal mesh can't be measured (nudge in-scene afterward).
    private const float FallbackWidth  = 1.83f;
    private const float FallbackHeight = 1.22f;
    private const float FallbackDepth  = 1.0f;

    private const float FrameThickness = 0.05f; // post / crossbar bar thickness
    private const float NetThickness   = 0.10f; // netting + goal-line slab thinness

    [MenuItem("Ice Legends/Setup Real Net")]
    public static void SetupRealNet()
    {
        // Clear any prior nets and the old thin test goals so nothing double-counts.
        foreach (string n in new[] { "TestNet_Opponent", "TestNet_Own", "TestGoal_Opponent", "TestGoal_Own" })
        {
            GameObject old = GameObject.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old);
        }

        Transform oppGoal = null, ownGoal = null;
        GameObject rink = GameObject.Find("hockey_arena_rink");
        if (rink != null)
        {
            Transform g1 = rink.transform.Find("hockey_arena_goal");
            Transform g2 = rink.transform.Find("hockey_arena_goal (1)");
            // Opponent net is the one at +Z (the player attacks +Z); own net at -Z.
            if (g1 != null && g2 != null)
            {
                if (g1.position.z >= g2.position.z) { oppGoal = g1; ownGoal = g2; }
                else                                { oppGoal = g2; ownGoal = g1; }
            }
            else
            {
                oppGoal = g1 ?? g2; // whatever we found; the other falls back to a default position
            }
        }

        CreateNet("TestNet_Opponent", oppGoal, new Vector3(0f, 0f,  DefaultGoalZ), isPlayerNet: false);
        CreateNet("TestNet_Own",      ownGoal, new Vector3(0f, 0f, -DefaultGoalZ), isPlayerNet: true);

        Debug.Log("Real net created (solid frame + soft netting + thin goal line). Green slab = " +
                  "opponent net, red slab = your net. The arena goal mesh collider was disabled; nudge / " +
                  "scale the generated pieces in-scene to match the visual mesh if needed.");
    }

    /// <summary>
    /// Turn each net's thin <c>GoalLine</c> slab into a DEEP goal-detection volume that fills the net
    /// interior — from just inside the mouth back to the backstop, bounded between the posts (width) and
    /// under the crossbar (height). The thin slab only registered shots that crossed it dead-center, so
    /// angled goals into the curved side netting slipped past it (and the aggressive side deaden killed
    /// the puck before it reached the slab). A deep volume counts ANY puck that gets inside the net at
    /// any angle, while wide shots (outside the posts) and over-the-bar shots (above the crossbar) still
    /// don't enter it. Reads each net's live Post_L / Backstop, so it respects your in-scene tuning and
    /// is safe to re-run. Width / height of the volume are left as you tuned them — only depth changes.
    /// </summary>
    [MenuItem("Ice Legends/Fix Goal Volume (reliable scoring)")]
    public static void FixGoalVolume()
    {
        int fixedCount = 0;
        foreach (string netName in new[] { "TestNet_Opponent", "TestNet_Own" })
        {
            GameObject net = GameObject.Find(netName);
            if (net == null) continue;
            Transform root = net.transform;

            Transform post = root.Find("Post_L");
            Transform back = root.Find("Backstop");
            Transform line = root.Find("GoalLine");
            if (post == null || back == null || line == null)
            {
                Debug.LogWarning($"Fix Goal Volume: '{netName}' missing Post_L / Backstop / GoalLine — skipped.");
                continue;
            }
            BoxCollider box = line.GetComponent<BoxCollider>();
            if (box == null)
            {
                Debug.LogWarning($"Fix Goal Volume: '{netName}' GoalLine has no BoxCollider — skipped.");
                continue;
            }

            float mouthZ = post.localPosition.z;
            float backZ = back.localPosition.z;
            float depthDir = Mathf.Sign(backZ - mouthZ);
            // Front face sits just INSIDE the mouth (so a puck merely grazing the front of the posts
            // from the crease side doesn't count); back face reaches the backstop.
            float front = mouthZ + depthDir * (NetThickness * 0.5f);
            float depth = Mathf.Abs(backZ - front);

            Undo.RecordObject(line, "Fix Goal Volume");
            Vector3 lp = line.localPosition;
            lp.z = (front + backZ) * 0.5f;
            line.localPosition = lp;

            Undo.RecordObject(box, "Fix Goal Volume Collider");
            Vector3 c = box.center; c.z = 0f; box.center = c;       // keep width/height offsets, zero depth offset
            Vector3 s = box.size;   s.z = depth; box.size = s;      // keep tuned width/height, deepen Z

            fixedCount++;
        }

        if (fixedCount > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"Fix Goal Volume: deepened the goal trigger on {fixedCount} net(s) to fill the net " +
                  "interior. Save the scene to keep it.");
    }

    /// <summary>
    /// STAGE 1 of promoting TestMovement to the real gameplay scene: wire the net's scoring through
    /// <see cref="GameManager"/> instead of the self-contained <c>TestGoalTrigger</c>. Adds a
    /// GameManager (+ a minimal <see cref="TestMatchHUD"/>) if the scene has none, then swaps each
    /// net's GoalLine from TestGoalTrigger to the real <see cref="GoalTrigger"/> — carrying the
    /// isPlayerNet flag over to isPlayerGoal (identical meaning: the puck entering the player's own
    /// net counts for the opponent). The real GoalTrigger counts only while the match is Playing and
    /// calls GameManager.GoalScored, which handles score / events / the face-off reset loop (so the
    /// old per-trigger celebration+reset is no longer needed). Safe to re-run.
    /// Netting (TestNetDeaden) is left as-is — it's puck-tag-gated and scene-agnostic; swapping it to
    /// NetPhysics is cosmetic cleanup for a later stage, not part of scoring.
    /// </summary>
    [MenuItem("Ice Legends/Wire Net Scoring to GameManager")]
    public static void WireNetScoringToGameManager()
    {
        // --- 1. Ensure a GameManager (+ HUD) exists. ---
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            GameObject go = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(go, "Add GameManager");
            gm = Undo.AddComponent<GameManager>(go);
            Undo.AddComponent<TestMatchHUD>(go);
            Debug.Log("Wire Net Scoring: created a GameManager (+ TestMatchHUD) — defaults are fine " +
                      "(5:00 match, center-ice face-off). It auto-starts the match on Play.");
        }
        else if (gm.GetComponent<TestMatchHUD>() == null)
        {
            Undo.AddComponent<TestMatchHUD>(gm.gameObject);
        }

        // --- 2. Swap each net's GoalLine: TestGoalTrigger -> real GoalTrigger. ---
        int swapped = 0, already = 0;
        foreach (string netName in new[] { "TestNet_Opponent", "TestNet_Own" })
        {
            GameObject net = GameObject.Find(netName);
            if (net == null) continue;
            Transform line = net.transform.Find("GoalLine");
            if (line == null)
            {
                Debug.LogWarning($"Wire Net Scoring: '{netName}' has no GoalLine — skipped.");
                continue;
            }

            TestGoalTrigger test = line.GetComponent<TestGoalTrigger>();
            if (test == null)
            {
                if (line.GetComponent<GoalTrigger>() != null) already++;
                else Debug.LogWarning($"Wire Net Scoring: '{netName}/GoalLine' has neither trigger — skipped.");
                continue;
            }

            bool isPlayerNet = test.isPlayerNet; // player's own net => opponent scores here
            Undo.DestroyObjectImmediate(test);

            GoalTrigger real = line.GetComponent<GoalTrigger>();
            if (real == null) real = Undo.AddComponent<GoalTrigger>(line.gameObject);
            var so = new SerializedObject(real);
            so.FindProperty("isPlayerGoal").boolValue = isPlayerNet;
            so.ApplyModifiedProperties();
            swapped++;
        }

        // --- 3. Sanity-check the puck tag GameManager relies on. ---
        GameObject puck = null;
        try { puck = GameObject.FindWithTag("Puck"); } catch { /* tag undefined */ }
        if (puck == null)
            Debug.LogWarning("Wire Net Scoring: no active object tagged 'Puck' found — GameManager " +
                             "won't find the puck at runtime. Tag the puck 'Puck' before testing.");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"Wire Net Scoring: swapped {swapped} goal line(s) to the real GoalTrigger" +
                  $"{(already > 0 ? $" ({already} already swapped)" : "")}. Scoring now flows through " +
                  "GameManager. Press Play — after the face-off the score updates on a goal. Save the scene to keep it.");
    }

    /// <summary>
    /// Copy the manually-tuned geometry of TestNet_Opponent onto TestNet_Own as a true mirror across
    /// center ice (Z=0). The two net roots sit at +Z / -Z and each opens toward center, so within each
    /// root's local space the mirror is a Z-flip: local X / Y / scale / collider size are identical,
    /// only local Z (position + collider center) is negated. Matches children by name, so it's safe to
    /// re-run after any in-scene nudge of the opponent net. Leaves components (TestGoalTrigger,
    /// TestNetDeaden) and the roots themselves untouched.
    /// </summary>
    [MenuItem("Ice Legends/Mirror Opponent Net → Own Net")]
    public static void MirrorOpponentNetToOwn()
    {
        GameObject opp = GameObject.Find("TestNet_Opponent");
        GameObject own = GameObject.Find("TestNet_Own");
        if (opp == null || own == null)
        {
            Debug.LogError("Mirror Net: need both TestNet_Opponent and TestNet_Own in the scene " +
                           "(run 'Setup Real Net' first).");
            return;
        }

        // Index the own net's children by name so we can pair them up regardless of sibling order.
        var ownChildren = new System.Collections.Generic.Dictionary<string, Transform>();
        foreach (Transform c in own.transform) ownChildren[c.name] = c;

        int mirrored = 0, missing = 0;
        foreach (Transform src in opp.transform)
        {
            if (!ownChildren.TryGetValue(src.name, out Transform dst))
            {
                Debug.LogWarning($"Mirror Net: TestNet_Own has no child named '{src.name}' — skipped.");
                missing++;
                continue;
            }

            Undo.RecordObject(dst, "Mirror Net Piece");
            Vector3 p = src.localPosition;
            dst.localPosition = new Vector3(p.x, p.y, -p.z);
            dst.localScale = src.localScale;
            dst.localRotation = src.localRotation; // posts/bars are axis-aligned; keep them in sync too

            BoxCollider sb = src.GetComponent<BoxCollider>();
            BoxCollider db = dst.GetComponent<BoxCollider>();
            if (sb != null && db != null)
            {
                Undo.RecordObject(db, "Mirror Net Collider");
                db.size = sb.size;
                Vector3 ctr = sb.center;
                db.center = new Vector3(ctr.x, ctr.y, -ctr.z);
            }
            mirrored++;
        }

        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(own.scene);

        Debug.Log($"Mirror Net: copied {mirrored} piece(s) from TestNet_Opponent onto TestNet_Own " +
                  $"(Z-flipped mirror across center ice){(missing > 0 ? $", {missing} unmatched" : "")}. " +
                  "Save the scene to keep it.");
        Selection.activeGameObject = own;
    }

    /// <param name="goal">The arena goal transform (for auto-sizing + collider disable); may be null.</param>
    /// <param name="fallbackPos">Where to place the net if <paramref name="goal"/> is null.</param>
    private static void CreateNet(string name, Transform goal, Vector3 fallbackPos, bool isPlayerNet)
    {
        // --- Measure the goal (auto-fit) and silence its blanket mesh collider. ---
        Vector3 rootPos;
        float w, h, d;
        if (goal != null && TryMeasureGoal(goal, out Bounds b))
        {
            rootPos = new Vector3(b.center.x, b.min.y, b.center.z);
            w = b.size.x; h = b.size.y; d = b.size.z;

            foreach (MeshCollider mc in goal.GetComponentsInChildren<MeshCollider>(true))
            {
                Undo.RecordObject(mc, "Disable arena goal collider");
                mc.enabled = false;
            }
        }
        else
        {
            rootPos = (goal != null) ? new Vector3(goal.position.x, 0f, goal.position.z) : fallbackPos;
            w = FallbackWidth; h = FallbackHeight; d = FallbackDepth;
        }

        // depthDir points from the mouth (center-ice side) toward the back net.
        float depthDir = Mathf.Sign(rootPos.z == 0f ? (isPlayerNet ? -1f : 1f) : rootPos.z);

        GameObject root = new GameObject(name);
        root.transform.position = rootPos;
        Undo.RegisterCreatedObjectUndo(root, "Create Real Net");

        float halfW = w * 0.5f;
        float mouthZ = -depthDir * (d * 0.5f); // local Z of the goal line / front mouth
        float backZ  =  depthDir * (d * 0.5f); // local Z of the back netting

        // --- Frame: SOLID posts + crossbar at the mouth (puck dings off these). ---
        AddBox(root.transform, "Post_L",   new Vector3(-halfW, h * 0.5f, mouthZ), new Vector3(FrameThickness, h, FrameThickness), trigger: false);
        AddBox(root.transform, "Post_R",   new Vector3( halfW, h * 0.5f, mouthZ), new Vector3(FrameThickness, h, FrameThickness), trigger: false);
        AddBox(root.transform, "Crossbar", new Vector3(0f, h, mouthZ),            new Vector3(w + FrameThickness, FrameThickness, FrameThickness), trigger: false);

        // --- Netting: TRIGGER + deaden (catches & settles). The back catch zone is a DEEP volume
        //     (not a thin shell) sitting just in front of a SOLID backstop: a hard shot blasts
        //     through the open front of the net, bleeds in the catch zone, taps the backstop and
        //     rattles a couple times before settling — instead of dead-stopping at the line. ---
        float backCatchDepth = Mathf.Min(0.3f, d * 0.5f);
        AddNetting(root.transform, "Net_Back",  new Vector3(0f, h * 0.5f, backZ - depthDir * (backCatchDepth * 0.5f)), new Vector3(w, h, backCatchDepth));
        AddNetting(root.transform, "Net_Left",  new Vector3(-halfW, h * 0.5f, 0f),   new Vector3(NetThickness, h, d));
        AddNetting(root.transform, "Net_Right", new Vector3( halfW, h * 0.5f, 0f),   new Vector3(NetThickness, h, d));
        AddNetting(root.transform, "Net_Top",   new Vector3(0f, h, 0f),              new Vector3(w, NetThickness, d));

        // Solid back wall flush with the visible back netting — the failsafe that stops any puck
        // the catch zone didn't fully kill (so nothing escapes out the back of the net).
        AddBox(root.transform, "Backstop", new Vector3(0f, h * 0.5f, backZ), new Vector3(w, h, FrameThickness), trigger: false);

        // --- Goal line: thin slab strictly BETWEEN the posts and UNDER the crossbar. COUNT ONLY —
        //     no deaden here, so the puck keeps its speed and blasts into the net. ---
        GameObject line = AddBox(root.transform, "GoalLine",
            new Vector3(0f, h * 0.5f, mouthZ + depthDir * (NetThickness * 0.5f)),
            new Vector3(w - FrameThickness, h, NetThickness), trigger: true);
        TestGoalTrigger trig = Undo.AddComponent<TestGoalTrigger>(line);
        trig.isPlayerNet = isPlayerNet;
        trig.deadenPuckOnEntry = false;

        Selection.activeGameObject = root;
    }

    /// <summary>Encapsulate every child Renderer's world bounds to get the goal's AABB.</summary>
    private static bool TryMeasureGoal(Transform goal, out Bounds bounds)
    {
        bounds = default;
        Renderer[] rends = goal.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return false;
        bounds = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
        return bounds.size.x > 0.01f && bounds.size.y > 0.01f;
    }

    private static GameObject AddBox(Transform parent, string name, Vector3 localPos, Vector3 size, bool trigger)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = trigger;
        box.size = size;
        return go;
    }

    private static void AddNetting(Transform parent, string name, Vector3 localPos, Vector3 size)
    {
        GameObject go = AddBox(parent, name, localPos, size, trigger: true);
        go.AddComponent<TestNetDeaden>();
    }
}

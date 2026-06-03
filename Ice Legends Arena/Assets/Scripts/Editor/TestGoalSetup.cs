using UnityEngine;
using UnityEditor;

/// <summary>
/// One-click setup for the thin test-scene goals. Creates a trigger box at each net (auto-finding
/// the arena's goal positions when the rink is in the scene, otherwise falling back to the rink's
/// natural goal line at Z ≈ ±23.7) and attaches a <see cref="TestGoalTrigger"/>.
///
/// The boxes are deliberately tall (ice → ~2.2m) so airborne roof shots count. Nudge / scale them
/// in the scene afterward to line up with the actual net mouths — the gizmos make them visible
/// (green = opponent net, red = your net).
/// </summary>
public static class TestGoalSetup
{
    private const float DefaultGoalZ = 23.7f; // arena's natural goal line (see TestSceneSetup)
    private const float MouthInset = 0.3f;    // pull the trigger toward center ice so it sits at the mouth

    [MenuItem("Ice Legends/Setup Test Goals")]
    public static void SetupTestGoals()
    {
        Vector3 opp = new Vector3(0f, 0f, DefaultGoalZ);
        Vector3 own = new Vector3(0f, 0f, -DefaultGoalZ);

        // Prefer the real arena goal positions if the rink is present.
        GameObject rink = GameObject.Find("hockey_arena_rink");
        if (rink != null)
        {
            Transform g1 = rink.transform.Find("hockey_arena_goal");
            Transform g2 = rink.transform.Find("hockey_arena_goal (1)");
            if (g1 != null) opp = g1.position;
            if (g2 != null) own = g2.position;
        }

        // Opponent net is at +Z (the player attacks +Z); own net at -Z.
        if (own.z > opp.z) { (opp, own) = (own, opp); }

        CreateGoal("TestGoal_Opponent", opp, isPlayerNet: false);
        CreateGoal("TestGoal_Own", own, isPlayerNet: true);

        Debug.Log("Test goals created (green = opponent net, red = your net). Nudge / scale the " +
                  "trigger boxes in the scene to line up with the net mouths if needed.");
    }

    private static void CreateGoal(string name, Vector3 goalPos, bool isPlayerNet)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        GameObject go = new GameObject(name);
        float z = goalPos.z - Mathf.Sign(goalPos.z) * MouthInset; // sit just in front of the net
        go.transform.position = new Vector3(goalPos.x, 0f, z);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 1.1f, 0f);   // span ice → ~2.2m up so air pucks count
        box.size = new Vector3(3f, 2.2f, 1.2f);

        TestGoalTrigger trig = go.AddComponent<TestGoalTrigger>();
        trig.isPlayerNet = isPlayerNet;

        Undo.RegisterCreatedObjectUndo(go, "Create Test Goal");
        Selection.activeGameObject = go;
    }
}

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEditor.SceneManagement;

/// <summary>
/// One-click setup for player switching in the test scene. Gives every my-team skater the dual
/// controller loadout (<see cref="TestPlayerController"/> + <see cref="TestTeammateController"/>) that
/// <see cref="TestTeamController"/> toggles between, copying the human player's tuned component values
/// onto teammates so a switched-to skater plays identically. Safe to re-run.
///
/// Menu: Ice Legends → Setup Switchable Team
/// </summary>
public static class TestTeamSetup
{
    [MenuItem("Ice Legends/Setup Switchable Team")]
    public static void SetupSwitchableTeam()
    {
        // The human player = the GameObject with an ENABLED TestPlayerController.
        TestPlayerController player = null;
        foreach (TestPlayerController pc in Object.FindObjectsByType<TestPlayerController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pc.enabled) { player = pc; break; }
        }
        if (player == null)
        {
            EditorUtility.DisplayDialog("Setup Switchable Team",
                "No active TestPlayerController found in the scene. Open TestMovement and make sure the " +
                "human player has an enabled TestPlayerController, then re-run.", "OK");
            return;
        }

        TestShotAimer playerAimer = player.GetComponent<TestShotAimer>();

        // All teammates (each already has a TestTeammateController).
        TestTeammateController[] teammates = Object.FindObjectsByType<TestTeammateController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int converted = 0;

        // 1) Make each teammate switchable: add a (disabled, tuning-matched) TestPlayerController + aimer.
        foreach (TestTeammateController tc in teammates)
        {
            GameObject go = tc.gameObject;
            if (go == player.gameObject) continue; // the player isn't its own teammate

            TestPlayerController pc = go.GetComponent<TestPlayerController>();
            if (pc == null) pc = Undo.AddComponent<TestPlayerController>(go); // auto-adds TimingMeter (RequireComponent)

            ComponentUtility.CopyComponent(player);
            ComponentUtility.PasteComponentValues(pc);
            // Reset per-instance object refs that must NOT point at the human player's objects.
            SerializedObject so = new SerializedObject(pc);
            SerializedProperty animProp = so.FindProperty("animator");
            if (animProp != null) animProp.objectReferenceValue = null; // Start() auto-finds this skater's own
            so.ApplyModifiedProperties();
            pc.enabled = false; // AI until switched to

            if (playerAimer != null)
            {
                TestShotAimer aim = go.GetComponent<TestShotAimer>();
                if (aim == null) aim = Undo.AddComponent<TestShotAimer>(go);
                ComponentUtility.CopyComponent(playerAimer);
                ComponentUtility.PasteComponentValues(aim);
                aim.enabled = false;
            }

            tc.enabled = true; // AI controller drives it until switched to
            EditorUtility.SetDirty(go);
            converted++;
        }

        // 2) Give the human player a (disabled) TestTeammateController so it can become AI when you
        //    switch off it. Copy tuning from an existing teammate when there is one.
        TestTeammateController playerTc = player.GetComponent<TestTeammateController>();
        if (playerTc == null) playerTc = Undo.AddComponent<TestTeammateController>(player.gameObject);
        if (teammates.Length > 0 && teammates[0] != null && teammates[0] != playerTc)
        {
            ComponentUtility.CopyComponent(teammates[0]);
            ComponentUtility.PasteComponentValues(playerTc);
        }
        playerTc.enabled = false;
        player.enabled = true;
        EditorUtility.SetDirty(player.gameObject);

        // 3) Ensure a TestTeamController exists in the scene.
        TestTeamController manager = Object.FindFirstObjectByType<TestTeamController>();
        if (manager == null)
        {
            GameObject mgr = new GameObject("TestTeamController");
            Undo.RegisterCreatedObjectUndo(mgr, "Create TestTeamController");
            mgr.AddComponent<TestTeamController>();
        }

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);

        EditorUtility.DisplayDialog("Setup Switchable Team",
            $"Done. Player: {player.name}\nSwitchable teammates: {converted}\n" +
            (playerAimer == null ? "(No TestShotAimer on the player — teammates won't get aimed shots/one-timer assist.)\n" : "") +
            "\nSave the scene, then Play:\n" +
            "• Tab = take control of the nearest teammate.\n" +
            "• B = pass + auto-switch to the receiver.\n" +
            "• Tap Space as a pass arrives = one-timer.", "OK");
    }
}

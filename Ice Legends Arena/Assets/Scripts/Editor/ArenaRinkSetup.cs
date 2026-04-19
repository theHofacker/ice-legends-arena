using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tools to align the 3D hockey arena rink with 2D gameplay objects.
/// Handles rink scaling, goal alignment, camera setup, and collider sync.
/// Menu: Tools > Ice Legends > Arena Setup
/// </summary>
public class ArenaRinkSetup : MonoBehaviour
{
    // 2D gameplay area dimensions (from RinkWallGenerator)
    private const float GAMEPLAY_RINK_LENGTH = 67f;  // X axis (end-to-end including boards)
    private const float GAMEPLAY_RINK_WIDTH = 28.5f;  // Y axis (side-to-side)

    // ================================================================
    // STEP 1: Scale and orient the 3D rink to match gameplay area
    // ================================================================

    [MenuItem("Tools/Ice Legends/Arena Setup/Step 1 - Scale Rink to Gameplay")]
    public static void ScaleRinkToGameplay()
    {
        GameObject rink = FindRinkInScene();
        if (rink == null) { ShowRinkNotFound(); return; }

        Bounds meshBounds = CalculateRawMeshBounds(rink);
        if (meshBounds.size == Vector3.zero) { ShowNoMeshes(); return; }

        Debug.Log("=== Step 1: Scale Rink to Gameplay ===");
        Debug.Log($"Raw mesh bounds (identity transform): size={meshBounds.size}");

        // Determine which mesh axis is the rink's long axis (length) vs short axis (width)
        // The rink model's ice is on the XZ plane, Y is up (height/boards)
        float meshX = meshBounds.size.x;
        float meshY = meshBounds.size.y; // height (boards, glass)
        float meshZ = meshBounds.size.z;

        // Long axis = rink length, short axis = rink width
        bool xIsLong = meshX >= meshZ;
        float meshLength = xIsLong ? meshX : meshZ;
        float meshWidth = xIsLong ? meshZ : meshX;

        Debug.Log($"Mesh length ({(xIsLong ? "X" : "Z")}): {meshLength:F2}");
        Debug.Log($"Mesh width ({(xIsLong ? "Z" : "X")}): {meshWidth:F2}");
        Debug.Log($"Mesh height (Y): {meshY:F2}");
        Debug.Log($"Mesh aspect ratio: {meshLength / meshWidth:F2} (real hockey rink ~2.17)");

        Undo.RecordObject(rink.transform, "Scale Arena Rink");

        // Rotation: -90 on X to lay ice flat on XY gameplay plane
        // After -90X rotation:
        //   Model X → Gameplay X (unchanged)
        //   Model Z → Gameplay Y
        //   Model Y → Gameplay -Z (depth, into screen)
        //
        // If X is the long axis: length maps to gameplay X, width (Z) maps to gameplay Y.
        // If Z is the long axis: we also need 90° Y rotation to swap X and Z, so length maps to gameplay X.
        float yRotation = xIsLong ? 0f : 90f;
        rink.transform.rotation = Quaternion.Euler(-90f, yRotation, 0f);

        // Calculate scale to match gameplay dimensions
        // After rotation, the length axis should map to gameplay X and width to gameplay Y
        float scaleForLength = GAMEPLAY_RINK_LENGTH / meshLength;
        float scaleForWidth = GAMEPLAY_RINK_WIDTH / meshWidth;
        float uniformScale = (scaleForLength + scaleForWidth) / 2f;

        // Use uniform scale to avoid distortion
        rink.transform.localScale = Vector3.one * uniformScale;
        rink.transform.position = Vector3.zero;

        MarkSceneDirty();

        // Verify with world bounds
        Bounds worldBounds = CalculateWorldBounds(rink);
        Debug.Log($"");
        Debug.Log($"Applied: rotation=({-90}, {yRotation}, 0), uniform scale={uniformScale:F4}");
        Debug.Log($"World bounds after scaling: size=({worldBounds.size.x:F1}, {worldBounds.size.y:F1}, {worldBounds.size.z:F1})");
        Debug.Log($"World X span: {worldBounds.size.x:F1} (target: {GAMEPLAY_RINK_LENGTH})");
        Debug.Log($"World Y span: {worldBounds.size.y:F1} (target: {GAMEPLAY_RINK_WIDTH})");
        Debug.Log($"");
        Debug.Log("NEXT: Run 'Step 2 - Align Goals to Rink' to move 2D goals to match 3D rink");

        EditorUtility.DisplayDialog("Rink Scaled",
            $"Rotation: (-90, {yRotation}, 0)\n" +
            $"Scale: {uniformScale:F4}\n\n" +
            $"World size: ({worldBounds.size.x:F1}, {worldBounds.size.y:F1})\n" +
            $"Target: ({GAMEPLAY_RINK_LENGTH}, {GAMEPLAY_RINK_WIDTH})\n\n" +
            $"Next: Run Step 2 to align goals.",
            "OK");
    }

    // ================================================================
    // STEP 2: Move 2D goals to match 3D rink position
    // ================================================================

    [MenuItem("Tools/Ice Legends/Arena Setup/Step 2 - Align Goals to Rink")]
    public static void AlignGoalsToRink()
    {
        GameObject rink = FindRinkInScene();
        if (rink == null) { ShowRinkNotFound(); return; }

        Bounds rinkBounds = CalculateWorldBounds(rink);

        Debug.Log("=== Step 2: Align Goals to Rink ===");
        Debug.Log($"Rink world bounds: X=[{rinkBounds.min.x:F1}, {rinkBounds.max.x:F1}], Y=[{rinkBounds.min.y:F1}, {rinkBounds.max.y:F1}]");

        // Goals should be positioned 4m from the end boards (proportional to rink size)
        // In real hockey: 4m from end boards on a 61m rink = 6.6% from each end
        float goalInsetRatio = 4f / 61f; // ~6.6%
        float goalInset = rinkBounds.size.x * goalInsetRatio;

        float westGoalX = rinkBounds.min.x + goalInset;
        float eastGoalX = rinkBounds.max.x - goalInset;
        float centerY = rinkBounds.center.y;

        Debug.Log($"Goal positions: West={westGoalX:F1}, East={eastGoalX:F1}, Center Y={centerY:F1}");

        // Find existing goals
        GameObject goalsContainer = GameObject.Find("Goals");
        if (goalsContainer == null)
        {
            Debug.LogError("No 'Goals' container found in scene. Run Tools > Setup Hockey Goals first.");
            return;
        }

        int goalsAligned = 0;
        foreach (Transform goalTransform in goalsContainer.transform)
        {
            Undo.RecordObject(goalTransform, "Align Goal to Rink");

            if (goalTransform.name.Contains("West"))
            {
                goalTransform.position = new Vector3(westGoalX, centerY, 0f);
                Debug.Log($"Moved {goalTransform.name} to ({westGoalX:F1}, {centerY:F1}, 0)");
                goalsAligned++;
            }
            else if (goalTransform.name.Contains("East"))
            {
                goalTransform.position = new Vector3(eastGoalX, centerY, 0f);
                Debug.Log($"Moved {goalTransform.name} to ({eastGoalX:F1}, {centerY:F1}, 0)");
                goalsAligned++;
            }
        }

        if (goalsAligned == 0)
        {
            Debug.LogWarning("No goals named 'WestGoal' or 'EastGoal' found under Goals container.");
            Debug.LogWarning("Goal children found:");
            foreach (Transform child in goalsContainer.transform)
            {
                Debug.LogWarning($"  - {child.name} at {child.position}");
            }
            return;
        }

        MarkSceneDirty();

        Debug.Log($"");
        Debug.Log($"Aligned {goalsAligned} goals to rink bounds.");
        Debug.Log("NEXT: Run 'Step 3 - Update Camera Bounds' to match camera to new rink area");
    }

    // ================================================================
    // STEP 3: Update camera bounds and player positions
    // ================================================================

    [MenuItem("Tools/Ice Legends/Arena Setup/Step 3 - Update Camera and Player Bounds")]
    public static void UpdateCameraAndPlayerBounds()
    {
        GameObject rink = FindRinkInScene();
        if (rink == null) { ShowRinkNotFound(); return; }

        Bounds rinkBounds = CalculateWorldBounds(rink);

        Debug.Log("=== Step 3: Update Camera & Player Bounds ===");

        // Update PuckFollowCamera bounds
        PuckFollowCamera followCam = FindAnyObjectByType<PuckFollowCamera>();
        if (followCam != null)
        {
            Undo.RecordObject(followCam, "Update Camera Bounds");

            // Camera bounds should be slightly inside the rink so camera doesn't go past boards
            float xPadding = rinkBounds.size.x * 0.1f;
            float yPadding = rinkBounds.size.y * 0.1f;

            followCam.minBounds = new Vector2(rinkBounds.min.x + xPadding, rinkBounds.min.y + yPadding);
            followCam.maxBounds = new Vector2(rinkBounds.max.x - xPadding, rinkBounds.max.y - yPadding);

            // Set broadcast mode
            followCam.cameraMode = PuckFollowCamera.CameraMode.Broadcast;

            Debug.Log($"Camera bounds updated: min={followCam.minBounds}, max={followCam.maxBounds}");
            Debug.Log($"Camera mode set to: Broadcast");
        }
        else
        {
            Debug.LogWarning("No PuckFollowCamera found in scene.");
        }

        // Update GameManager face-off position
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            Undo.RecordObject(gm, "Update Face-off Position");
            // Center ice should be at rink center
            SerializedObject gmSO = new SerializedObject(gm);
            SerializedProperty centerIce = gmSO.FindProperty("centerIcePosition");
            if (centerIce != null)
            {
                centerIce.vector2Value = new Vector2(rinkBounds.center.x, rinkBounds.center.y);
                gmSO.ApplyModifiedProperties();
                Debug.Log($"Center ice position updated to ({rinkBounds.center.x:F1}, {rinkBounds.center.y:F1})");
            }
        }

        // Reposition players to be within the rink bounds
        RepositionPlayersToRink(rinkBounds);

        MarkSceneDirty();

        Debug.Log($"");
        Debug.Log("NEXT: Run 'Step 4 - Setup Broadcast Camera' to set the camera angle");
    }

    // ================================================================
    // STEP 4: Set up broadcast camera angle
    // ================================================================

    [MenuItem("Tools/Ice Legends/Arena Setup/Step 4 - Setup Broadcast Camera")]
    public static void SetupBroadcastCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            EditorUtility.DisplayDialog("No Camera", "No Main Camera found in scene.", "OK");
            return;
        }

        Debug.Log("=== Step 4: Setup Broadcast Camera ===");

        Undo.RecordObject(mainCam.transform, "Setup Broadcast Camera");
        Undo.RecordObject(mainCam, "Setup Broadcast Camera");

        // Switch to perspective
        mainCam.orthographic = false;
        mainCam.fieldOfView = 60f;

        // Position camera for broadcast view:
        // - X = 0 (center ice, will be moved by PuckFollowCamera at runtime)
        // - Y = -25 (south side of rink, looking north across the ice)
        // - Z = -30 (elevated above the ice plane)
        mainCam.transform.position = new Vector3(0f, -25f, -30f);

        // Look at center ice
        mainCam.transform.LookAt(Vector3.zero, Vector3.forward);

        // Configure PuckFollowCamera for broadcast
        PuckFollowCamera followCam = mainCam.GetComponent<PuckFollowCamera>();
        if (followCam != null)
        {
            Undo.RecordObject(followCam, "Configure Broadcast Camera");
            followCam.cameraMode = PuckFollowCamera.CameraMode.Broadcast;
            followCam.broadcastHeight = 30f;
            followCam.broadcastSideOffset = -25f;
            followCam.broadcastFOV = 60f;
            followCam.broadcastFollowX = 0.6f;
            followCam.broadcastFollowZ = 0.2f;
            followCam.followSpeed = 8f;
        }

        MarkSceneDirty();

        Debug.Log($"Camera: perspective, FOV={mainCam.fieldOfView}");
        Debug.Log($"Position: {mainCam.transform.position}");
        Debug.Log($"Looking at: center ice (0, 0, 0)");
        Debug.Log("");
        Debug.Log("TUNING (in PuckFollowCamera Inspector):");
        Debug.Log("- broadcastHeight: raise for more top-down, lower for more edge-on");
        Debug.Log("- broadcastSideOffset: more negative = further from ice, sees more rink");
        Debug.Log("- broadcastFOV: increase for wider view");
        Debug.Log("- broadcastFollowX/Y: how tightly camera tracks the puck");

        EditorUtility.DisplayDialog("Broadcast Camera Ready",
            "Camera configured for broadcast view.\n\n" +
            "Hit Play to test. Tweak values in the\n" +
            "PuckFollowCamera Inspector if needed.\n\n" +
            "Key settings:\n" +
            "- Broadcast Height: 30 (elevation)\n" +
            "- Broadcast Side Offset: -25 (camera side)\n" +
            "- Broadcast FOV: 60 (field of view)",
            "OK");
    }

    // ================================================================
    // UTILITY: Print current bounds for debugging
    // ================================================================

    [MenuItem("Tools/Ice Legends/Arena Setup/Debug - Print Rink Bounds")]
    public static void PrintRinkBoundsInfo()
    {
        GameObject rink = FindRinkInScene();
        if (rink == null) { Debug.LogWarning("No rink found in scene."); return; }

        Bounds worldBounds = CalculateWorldBounds(rink);

        Debug.Log("=== Rink World Bounds ===");
        Debug.Log($"Transform: pos={rink.transform.position}, rot={rink.transform.rotation.eulerAngles}, scale={rink.transform.localScale}");
        Debug.Log($"World center: {worldBounds.center}");
        Debug.Log($"World size: {worldBounds.size}");
        Debug.Log($"World min: {worldBounds.min}");
        Debug.Log($"World max: {worldBounds.max}");
        Debug.Log($"X span: {worldBounds.size.x:F1} (target: {GAMEPLAY_RINK_LENGTH})");
        Debug.Log($"Y span: {worldBounds.size.y:F1} (target: {GAMEPLAY_RINK_WIDTH})");

        // Find and report goal positions
        GameObject goalsContainer = GameObject.Find("Goals");
        if (goalsContainer != null)
        {
            Debug.Log("");
            Debug.Log("Current goal positions:");
            foreach (Transform child in goalsContainer.transform)
            {
                Debug.Log($"  {child.name}: {child.position}");
            }
        }
    }

    [MenuItem("Tools/Ice Legends/Arena Setup/Debug - Print Raw Mesh Bounds")]
    public static void PrintRawMeshBounds()
    {
        GameObject rink = FindRinkInScene();
        if (rink == null) { Debug.LogWarning("No rink found in scene."); return; }

        Bounds rawBounds = CalculateRawMeshBounds(rink);
        Debug.Log("=== Raw Mesh Bounds (identity transform) ===");
        Debug.Log($"Size: {rawBounds.size}");
        Debug.Log($"Center: {rawBounds.center}");
        Debug.Log($"X: {rawBounds.size.x:F2}, Y: {rawBounds.size.y:F2}, Z: {rawBounds.size.z:F2}");
        Debug.Log($"Aspect (X/Z): {rawBounds.size.x / rawBounds.size.z:F2}");
        Debug.Log($"Aspect (Z/X): {rawBounds.size.z / rawBounds.size.x:F2}");

        // List all child mesh names for reference
        MeshFilter[] meshFilters = rink.GetComponentsInChildren<MeshFilter>();
        Debug.Log($"");
        Debug.Log($"Mesh children ({meshFilters.Length} total):");
        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh != null)
            {
                Bounds mb = mf.sharedMesh.bounds;
                Debug.Log($"  {mf.gameObject.name}: mesh={mf.sharedMesh.name}, bounds size={mb.size}");
            }
        }
    }

    // ================================================================
    // Fine Tune Window
    // ================================================================

    [MenuItem("Tools/Ice Legends/Arena Setup/Fine Tune Scale (Window)")]
    public static void FineTuneScale()
    {
        GameObject rink = FindRinkInScene();
        ArenaScaleTuner.ShowWindow(rink);
    }

    // ================================================================
    // Helper Methods
    // ================================================================

    private static void RepositionPlayersToRink(Bounds rinkBounds)
    {
        // Find all players and move them proportionally into the rink
        // Current layout assumes gameplay centered at (0,0) spanning 67x28.5
        // New layout centers at rink center

        float scaleX = rinkBounds.size.x / GAMEPLAY_RINK_LENGTH;
        float scaleY = rinkBounds.size.y / GAMEPLAY_RINK_WIDTH;
        Vector2 offset = new Vector2(rinkBounds.center.x, rinkBounds.center.y);

        int playersRepositioned = 0;

        // Reposition all players (teammates, opponents, and the human player)
        TeammateController[] teammates = FindObjectsByType<TeammateController>(FindObjectsSortMode.None);
        foreach (TeammateController tc in teammates)
        {
            RepositionPlayer(tc.transform, scaleX, scaleY, offset);
            playersRepositioned++;
        }

        AIController[] opponents = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        foreach (AIController ai in opponents)
        {
            RepositionPlayer(ai.transform, scaleX, scaleY, offset);
            playersRepositioned++;
        }

        // Find the human-controlled player (has PlayerController but not TeammateController)
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (PlayerController pc in players)
        {
            if (pc.GetComponent<TeammateController>() == null && pc.GetComponent<AIController>() == null)
            {
                RepositionPlayer(pc.transform, scaleX, scaleY, offset);
                playersRepositioned++;
            }
        }

        // Reposition puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            Undo.RecordObject(puck.transform, "Reposition Puck");
            puck.transform.position = new Vector3(rinkBounds.center.x, rinkBounds.center.y, 0f);
            Debug.Log($"Puck moved to rink center: ({rinkBounds.center.x:F1}, {rinkBounds.center.y:F1})");
        }

        Debug.Log($"Repositioned {playersRepositioned} players to match rink bounds");
    }

    private static void RepositionPlayer(Transform playerTransform, float scaleX, float scaleY, Vector2 offset)
    {
        Undo.RecordObject(playerTransform, "Reposition Player");
        Vector3 pos = playerTransform.position;
        // Scale the player's position proportionally and offset to rink center
        pos.x = pos.x * scaleX + offset.x;
        pos.y = pos.y * scaleY + offset.y;
        playerTransform.position = pos;
    }

    private static GameObject FindRinkInScene()
    {
        string[] searchNames = new[]
        {
            "hockey_arena_rink",
            "hockey_arena_rink(Clone)",
            "HockeyArenaRink",
            "Rink",
            "3DRink",
            "Arena_Rink"
        };

        foreach (string name in searchNames)
        {
            GameObject found = GameObject.Find(name);
            if (found != null) return found;
        }

        // Search under ENVIRONMENT
        GameObject environment = GameObject.Find("--- ENVIRONMENT ---");
        if (environment != null)
        {
            foreach (Transform child in environment.transform)
            {
                string lower = child.name.ToLower();
                if (lower.Contains("rink") || lower.Contains("arena"))
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Get mesh bounds at identity transform (raw model dimensions)
    /// </summary>
    private static Bounds CalculateRawMeshBounds(GameObject root)
    {
        // Save current transform
        Vector3 origPos = root.transform.position;
        Quaternion origRot = root.transform.rotation;
        Vector3 origScale = root.transform.localScale;

        // Reset to identity
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Bounds bounds = CalculateWorldBounds(root);

        // Restore transform
        root.transform.position = origPos;
        root.transform.rotation = origRot;
        root.transform.localScale = origScale;

        return bounds;
    }

    /// <summary>
    /// Get world-space bounds with current transform
    /// </summary>
    private static Bounds CalculateWorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds();

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }
        return combinedBounds;
    }

    private static void MarkSceneDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    private static void ShowRinkNotFound()
    {
        EditorUtility.DisplayDialog("Rink Not Found",
            "Could not find the 3D rink in the scene.\n\n" +
            "Searched for: hockey_arena_rink, Arena_Rink, or anything\n" +
            "containing 'rink'/'arena' under --- ENVIRONMENT ---.\n\n" +
            "Make sure the rink prefab is in the scene.",
            "OK");
    }

    private static void ShowNoMeshes()
    {
        EditorUtility.DisplayDialog("No Meshes",
            "No MeshRenderers found on the rink object.", "OK");
    }
}

/// <summary>
/// Editor window for interactive rink scale/position fine-tuning
/// </summary>
public class ArenaScaleTuner : EditorWindow
{
    private GameObject rink;
    private float scaleMultiplier = 1f;
    private Vector3 positionOffset = Vector3.zero;
    private float rotationX = -90f;
    private float rotationY = 0f;

    public static void ShowWindow(GameObject rinkObj)
    {
        ArenaScaleTuner window = GetWindow<ArenaScaleTuner>("Arena Scale Tuner");
        window.rink = rinkObj;
        window.minSize = new Vector2(380, 400);

        if (rinkObj != null)
        {
            window.rotationX = rinkObj.transform.rotation.eulerAngles.x;
            window.rotationY = rinkObj.transform.rotation.eulerAngles.y;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Arena Rink Fine Tuning", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        rink = (GameObject)EditorGUILayout.ObjectField("Rink Object", rink, typeof(GameObject), true);

        if (rink == null)
        {
            EditorGUILayout.HelpBox("Drag the rink GameObject here from the Hierarchy.", MessageType.Warning);
            return;
        }

        // Current state
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Current Transform", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Position: {rink.transform.position}");
        EditorGUILayout.LabelField($"Rotation: {rink.transform.rotation.eulerAngles}");
        EditorGUILayout.LabelField($"Scale: {rink.transform.localScale}");

        // World bounds
        Renderer[] renderers = rink.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("World Bounds", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"X: {b.min.x:F1} to {b.max.x:F1} (span: {b.size.x:F1}, target: 67)");
            EditorGUILayout.LabelField($"Y: {b.min.y:F1} to {b.max.y:F1} (span: {b.size.y:F1}, target: 28.5)");
            EditorGUILayout.LabelField($"Z: {b.min.z:F1} to {b.max.z:F1} (span: {b.size.z:F1})");
        }

        // Controls
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Adjustments", EditorStyles.boldLabel);

        rotationX = EditorGUILayout.FloatField("X Rotation", rotationX);
        rotationY = EditorGUILayout.FloatField("Y Rotation", rotationY);
        scaleMultiplier = EditorGUILayout.Slider("Scale Multiplier", scaleMultiplier, 0.1f, 5f);
        positionOffset = EditorGUILayout.Vector3Field("Position Offset", positionOffset);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Rotation"))
        {
            Undo.RecordObject(rink.transform, "Set Rink Rotation");
            rink.transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
            MarkDirty();
        }
        if (GUILayout.Button("Apply Scale x"))
        {
            Undo.RecordObject(rink.transform, "Scale Rink");
            rink.transform.localScale *= scaleMultiplier;
            scaleMultiplier = 1f;
            MarkDirty();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Offset"))
        {
            Undo.RecordObject(rink.transform, "Offset Rink");
            rink.transform.position += positionOffset;
            positionOffset = Vector3.zero;
            MarkDirty();
        }
        if (GUILayout.Button("Center at Origin"))
        {
            Undo.RecordObject(rink.transform, "Center Rink");
            rink.transform.position = Vector3.zero;
            MarkDirty();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Workflow:\n" +
            "1. Use Step 1 (Scale Rink to Gameplay) first\n" +
            "2. Check World Bounds above - X should be ~67, Y should be ~28.5\n" +
            "3. If axes are swapped, change Y Rotation to 90 and click Set Rotation\n" +
            "4. If too small/big, adjust Scale Multiplier\n" +
            "5. If offset from center, use Position Offset or Center at Origin",
            MessageType.Info);
    }

    private void MarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Repaint();
    }
}

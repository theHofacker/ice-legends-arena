using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Sets up a new 2.5D isometric scene for Ice Legends Arena.
/// Creates camera, lighting, and basic rink structure.
/// </summary>
public class Scene25DSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Create 2.5D Scene Setup")]
    public static void Create25DScene()
    {
        // Save current scene first
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            // Create new scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Debug.Log("Creating 2.5D Ice Legends Arena scene...");

            // Set up camera
            SetupIsometricCamera();

            // Set up lighting
            SetupLighting();

            // Create scene structure
            CreateSceneStructure();

            // Save the new scene
            string scenePath = "Assets/Scenes/Gameplay2.5D.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);

            Debug.Log($"✅ 2.5D scene created at: {scenePath}");
            Debug.Log("Next steps:");
            Debug.Log("1. Add character sprites");
            Debug.Log("2. Port puck physics from PhysicsSandbox");
            Debug.Log("3. Create isometric ice rink sprite");
        }
    }

    private static void SetupIsometricCamera()
    {
        // Find or create camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        // Configure for 2.5D isometric
        mainCam.orthographic = true;
        mainCam.orthographicSize = 15f; // Adjust based on testing (shows ~60-70% of rink)
        mainCam.transform.position = new Vector3(0, 0, -10);
        mainCam.transform.rotation = Quaternion.identity;

        // Optional: Slight tilt for depth (isometric-like feel)
        // Uncomment if you want angled camera:
        // mainCam.transform.rotation = Quaternion.Euler(30, 0, 0);
        // mainCam.transform.position = new Vector3(0, 20, -20);

        mainCam.backgroundColor = new Color(0.1f, 0.1f, 0.15f); // Dark blue background

        Debug.Log("✅ Isometric camera configured");
    }

    private static void SetupLighting()
    {
        // Find or create directional light
        Light dirLight = Object.FindObjectOfType<Light>();
        if (dirLight == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
        }

        // Set up lighting for 2.5D
        dirLight.transform.rotation = Quaternion.Euler(50, -30, 0);
        dirLight.intensity = 1.0f;
        dirLight.color = Color.white;
        dirLight.shadows = LightShadows.Soft; // Enable shadows for depth

        Debug.Log("✅ Lighting configured");
    }

    private static void CreateSceneStructure()
    {
        // Create organized hierarchy
        GameObject environment = new GameObject("--- ENVIRONMENT ---");
        GameObject gameplay = new GameObject("--- GAMEPLAY ---");
        GameObject ui = new GameObject("--- UI ---");
        GameObject managers = new GameObject("--- MANAGERS ---");

        // Create essential gameplay objects
        GameObject rink = new GameObject("IceRink");
        rink.transform.SetParent(environment.transform);

        GameObject players = new GameObject("Players");
        players.transform.SetParent(gameplay.transform);

        GameObject puck = new GameObject("Puck_Placeholder");
        puck.transform.SetParent(gameplay.transform);

        // Add placeholder components
        AddPlaceholderComponents(rink, players, puck);

        Debug.Log("✅ Scene structure created");
        Debug.Log("Hierarchy:");
        Debug.Log("  - ENVIRONMENT (IceRink)");
        Debug.Log("  - GAMEPLAY (Players, Puck)");
        Debug.Log("  - UI (empty - ready for HUD)");
        Debug.Log("  - MANAGERS (empty - ready for GameManager)");
    }

    private static void AddPlaceholderComponents(GameObject rink, GameObject players, GameObject puck)
    {
        // Add note components to guide development
        var rinkNote = rink.AddComponent<SceneNote>();
        rinkNote.note = "Add isometric ice rink sprite here.\n" +
                        "Create sprite with perspective/depth.\n" +
                        "Reference: GoalBattle screenshots for style.";

        var playersNote = players.AddComponent<SceneNote>();
        playersNote.note = "3v3 Setup:\n" +
                          "- Create 3 player characters\n" +
                          "- Add Rigidbody2D + CircleCollider2D\n" +
                          "- Attach PlayerController script\n" +
                          "- Generate sprites with Midjourney/Blender";

        var puckNote = puck.AddComponent<SceneNote>();
        puckNote.note = "Port from PhysicsSandbox scene:\n" +
                       "- Copy Puck prefab\n" +
                       "- Rigidbody2D settings\n" +
                       "- PuckPhysicsMaterial\n" +
                       "- CircleCollider2D (radius 0.25)";
    }

    // Helper component to leave notes in scene
    public class SceneNote : MonoBehaviour
    {
        [TextArea(3, 10)]
        public string note = "Development note here...";
    }
#endif
}

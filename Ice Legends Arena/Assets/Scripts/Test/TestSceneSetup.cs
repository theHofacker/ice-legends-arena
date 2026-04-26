using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Creates a minimal test scene for debugging basic player + puck mechanics.
/// Menu: Ice Legends > Create Test Scene
/// </summary>
public class TestSceneSetup : MonoBehaviour
{
    [MenuItem("Ice Legends/Create Test Scene")]
    public static void CreateTestScene()
    {
        // Create new scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Remove default directional light (we'll add our own)
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in lights) DestroyImmediate(l.gameObject);

        // === LIGHTING ===
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.95f, 0.9f);
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // === CAMERA ===
        // Remove default camera
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera c in cams) DestroyImmediate(c.gameObject);

        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        Camera cam = camObj.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;

        // Broadcast camera: behind and above, looking toward +Z (toward far goal)
        // Position at negative Z, elevated, looking forward
        camObj.transform.position = new Vector3(0f, 20f, -22f);
        camObj.transform.LookAt(new Vector3(0f, 0f, 5f), Vector3.up);

        // === RINK ===
        // Try to find and instantiate the hockey arena rink prefab
        string rinkPrefabPath = "Assets/Hockey arena - sport pavilion/Models/Prefabs/hockey_arena_rink.prefab";
        GameObject rinkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rinkPrefabPath);

        if (rinkPrefab != null)
        {
            GameObject rink = PrefabUtility.InstantiatePrefab(rinkPrefab) as GameObject;
            rink.name = "hockey_arena_rink";
            // NO rotation - use natural orientation (goals at Z ±23.7)
            rink.transform.position = Vector3.zero;
            rink.transform.rotation = Quaternion.identity;
            rink.transform.localScale = Vector3.one;

            // Tag the goals
            Transform goal1 = rink.transform.Find("hockey_arena_goal");
            Transform goal2 = rink.transform.Find("hockey_arena_goal (1)");

            if (goal1 != null) goal1.gameObject.tag = "Goal";
            if (goal2 != null) goal2.gameObject.tag = "Goal";

            Debug.Log($"Rink placed. Goal positions: {goal1?.position}, {goal2?.position}");
        }
        else
        {
            Debug.LogWarning($"Rink prefab not found at {rinkPrefabPath}. Creating placeholder floor.");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "IcePlaceholder";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(3f, 1f, 6f); // ~30x60 units
            floor.GetComponent<Renderer>().material.color = new Color(0.85f, 0.9f, 0.95f);
        }

        // === PLAYER ===
        GameObject player = new GameObject("TestPlayer");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 0f, -5f); // Slightly behind center ice

        // Add capsule collider (visual placeholder)
        CapsuleCollider capsule = player.AddComponent<CapsuleCollider>();
        capsule.radius = 0.3f;
        capsule.height = 1.0f;
        capsule.center = new Vector3(0f, 0.5f, 0f);

        // Add rigidbody
        player.AddComponent<Rigidbody>();

        // Add test controller
        player.AddComponent<TestPlayerController>();

        // Add a visible mesh child (capsule)
        GameObject playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerVisual.name = "PlayerModel";
        playerVisual.transform.SetParent(player.transform);
        playerVisual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        playerVisual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
        DestroyImmediate(playerVisual.GetComponent<Collider>()); // Remove duplicate collider
        playerVisual.GetComponent<Renderer>().material.color = Color.cyan;

        // Add a forward indicator (so we can see which way they face)
        GameObject fwdIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fwdIndicator.name = "ForwardIndicator";
        fwdIndicator.transform.SetParent(player.transform);
        fwdIndicator.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
        fwdIndicator.transform.localScale = new Vector3(0.1f, 0.1f, 0.3f);
        DestroyImmediate(fwdIndicator.GetComponent<Collider>());
        fwdIndicator.GetComponent<Renderer>().material.color = Color.blue;

        // === PUCK ===
        GameObject puck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        puck.name = "Puck";
        puck.tag = "Puck";
        puck.transform.position = new Vector3(0f, 0.05f, 0f); // Center ice
        puck.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f); // Flat disc

        // Replace MeshCollider with SphereCollider
        DestroyImmediate(puck.GetComponent<Collider>());
        SphereCollider puckSphere = puck.AddComponent<SphereCollider>();
        puckSphere.radius = 0.5f;

        puck.AddComponent<Rigidbody>();
        puck.AddComponent<TestPuckController>();
        puck.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.15f);

        // === INPUT MANAGER ===
        // Check if InputManager prefab exists (from PersistentManagers)
        GameObject inputMgr = new GameObject("InputManager");
        InputManager im = inputMgr.AddComponent<InputManager>();
        Debug.Log("Added InputManager. Note: You may need to assign the Input Actions Asset in the Inspector.");

        // === SAVE ===
        string scenePath = "Assets/Scenes/TestMovement.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"Test scene saved to {scenePath}");

        Debug.Log("=== TEST SCENE CREATED ===");
        Debug.Log("Controls:");
        Debug.Log("  WASD = move player");
        Debug.Log("  Space = shoot puck");
        Debug.Log("  Player is CYAN capsule, forward = BLUE indicator");
        Debug.Log("  Puck is BLACK disc at center ice");
        Debug.Log("");
        Debug.Log("If WASD doesn't work, assign Input Actions Asset on InputManager.");
        Debug.Log("If directions feel wrong, toggle logInput on TestPlayerController.");
    }
}
#endif

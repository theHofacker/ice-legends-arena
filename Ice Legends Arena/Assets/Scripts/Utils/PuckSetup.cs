using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Creates a hockey puck with proper physics settings.
/// </summary>
public class PuckSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Create Hockey Puck")]
    public static void CreatePuck()
    {
        // Puck dimensions (scaled up for better visibility and collision)
        // Real puck: 76mm diameter, but scaling up for gameplay
        float puckDiameter = 0.5f;    // Scaled up for better visibility (0.5m in Unity)
        float puckRadius = puckDiameter / 2f;

        // Create puck GameObject
        GameObject puck = new GameObject("Puck");
        puck.tag = "Puck";  // Important for goal detection
        puck.transform.position = Vector3.zero;

        // Add sprite renderer for visualization
        SpriteRenderer spriteRenderer = puck.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        spriteRenderer.color = new Color(0.1f, 0.1f, 0.1f, 1f); // Dark gray/black
        spriteRenderer.sortingOrder = 5; // Render above ice
        puck.transform.localScale = new Vector3(puckDiameter * 5, puckDiameter * 5, 1); // Scale the knob sprite

        // Add Rigidbody2D for physics
        Rigidbody2D rb = puck.AddComponent<Rigidbody2D>();
        rb.mass = 1f;                 // Adjusted for scaled-up puck
        rb.linearDamping = 0.8f;      // More drag to slow it down naturally
        rb.angularDamping = 0.5f;     // Angular drag
        rb.gravityScale = 0;          // No gravity in top-down view
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Prevents tunneling through walls
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Puck doesn't visibly rotate in 2D
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep; // Prevents getting stuck

        // Add CircleCollider2D
        CircleCollider2D collider = puck.AddComponent<CircleCollider2D>();
        collider.radius = puckRadius; // Match the diameter

        // Create and assign physics material
        PhysicsMaterial2D puckMaterial = new PhysicsMaterial2D("PuckPhysics");
        puckMaterial.friction = 0.1f;   // Low friction (slides on ice)
        puckMaterial.bounciness = 0.6f; // Some bounce off boards/players

        // Save physics material
        string materialPath = "Assets/Physics/PuckPhysicsMaterial.physicsMaterial2D";
        string directory = System.IO.Path.GetDirectoryName(materialPath);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        AssetDatabase.CreateAsset(puckMaterial, materialPath);

        collider.sharedMaterial = puckMaterial;

        // Make it a prefab
        string prefabPath = "Assets/Prefabs/Puck.prefab";
        string prefabDirectory = System.IO.Path.GetDirectoryName(prefabPath);
        if (!System.IO.Directory.Exists(prefabDirectory))
        {
            System.IO.Directory.CreateDirectory(prefabDirectory);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(puck, prefabPath);

        // Select the created puck in hierarchy
        Selection.activeGameObject = puck;

        Debug.Log("Hockey puck created successfully!");
        Debug.Log($"Puck specs: {puckDiameter}m diameter, {rb.mass}kg mass");
        Debug.Log("Don't forget to create the 'Puck' tag in the Tag Manager if it doesn't exist!");

        AssetDatabase.Refresh();
    }
#endif
}

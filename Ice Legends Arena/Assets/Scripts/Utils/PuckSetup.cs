using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Creates a hockey puck with proper 3D physics settings.
/// </summary>
public class PuckSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Create Hockey Puck")]
    public static void CreatePuck()
    {
        float puckDiameter = 0.5f;
        float puckRadius = puckDiameter / 2f;
        float puckHeight = 0.1f;

        // Create puck GameObject
        GameObject puck = new GameObject("Puck");
        puck.tag = "Puck";
        puck.transform.position = new Vector3(0f, puckHeight / 2f, 0f); // Slightly above ice

        // Add a cylinder mesh for 3D visualization (or keep sprite for now)
        // TODO: Replace with proper 3D puck mesh
        SpriteRenderer spriteRenderer = puck.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        spriteRenderer.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        puck.transform.localScale = new Vector3(puckDiameter * 5, puckDiameter * 5, 1);

        // Add Rigidbody for 3D physics
        Rigidbody rb = puck.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0.8f;
        rb.angularDamping = 0.5f;
        rb.useGravity = true; // Puck uses gravity for saucer passes and roof shots
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Add SphereCollider (or CapsuleCollider for puck shape)
        SphereCollider collider = puck.AddComponent<SphereCollider>();
        collider.radius = puckRadius;

        // Create and assign physics material
        PhysicsMaterial puckMaterial = new PhysicsMaterial("PuckPhysics");
        puckMaterial.dynamicFriction = 0.1f;
        puckMaterial.staticFriction = 0.1f;
        puckMaterial.bounciness = 0.6f;
        puckMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        puckMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;

        // Save physics material
        string materialPath = "Assets/Physics/PuckPhysicsMaterial.physicMaterial";
        string directory = System.IO.Path.GetDirectoryName(materialPath);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        AssetDatabase.CreateAsset(puckMaterial, materialPath);

        collider.sharedMaterial = puckMaterial;

        // Add PuckController
        puck.AddComponent<PuckController>();

        // Make it a prefab
        string prefabPath = "Assets/Prefabs/Puck.prefab";
        string prefabDirectory = System.IO.Path.GetDirectoryName(prefabPath);
        if (!System.IO.Directory.Exists(prefabDirectory))
        {
            System.IO.Directory.CreateDirectory(prefabDirectory);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(puck, prefabPath);

        Selection.activeGameObject = puck;

        Debug.Log("Hockey puck created successfully!");
        Debug.Log($"Puck specs: {puckDiameter}m diameter, {rb.mass}kg mass, 3D physics with gravity");

        AssetDatabase.Refresh();
    }
#endif
}

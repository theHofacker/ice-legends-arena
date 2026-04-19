using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sets up the complete ice rink boundary with curved ends.
/// Creates both visual borders and 3D physics colliders on the XZ plane.
/// </summary>
public class RinkBoundarySetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Setup Complete Rink Boundary")]
    public static void SetupRinkBoundary()
    {
        // International rink dimensions: 61m x 30m
        float rinkLength = 61f;     // X dimension
        float rinkWidth = 30f;      // Z dimension
        float cornerRadius = 8.5f;
        float wallThickness = 0.5f;
        float wallHeight = 2f;
        int curveSegments = 16;

        // Find or create RinkBoundary
        GameObject rinkBoundary = GameObject.Find("RinkBoundary");
        if (rinkBoundary == null)
        {
            rinkBoundary = new GameObject("RinkBoundary");
        }

        // Clear existing children
        while (rinkBoundary.transform.childCount > 0)
        {
            DestroyImmediate(rinkBoundary.transform.GetChild(0).gameObject);
        }

        // Generate rink outline points on XZ plane
        Vector3[] outlinePoints = GenerateRinkOutline(rinkLength, rinkWidth, cornerRadius, curveSegments);

        // Create BoxCollider segments along the outline
        CreateBoundaryColliders(rinkBoundary, outlinePoints, wallThickness, wallHeight);

        // Create visual walls using LineRenderers
        CreateVisualWalls(rinkBoundary, outlinePoints, wallThickness);

        Debug.Log($"Rink boundary created! Dimensions: {rinkLength}m x {rinkWidth}m (XZ plane)");
        Debug.Log($"Corner radius: {cornerRadius}m, Wall height: {wallHeight}m");
    }

    private static void CreateBoundaryColliders(GameObject parent, Vector3[] points, float thickness, float height)
    {
        // Create physics material for boards
        PhysicsMaterial boardMaterial = new PhysicsMaterial("BoardPhysics");
        boardMaterial.dynamicFriction = 0.3f;
        boardMaterial.staticFriction = 0.4f;
        boardMaterial.bounciness = 0.5f;
        boardMaterial.frictionCombine = PhysicsMaterialCombine.Average;
        boardMaterial.bounceCombine = PhysicsMaterialCombine.Average;

        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 start = points[i];
            Vector3 end = points[i + 1];
            Vector3 midpoint = (start + end) / 2f;
            midpoint.y = height / 2f; // Center vertically

            float segmentLength = Vector3.Distance(start, end);
            if (segmentLength < 0.01f) continue;

            Vector3 direction = (end - start).normalized;

            GameObject segment = new GameObject($"Wall_{i:D3}");
            segment.transform.SetParent(parent.transform);
            segment.transform.position = midpoint;
            segment.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            // Add collider
            BoxCollider collider = segment.AddComponent<BoxCollider>();
            collider.size = new Vector3(thickness, height, segmentLength);
            collider.sharedMaterial = boardMaterial;

            // Add kinematic rigidbody
            Rigidbody rb = segment.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        Debug.Log($"Created {points.Length - 1} wall collider segments");
    }

    private static void CreateVisualWalls(GameObject parent, Vector3[] points, float thickness)
    {
        GameObject visualWalls = new GameObject("VisualWalls");
        visualWalls.transform.SetParent(parent.transform);
        visualWalls.transform.localPosition = Vector3.zero;

        LineRenderer line = visualWalls.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.startWidth = thickness;
        line.endWidth = thickness;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.numCapVertices = 5;
    }

    /// <summary>
    /// Generate rink outline as Vector3 points on the XZ plane (Y=0).
    /// </summary>
    private static Vector3[] GenerateRinkOutline(float length, float width, float radius, int segments)
    {
        float halfLength = length / 2f;  // X
        float halfWidth = width / 2f;    // Z

        System.Collections.Generic.List<Vector3> pointsList = new System.Collections.Generic.List<Vector3>();

        // Top-right corner (0 to 90 degrees)
        Vector2 trCenter = new Vector2(halfLength - radius, halfWidth - radius);
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(0f, 90f, t) * Mathf.Deg2Rad;
            pointsList.Add(new Vector3(trCenter.x + radius * Mathf.Cos(angle), 0f, trCenter.y + radius * Mathf.Sin(angle)));
        }

        // Top edge
        pointsList.Add(new Vector3(-halfLength + radius, 0f, halfWidth));

        // Top-left corner (90 to 180)
        Vector2 tlCenter = new Vector2(-halfLength + radius, halfWidth - radius);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(90f, 180f, t) * Mathf.Deg2Rad;
            pointsList.Add(new Vector3(tlCenter.x + radius * Mathf.Cos(angle), 0f, tlCenter.y + radius * Mathf.Sin(angle)));
        }

        // Left edge
        pointsList.Add(new Vector3(-halfLength, 0f, -halfWidth + radius));

        // Bottom-left corner (180 to 270)
        Vector2 blCenter = new Vector2(-halfLength + radius, -halfWidth + radius);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(180f, 270f, t) * Mathf.Deg2Rad;
            pointsList.Add(new Vector3(blCenter.x + radius * Mathf.Cos(angle), 0f, blCenter.y + radius * Mathf.Sin(angle)));
        }

        // Bottom edge
        pointsList.Add(new Vector3(halfLength - radius, 0f, -halfWidth));

        // Bottom-right corner (270 to 360)
        Vector2 brCenter = new Vector2(halfLength - radius, -halfWidth + radius);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(270f, 360f, t) * Mathf.Deg2Rad;
            pointsList.Add(new Vector3(brCenter.x + radius * Mathf.Cos(angle), 0f, brCenter.y + radius * Mathf.Sin(angle)));
        }

        // Close the loop
        pointsList.Add(new Vector3(halfLength, 0f, halfWidth - radius));

        Debug.Log($"Generated {pointsList.Count} points for rink boundary on XZ plane");
        return pointsList.ToArray();
    }
#endif
}

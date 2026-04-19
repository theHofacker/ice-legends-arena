using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generates box colliders for ice rink walls with rounded ends.
/// Creates realistic hockey rink boundaries that match the ice surface shape.
/// Converted to 3D physics (X = rink length, Z = rink width, Y = height).
/// EdgeCollider2D replaced with BoxCollider segments.
/// </summary>
public class RinkWallGenerator : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Generate Rink Walls")]
    public static void GenerateRinkWalls()
    {
        // Find or create the RinkBoundary parent
        GameObject rinkBoundary = GameObject.Find("RinkBoundary");
        if (rinkBoundary == null)
        {
            rinkBoundary = new GameObject("RinkBoundary");
            Debug.Log("Created RinkBoundary GameObject");
        }

        // Rink dimensions (matching your current setup)
        // Old 2D: rinkWidth=67 (X), rinkHeight=28.5 (Y)
        // New 3D: rinkLength=67 (X), rinkWidth=28.5 (Z)
        float rinkLength = 67f;     // X-axis (was rinkWidth in 2D)
        float rinkWidth = 28.5f;    // Z-axis (was rinkHeight in 2D)
        float cornerRadius = 14.25f; // Radius for rounded ends
        int cornerSegments = 16;     // Smoothness of curves
        float wallHeight = 2f;       // Y-axis height of walls
        float wallThickness = 0.5f;  // Thickness of wall colliders

        // Clear existing walls
        foreach (Transform child in rinkBoundary.transform)
        {
            if (child.name.StartsWith("Wall"))
            {
                DestroyImmediate(child.gameObject);
            }
        }

        // Create the four wall segments with box colliders
        // North/South are the rounded ends (along X), East/West are straight sides (along Z)
        CreateNorthWall(rinkBoundary.transform, rinkLength, rinkWidth, cornerRadius, cornerSegments, wallHeight, wallThickness);
        CreateSouthWall(rinkBoundary.transform, rinkLength, rinkWidth, cornerRadius, cornerSegments, wallHeight, wallThickness);
        CreateEastWall(rinkBoundary.transform, rinkLength, rinkWidth, cornerRadius, cornerSegments, wallHeight, wallThickness);
        CreateWestWall(rinkBoundary.transform, rinkLength, rinkWidth, cornerRadius, cornerSegments, wallHeight, wallThickness);

        Debug.Log("Rink walls generated successfully!");
    }

    private static void CreateNorthWall(Transform parent, float length, float width, float radius, int segments,
        float wallHeight, float wallThickness)
    {
        GameObject wall = new GameObject("WallNorth");
        wall.transform.SetParent(parent);
        wall.transform.localPosition = Vector3.zero;

        // North wall = positive Z rounded end
        // Arc center at (length/2 - radius, width/2) on XZ, sweeping from 0 to 180 degrees
        // In 2D this was an arc at (width/2 - radius, height/2) on XY
        Vector3[] arcPoints = GenerateArc3D(length / 2f - radius, width / 2f, radius, 0f, 180f, segments);
        CreateWallSegmentsFromPoints(wall.transform, arcPoints, wallHeight, wallThickness);
    }

    private static void CreateSouthWall(Transform parent, float length, float width, float radius, int segments,
        float wallHeight, float wallThickness)
    {
        GameObject wall = new GameObject("WallSouth");
        wall.transform.SetParent(parent);
        wall.transform.localPosition = Vector3.zero;

        // South wall = negative Z rounded end
        // Arc center at (length/2 - radius, -width/2) on XZ, sweeping from 180 to 360 degrees
        Vector3[] arcPoints = GenerateArc3D(length / 2f - radius, -width / 2f, radius, 180f, 360f, segments);
        CreateWallSegmentsFromPoints(wall.transform, arcPoints, wallHeight, wallThickness);
    }

    private static void CreateEastWall(Transform parent, float length, float width, float radius, int segments,
        float wallHeight, float wallThickness)
    {
        GameObject wall = new GameObject("WallEast");
        wall.transform.SetParent(parent);
        wall.transform.localPosition = Vector3.zero;

        // East wall = positive X straight section
        float straightWidth = width - (radius * 2);
        Vector3 start = new Vector3(length / 2f, wallHeight / 2f, straightWidth / 2f);
        Vector3 end = new Vector3(length / 2f, wallHeight / 2f, -straightWidth / 2f);

        CreateSingleWallSegment(wall.transform, "EastSegment", start, end, wallHeight, wallThickness);
    }

    private static void CreateWestWall(Transform parent, float length, float width, float radius, int segments,
        float wallHeight, float wallThickness)
    {
        GameObject wall = new GameObject("WallWest");
        wall.transform.SetParent(parent);
        wall.transform.localPosition = Vector3.zero;

        // West wall = negative X straight section
        float straightWidth = width - (radius * 2);
        Vector3 start = new Vector3(-length / 2f, wallHeight / 2f, -straightWidth / 2f);
        Vector3 end = new Vector3(-length / 2f, wallHeight / 2f, straightWidth / 2f);

        CreateSingleWallSegment(wall.transform, "WestSegment", start, end, wallHeight, wallThickness);
    }

    /// <summary>
    /// Generate arc points on the XZ plane (Y=wallHeight/2 for vertical centering).
    /// Maps from old 2D XY arc to 3D XZ arc.
    /// </summary>
    private static Vector3[] GenerateArc3D(float centerX, float centerZ, float radius, float startAngle, float endAngle, int segments)
    {
        Vector3[] points = new Vector3[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments);
            float radians = angle * Mathf.Deg2Rad;

            float x = centerX + radius * Mathf.Cos(radians);
            float z = centerZ + radius * Mathf.Sin(radians);

            points[i] = new Vector3(x, 0, z);
        }

        return points;
    }

    /// <summary>
    /// Create box collider segments between consecutive points to approximate the arc.
    /// Replaces EdgeCollider2D which has no 3D equivalent.
    /// </summary>
    private static void CreateWallSegmentsFromPoints(Transform parent, Vector3[] points, float wallHeight, float wallThickness)
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 start = points[i];
            Vector3 end = points[i + 1];
            string segmentName = $"Segment_{i}";

            CreateSingleWallSegment(parent, segmentName, start, end, wallHeight, wallThickness);
        }
    }

    /// <summary>
    /// Create a single box collider wall segment between two XZ points.
    /// </summary>
    private static void CreateSingleWallSegment(Transform parent, string name, Vector3 start, Vector3 end, float wallHeight, float wallThickness)
    {
        GameObject segment = new GameObject(name);
        segment.transform.SetParent(parent);

        // Position at midpoint between start and end
        Vector3 midpoint = (start + end) / 2f;
        midpoint.y = wallHeight / 2f; // Center vertically
        segment.transform.position = midpoint;

        // Calculate segment length and rotation
        Vector3 direction = end - start;
        direction.y = 0; // Keep on XZ plane
        float segmentLength = direction.magnitude;

        // Rotate to face along the segment direction
        if (segmentLength > 0.001f)
        {
            segment.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        // Add kinematic Rigidbody for physics interactions
        Rigidbody rb = segment.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        // Add BoxCollider sized to span the segment
        // Local Z = along segment direction (LookRotation forward), X = thickness, Y = height
        BoxCollider collider = segment.AddComponent<BoxCollider>();
        collider.size = new Vector3(wallThickness, wallHeight, segmentLength);
        collider.center = Vector3.zero;

        // Add bouncy physics material for realistic puck bounces off boards
        PhysicsMaterial boardMaterial = new PhysicsMaterial("BoardMaterial");
        boardMaterial.dynamicFriction = 0.3f;
        boardMaterial.staticFriction = 0.3f;
        boardMaterial.bounciness = 0.5f;
        boardMaterial.frictionCombine = PhysicsMaterialCombine.Average;
        boardMaterial.bounceCombine = PhysicsMaterialCombine.Average;
        collider.sharedMaterial = boardMaterial;
    }
#endif
}

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sets up the complete ice rink boundary with curved ends.
/// Creates both visual borders and physics colliders that match the ice surface shape.
/// </summary>
public class RinkBoundarySetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Setup Complete Rink Boundary")]
    public static void SetupRinkBoundary()
    {
        // International rink dimensions: 61m x 30m
        // Using 1 Unity unit = 1 meter
        float rinkLength = 61f;     // Length (will be horizontal/width in Unity)
        float rinkWidth = 30f;      // Width (will be vertical/height in Unity)
        float cornerRadius = 8.5f;  // Corner radius (all 4 corners)
        float wallThickness = 1f;   // Visual thickness
        int curveSegments = 16;     // Smoothness of curves (per corner)

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

        // Create complete boundary as a single EdgeCollider2D
        CreateBoundaryCollider(rinkBoundary, rinkLength, rinkWidth, cornerRadius, curveSegments);

        // Create visual walls
        CreateVisualWalls(rinkBoundary, rinkLength, rinkWidth, cornerRadius, wallThickness, curveSegments);

        Debug.Log($"Rink boundary created! Dimensions: {rinkLength}m x {rinkWidth}m");
        Debug.Log($"Corner radius: {cornerRadius}m at all 4 corners");
        Debug.Log("International ice hockey rink with proper rounded corners.");
    }

    private static void CreateBoundaryCollider(GameObject parent, float width, float height, float radius, int segments)
    {
        EdgeCollider2D collider = parent.GetComponent<EdgeCollider2D>();
        if (collider == null)
        {
            collider = parent.AddComponent<EdgeCollider2D>();
        }

        // Generate points for the complete boundary
        Vector2[] points = GenerateRinkOutline(width, height, radius, segments);
        collider.points = points;
        collider.edgeRadius = 0.1f; // Slight radius for smoother collisions
    }

    private static void CreateVisualWalls(GameObject parent, float length, float width, float radius, float thickness, int segments)
    {
        // Create wall sections with LineRenderers for visualization
        // For a rounded rectangle, create 4 walls (top, right, bottom, left) with corners
        CreateWallSection(parent, "WallTop", GenerateTopWall(length, width, radius, segments), thickness);
        CreateWallSection(parent, "WallRight", GenerateRightWall(length, width, radius, segments), thickness);
        CreateWallSection(parent, "WallBottom", GenerateBottomWall(length, width, radius, segments), thickness);
        CreateWallSection(parent, "WallLeft", GenerateLeftWall(length, width, radius, segments), thickness);
    }

    private static void CreateWallSection(GameObject parent, string name, Vector2[] points, float thickness)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent.transform);
        wall.transform.localPosition = Vector3.zero;

        LineRenderer line = wall.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.startWidth = thickness;
        line.endWidth = thickness;
        line.positionCount = points.Length;

        // Convert 2D points to 3D (z = 0)
        Vector3[] positions = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            positions[i] = new Vector3(points[i].x, points[i].y, 0);
        }
        line.SetPositions(positions);

        line.sortingOrder = 1; // Render above ice surface
        line.numCapVertices = 5;
    }

    // Generate complete outline for collision - proper rounded rectangle with 4 corners
    private static Vector2[] GenerateRinkOutline(float length, float width, float radius, int segments)
    {
        float halfLength = length / 2f;  // X dimension
        float halfWidth = width / 2f;    // Y dimension

        System.Collections.Generic.List<Vector2> pointsList = new System.Collections.Generic.List<Vector2>();

        // Start at top-right, go clockwise around the rink
        // Creating a proper rounded rectangle with 4 rounded corners

        // 1. Top-right corner (0° to 90°)
        Vector2 trCenter = new Vector2(halfLength - radius, halfWidth - radius);
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(0f, 90f, t);
            float radians = angle * Mathf.Deg2Rad;
            float x = trCenter.x + radius * Mathf.Cos(radians);
            float y = trCenter.y + radius * Mathf.Sin(radians);
            pointsList.Add(new Vector2(x, y));
        }

        // 2. Top edge (right to left along top)
        // Already at (halfLength - radius, halfWidth) from corner
        // Add point at left side before next corner
        pointsList.Add(new Vector2(-halfLength + radius, halfWidth));

        // 3. Top-left corner (90° to 180°)
        Vector2 tlCenter = new Vector2(-halfLength + radius, halfWidth - radius);
        for (int i = 1; i <= segments; i++) // Start at i=1 to avoid duplicate point
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(90f, 180f, t);
            float radians = angle * Mathf.Deg2Rad;
            float x = tlCenter.x + radius * Mathf.Cos(radians);
            float y = tlCenter.y + radius * Mathf.Sin(radians);
            pointsList.Add(new Vector2(x, y));
        }

        // 4. Left edge (top to bottom along left)
        pointsList.Add(new Vector2(-halfLength, -halfWidth + radius));

        // 5. Bottom-left corner (180° to 270°)
        Vector2 blCenter = new Vector2(-halfLength + radius, -halfWidth + radius);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(180f, 270f, t);
            float radians = angle * Mathf.Deg2Rad;
            float x = blCenter.x + radius * Mathf.Cos(radians);
            float y = blCenter.y + radius * Mathf.Sin(radians);
            pointsList.Add(new Vector2(x, y));
        }

        // 6. Bottom edge (left to right along bottom)
        pointsList.Add(new Vector2(halfLength - radius, -halfWidth));

        // 7. Bottom-right corner (270° to 360°)
        Vector2 brCenter = new Vector2(halfLength - radius, -halfWidth + radius);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(270f, 360f, t);
            float radians = angle * Mathf.Deg2Rad;
            float x = brCenter.x + radius * Mathf.Cos(radians);
            float y = brCenter.y + radius * Mathf.Sin(radians);
            pointsList.Add(new Vector2(x, y));
        }

        // 8. Right edge (bottom to top along right) - closes the loop
        pointsList.Add(new Vector2(halfLength, halfWidth - radius));

        Debug.Log($"Generated {pointsList.Count} points for rounded rectangle rink boundary");
        return pointsList.ToArray();
    }

    private static Vector2[] GenerateTopWall(float length, float width, float radius, int segments)
    {
        float halfLength = length / 2f;
        float halfWidth = width / 2f;

        System.Collections.Generic.List<Vector2> points = new System.Collections.Generic.List<Vector2>();

        // Start at left edge where left wall ends
        points.Add(new Vector2(-halfLength, halfWidth - radius));

        // Top-left corner (180° to 90°) - curves from left edge to top edge
        Vector2 tlCenter = new Vector2(-halfLength + radius, halfWidth - radius);
        for (int i = 1; i <= segments; i++)  // Start at i=1 to avoid duplicate point
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(180f, 90f, t);
            float radians = angle * Mathf.Deg2Rad;
            points.Add(tlCenter + new Vector2(radius * Mathf.Cos(radians), radius * Mathf.Sin(radians)));
        }

        // Top-right corner (90° to 0°) - curves from top edge to right edge
        Vector2 trCenter = new Vector2(halfLength - radius, halfWidth - radius);
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(90f, 0f, t);
            float radians = angle * Mathf.Deg2Rad;
            points.Add(trCenter + new Vector2(radius * Mathf.Cos(radians), radius * Mathf.Sin(radians)));
        }

        // End at right edge where right wall starts
        points.Add(new Vector2(halfLength, halfWidth - radius));

        return points.ToArray();
    }

    private static Vector2[] GenerateRightWall(float length, float width, float radius, int segments)
    {
        float halfLength = length / 2f;
        float halfWidth = width / 2f;

        return new Vector2[]
        {
            new Vector2(halfLength, halfWidth - radius),
            new Vector2(halfLength, -halfWidth + radius)
        };
    }

    private static Vector2[] GenerateBottomWall(float length, float width, float radius, int segments)
    {
        float halfLength = length / 2f;
        float halfWidth = width / 2f;

        System.Collections.Generic.List<Vector2> points = new System.Collections.Generic.List<Vector2>();

        // Start at right edge where right wall ends
        points.Add(new Vector2(halfLength, -halfWidth + radius));

        // Bottom-right corner (0° to 270°) - curves from right edge to bottom edge
        Vector2 brCenter = new Vector2(halfLength - radius, -halfWidth + radius);
        for (int i = 1; i <= segments; i++)  // Start at i=1 to avoid duplicate point
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(0f, 270f, t);
            float radians = angle * Mathf.Deg2Rad;
            points.Add(brCenter + new Vector2(radius * Mathf.Cos(radians), radius * Mathf.Sin(radians)));
        }

        // Bottom-left corner (270° to 180°) - curves from bottom edge to left edge
        Vector2 blCenter = new Vector2(-halfLength + radius, -halfWidth + radius);
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(270f, 180f, t);
            float radians = angle * Mathf.Deg2Rad;
            points.Add(blCenter + new Vector2(radius * Mathf.Cos(radians), radius * Mathf.Sin(radians)));
        }

        // End at left edge where left wall starts
        points.Add(new Vector2(-halfLength, -halfWidth + radius));

        return points.ToArray();
    }

    private static Vector2[] GenerateLeftWall(float length, float width, float radius, int segments)
    {
        float halfLength = length / 2f;
        float halfWidth = width / 2f;

        return new Vector2[]
        {
            new Vector2(-halfLength, -halfWidth + radius),
            new Vector2(-halfLength, halfWidth - radius)
        };
    }

    private static Vector2[] GenerateArc(float centerX, float centerY, float radius, float startAngle, float endAngle, int segments)
    {
        Vector2[] points = new Vector2[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            float radians = angle * Mathf.Deg2Rad;

            float x = centerX + radius * Mathf.Cos(radians);
            float y = centerY + radius * Mathf.Sin(radians);

            points[i] = new Vector2(x, y);
        }

        return points;
    }
#endif
}

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generates edge colliders for ice rink walls with rounded ends.
/// Creates realistic hockey rink boundaries that match the ice surface shape.
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
        float rinkWidth = 67f;      // Total width
        float rinkHeight = 28.5f;   // Total height
        float cornerRadius = 14.25f; // Radius for rounded ends
        int cornerSegments = 16;     // Smoothness of curves

        // Clear existing walls
        foreach (Transform child in rinkBoundary.transform)
        {
            if (child.name.StartsWith("Wall"))
            {
                DestroyImmediate(child.gameObject);
            }
        }

        // Create the four wall segments with edge colliders
        CreateNorthWall(rinkBoundary.transform, rinkWidth, rinkHeight, cornerRadius, cornerSegments);
        CreateSouthWall(rinkBoundary.transform, rinkWidth, rinkHeight, cornerRadius, cornerSegments);
        CreateEastWall(rinkBoundary.transform, rinkWidth, rinkHeight, cornerRadius, cornerSegments);
        CreateWestWall(rinkBoundary.transform, rinkWidth, rinkHeight, cornerRadius, cornerSegments);

        Debug.Log("Rink walls generated successfully!");
    }

    private static void CreateNorthWall(Transform parent, float width, float height, float radius, int segments)
    {
        GameObject wall = new GameObject("WallNorth");
        wall.transform.SetParent(parent);
        wall.transform.localPosition = Vector3.zero;

        EdgeCollider2D collider = wall.AddComponent<EdgeCollider2D>();

        // Create arc for north wall (top rounded end)
        Vector2[] points = GenerateArc(width/2f - radius, height/2f, radius, 0f, 180f, segments);
        collider.points = points;
    }

    private static void CreateSouthWall(Transform parent, float width, float height, float radius, int segments)
    {
        GameObject wall = new GameObject("WallSouth");
        wall.transform.SetParent(parent);
        wall.transform.localPosition = Vector3.zero;

        EdgeCollider2D collider = wall.AddComponent<EdgeCollider2D>();

        // Create arc for south wall (bottom rounded end)
        Vector2[] points = GenerateArc(width/2f - radius, -height/2f, radius, 180f, 360f, segments);
        collider.points = points;
    }

    private static void CreateEastWall(Transform parent, float width, float height, float radius, int segments)
    {
        GameObject wall = new GameObject("WallEast");
        wall.transform.SetParent(parent);
        wall.transform.localPosition = Vector3.zero;

        EdgeCollider2D collider = wall.AddComponent<EdgeCollider2D>();

        // Straight section on the east side
        float straightHeight = height - (radius * 2);
        Vector2[] points = new Vector2[]
        {
            new Vector2(width/2f, straightHeight/2f),
            new Vector2(width/2f, -straightHeight/2f)
        };
        collider.points = points;
    }

    private static void CreateWestWall(Transform parent, float width, float height, float radius, int segments)
    {
        GameObject wall = new GameObject("WallWest");
        wall.transform.SetParent(parent);
        wall.transform.localPosition = Vector3.zero;

        EdgeCollider2D collider = wall.AddComponent<EdgeCollider2D>();

        // Straight section on the west side
        float straightHeight = height - (radius * 2);
        Vector2[] points = new Vector2[]
        {
            new Vector2(-width/2f, -straightHeight/2f),
            new Vector2(-width/2f, straightHeight/2f)
        };
        collider.points = points;
    }

    private static Vector2[] GenerateArc(float centerX, float centerY, float radius, float startAngle, float endAngle, int segments)
    {
        Vector2[] points = new Vector2[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments);
            float radians = angle * Mathf.Deg2Rad;

            float x = centerX + radius * Mathf.Cos(radians);
            float y = centerY + radius * Mathf.Sin(radians);

            points[i] = new Vector2(x, y);
        }

        return points;
    }
#endif
}

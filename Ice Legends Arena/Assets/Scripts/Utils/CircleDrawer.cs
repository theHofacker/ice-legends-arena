using UnityEngine;

/// <summary>
/// Helper component to draw circles using LineRenderer.
/// Useful for creating rink markings like center ice circles and face-off circles.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class CircleDrawer : MonoBehaviour
{
    [Header("Circle Settings")]
    [Tooltip("Radius of the circle in Unity units")]
    [SerializeField] private float radius = 5f;

    [Tooltip("Number of segments (higher = smoother circle)")]
    [Range(8, 128)]
    [SerializeField] private int segments = 64;

    [Tooltip("Width of the line")]
    [Range(0.01f, 1f)]
    [SerializeField] private float lineWidth = 0.15f;

    [Header("Appearance")]
    [Tooltip("Color of the circle line")]
    [SerializeField] private Color lineColor = Color.red;

    private LineRenderer lineRenderer;

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        DrawCircle();
    }

    /// <summary>
    /// Draws the circle using the LineRenderer component
    /// </summary>
    private void DrawCircle()
    {
        if (lineRenderer == null)
        {
            Debug.LogError("CircleDrawer: LineRenderer component is missing!", this);
            return;
        }

        // Configure LineRenderer settings
        lineRenderer.positionCount = segments + 1;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        // Calculate circle points
        float angleStep = 360f / segments;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
    }

    /// <summary>
    /// Redraws the circle when values change in the Inspector
    /// </summary>
    private void OnValidate()
    {
        // Ensure valid values
        if (radius <= 0) radius = 5f;
        if (lineWidth <= 0) lineWidth = 0.15f;

        // Redraw if in Play mode
        if (Application.isPlaying && lineRenderer != null)
        {
            DrawCircle();
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draw gizmo preview of circle in Scene view
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = lineColor;
        DrawGizmoCircle(transform.position, radius);
    }

    private void DrawGizmoCircle(Vector3 center, float circleRadius)
    {
        int gizmoSegments = 32;
        float angleStep = 360f / gizmoSegments;

        for (int i = 0; i < gizmoSegments; i++)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

            Vector3 p1 = center + new Vector3(
                Mathf.Cos(angle1) * circleRadius,
                Mathf.Sin(angle1) * circleRadius,
                0
            );

            Vector3 p2 = center + new Vector3(
                Mathf.Cos(angle2) * circleRadius,
                Mathf.Sin(angle2) * circleRadius,
                0
            );

            Gizmos.DrawLine(p1, p2);
        }
    }
#endif
}

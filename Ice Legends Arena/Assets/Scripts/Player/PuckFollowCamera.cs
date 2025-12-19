using UnityEngine;

/// <summary>
/// Camera follows the puck (not the player) for mobile-optimized gameplay.
/// Inspired by Mini Basketball - shows ~60-70% of rink, creates fog of war effect.
/// </summary>
public class PuckFollowCamera : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("The puck to follow")]
    public Transform puckTransform;

    [Tooltip("How fast camera follows puck")]
    [Range(1f, 20f)]
    public float followSpeed = 10f;

    [Tooltip("Predict puck direction and lead camera")]
    public bool predictiveFollow = true;

    [Range(0f, 2f)]
    public float predictionAmount = 0.5f;

    [Header("Camera Bounds")]
    [Tooltip("Keep camera within rink boundaries")]
    public bool constrainToBounds = true;

    public Vector2 minBounds = new Vector2(-25f, -12f);
    public Vector2 maxBounds = new Vector2(25f, 12f);

    [Header("Zoom Settings")]
    [Tooltip("Base orthographic size (shows 60-70% of rink)")]
    [Range(10f, 25f)]
    public float baseZoom = 15f;

    [Tooltip("Zoom out when puck moves fast")]
    public bool dynamicZoom = false;

    [Range(0f, 5f)]
    public float zoomOutAmount = 2f;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;
    private Rigidbody2D puckRb;

    private void Start()
    {
        cam = GetComponent<Camera>();

        if (cam != null)
        {
            cam.orthographicSize = baseZoom;
        }

        // Auto-find puck if not assigned
        if (puckTransform == null)
        {
            GameObject puck = GameObject.FindGameObjectWithTag("Puck");
            if (puck != null)
            {
                puckTransform = puck.transform;
                puckRb = puck.GetComponent<Rigidbody2D>();
            }
            else
            {
                Debug.LogWarning("PuckFollowCamera: No puck found! Assign puckTransform or tag puck with 'Puck'");
            }
        }
        else
        {
            puckRb = puckTransform.GetComponent<Rigidbody2D>();
        }
    }

    private void LateUpdate()
    {
        if (puckTransform == null) return;

        // Calculate target position
        Vector3 targetPosition = CalculateTargetPosition();

        // Apply bounds constraint
        if (constrainToBounds)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
        }

        // Smooth follow
        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            1f / followSpeed
        );

        transform.position = smoothedPosition;

        // Dynamic zoom based on puck speed
        if (dynamicZoom && puckRb != null && cam != null)
        {
            float puckSpeed = puckRb.linearVelocity.magnitude;
            float targetZoom = baseZoom + (puckSpeed * zoomOutAmount * 0.1f);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * 2f);
        }
    }

    private Vector3 CalculateTargetPosition()
    {
        Vector3 puckPosition = puckTransform.position;

        // Predictive follow: anticipate puck direction
        if (predictiveFollow && puckRb != null)
        {
            Vector2 puckVelocity = puckRb.linearVelocity;
            Vector3 prediction = new Vector3(
                puckVelocity.x * predictionAmount,
                puckVelocity.y * predictionAmount,
                0
            );
            puckPosition += prediction;
        }

        // Keep Z position fixed for 2D
        return new Vector3(puckPosition.x, puckPosition.y, transform.position.z);
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize camera bounds in editor
        if (constrainToBounds)
        {
            Gizmos.color = Color.yellow;
            Vector3 bottomLeft = new Vector3(minBounds.x, minBounds.y, 0);
            Vector3 topRight = new Vector3(maxBounds.x, maxBounds.y, 0);
            Vector3 topLeft = new Vector3(minBounds.x, maxBounds.y, 0);
            Vector3 bottomRight = new Vector3(maxBounds.x, minBounds.y, 0);

            Gizmos.DrawLine(bottomLeft, topLeft);
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
        }
    }
}

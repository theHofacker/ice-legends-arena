using UnityEngine;

/// <summary>
/// Camera follows the puck (not the player) for mobile-optimized gameplay.
/// Supports both orthographic (top-down) and perspective (broadcast 3D) modes.
/// Inspired by Mini Basketball - shows ~60-70% of rink, creates fog of war effect.
///
/// Coordinate system (3D gameplay on XZ plane):
///   X = rink length (left goal to right goal)
///   Z = rink width (near boards to far boards)
///   Y = height above ice
/// </summary>
public class PuckFollowCamera : MonoBehaviour
{
    public enum CameraMode
    {
        Orthographic,  // Classic top-down view (mostly deprecated)
        Broadcast      // Angled 3D hockey broadcast view
    }

    [Header("Camera Mode")]
    public CameraMode cameraMode = CameraMode.Broadcast;

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

    [Header("Camera Bounds (Gameplay XZ)")]
    [Tooltip("Keep camera target within rink boundaries")]
    public bool constrainToBounds = true;

    [Tooltip("Min bounds on XZ plane (x = rink length min, y = rink width min Z)")]
    public Vector2 minBounds = new Vector2(-25f, -12f);

    [Tooltip("Max bounds on XZ plane (x = rink length max, y = rink width max Z)")]
    public Vector2 maxBounds = new Vector2(25f, 12f);

    [Header("Orthographic Settings")]
    [Tooltip("Orthographic size (shows 60-70% of rink)")]
    [Range(10f, 25f)]
    public float baseZoom = 15f;

    [Tooltip("Zoom out when puck moves fast")]
    public bool dynamicZoom = false;

    [Range(0f, 5f)]
    public float zoomOutAmount = 2f;

    [Header("Broadcast Camera Settings")]
    [Tooltip("How high above the ice the camera sits (positive Y)")]
    [Range(15f, 60f)]
    public float broadcastHeight = 30f;

    [Tooltip("How far to the side the camera sits (Z offset, negative = near side / south)")]
    [Range(-50f, -5f)]
    public float broadcastSideOffset = -25f;

    [Tooltip("Field of view for broadcast mode")]
    [Range(20f, 90f)]
    public float broadcastFOV = 60f;

    [Tooltip("How much camera follows puck along X (rink length). 1 = full follow, 0 = fixed")]
    [Range(0f, 1f)]
    public float broadcastFollowX = 0.6f;

    [Tooltip("How much camera follows puck along Z (rink width). Lower = more stable broadcast feel")]
    [Range(0f, 1f)]
    public float broadcastFollowZ = 0.2f;

    [Header("Screen Shake")]
    [Tooltip("Enable screen shake on big hits")]
    public bool enableScreenShake = true;

    [Tooltip("Shake intensity for body checks")]
    [Range(0.1f, 1f)]
    public float bodyCheckShakeIntensity = 0.3f;

    [Tooltip("Shake intensity for goals")]
    [Range(0.2f, 1.5f)]
    public float goalShakeIntensity = 0.5f;

    [Tooltip("Shake intensity for slapshots")]
    [Range(0.1f, 0.5f)]
    public float slapShotShakeIntensity = 0.2f;

    [Tooltip("How fast shake decays")]
    [Range(1f, 10f)]
    public float shakeDecay = 5f;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;
    private Rigidbody puckRb;

    // Screen shake state
    private float currentShakeIntensity = 0f;
    private Vector3 shakeOffset = Vector3.zero;

    // Singleton for easy access
    public static PuckFollowCamera Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        cam = GetComponent<Camera>();

        // Apply initial camera mode settings
        ApplyCameraMode();

        // Auto-find puck if not assigned
        if (puckTransform == null)
        {
            GameObject puck = GameObject.FindGameObjectWithTag("Puck");
            if (puck != null)
            {
                puckTransform = puck.transform;
                puckRb = puck.GetComponent<Rigidbody>();
            }
            else
            {
                Debug.LogWarning("PuckFollowCamera: No puck found! Assign puckTransform or tag puck with 'Puck'");
            }
        }
        else
        {
            puckRb = puckTransform.GetComponent<Rigidbody>();
        }

        // Subscribe to GameManager goal events for goal shake
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoalScored += OnGoalScored;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoalScored -= OnGoalScored;
        }
    }

    /// <summary>
    /// Apply camera mode settings (call when switching modes at runtime)
    /// </summary>
    public void ApplyCameraMode()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        if (cameraMode == CameraMode.Orthographic)
        {
            cam.orthographic = true;
            cam.orthographicSize = baseZoom;
            // Top-down: camera looks straight down (positive Y looking at XZ plane)
            transform.position = new Vector3(0f, broadcastHeight, 0f);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else // Broadcast
        {
            cam.orthographic = false;
            cam.fieldOfView = broadcastFOV;
            // Camera at (0, height, sideOffset) looking at rink center on XZ plane
            transform.position = new Vector3(0f, broadcastHeight, broadcastSideOffset);
            transform.LookAt(Vector3.zero, Vector3.up);
        }
    }

    private void OnGoalScored(bool scoredByPlayer, int playerScore, int opponentScore)
    {
        TriggerShake(goalShakeIntensity);
    }

    private void LateUpdate()
    {
        if (puckTransform == null) return;

        if (cameraMode == CameraMode.Orthographic)
        {
            UpdateOrthographic();
        }
        else
        {
            UpdateBroadcast();
        }
    }

    /// <summary>
    /// Orthographic top-down camera behavior (looking down Y axis at XZ plane)
    /// </summary>
    private void UpdateOrthographic()
    {
        Vector3 targetPosition = CalculateTargetPositionXZ();

        if (constrainToBounds)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.z = Mathf.Clamp(targetPosition.z, minBounds.y, maxBounds.y);
        }

        // Keep Y (height) fixed
        targetPosition.y = transform.position.y;

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position - shakeOffset,
            targetPosition,
            ref velocity,
            1f / followSpeed
        );

        ApplyScreenShake();
        transform.position = smoothedPosition + shakeOffset;

        // Dynamic zoom
        if (dynamicZoom && puckRb != null && cam != null)
        {
            float puckSpeed = puckRb.linearVelocity.magnitude;
            float targetZoom = baseZoom + (puckSpeed * zoomOutAmount * 0.1f);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * 2f);
        }
    }

    /// <summary>
    /// Broadcast-style perspective camera - angled view from one side of the rink.
    ///
    /// Coordinate system (3D gameplay on XZ plane):
    ///   X = rink length (left goal ←→ right goal)
    ///   Z = rink width (near boards ←→ far boards)
    ///   Y = height above the ice
    ///
    /// Broadcast camera sits on the "south" side (negative Z) of the rink,
    /// elevated above the ice (positive Y), looking across and down at the action.
    /// </summary>
    private void UpdateBroadcast()
    {
        // Calculate the target point on the XZ gameplay plane to look at
        Vector3 puckPos = puckTransform.position;
        float targetX = puckPos.x;
        float targetZ = puckPos.z;

        // Predictive follow
        if (predictiveFollow && puckRb != null)
        {
            Vector3 puckVel = puckRb.linearVelocity;
            targetX += puckVel.x * predictionAmount;
            targetZ += puckVel.z * predictionAmount;
        }

        // Clamp to bounds
        if (constrainToBounds)
        {
            targetX = Mathf.Clamp(targetX, minBounds.x, maxBounds.x);
            targetZ = Mathf.Clamp(targetZ, minBounds.y, maxBounds.y);
        }

        // Loose follow: camera tracks puck but doesn't snap to it
        float lookX = targetX * broadcastFollowX;
        float lookZ = targetZ * broadcastFollowZ;

        // Camera position:
        // X: follows the puck left-right along the rink
        // Y: elevated above the ice surface (positive Y)
        // Z: sits on the near side (south) of the rink, offset from center
        Vector3 targetCamPos = new Vector3(
            lookX,
            broadcastHeight,
            broadcastSideOffset
        );

        // Look-at point: where the puck is on the ice (XZ plane, Y=0)
        Vector3 lookAtPoint = new Vector3(lookX, 0f, lookZ);

        // Smooth camera movement
        Vector3 smoothedPos = Vector3.SmoothDamp(
            transform.position - shakeOffset,
            targetCamPos,
            ref velocity,
            1f / followSpeed
        );

        ApplyScreenShake();
        transform.position = smoothedPos + shakeOffset;

        // Point camera at the action on the ice
        transform.LookAt(lookAtPoint, Vector3.up);

        // Dynamic FOV zoom based on puck speed
        if (dynamicZoom && puckRb != null && cam != null)
        {
            float puckSpeed = puckRb.linearVelocity.magnitude;
            float targetFOV = broadcastFOV + (puckSpeed * zoomOutAmount * 0.3f);
            targetFOV = Mathf.Clamp(targetFOV, broadcastFOV, broadcastFOV + 15f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 2f);
        }
    }

    /// <summary>
    /// Apply screen shake offset (camera-space jitter on X and Y axes)
    /// </summary>
    private void ApplyScreenShake()
    {
        if (enableScreenShake && currentShakeIntensity > 0.01f)
        {
            shakeOffset = new Vector3(
                Random.Range(-1f, 1f) * currentShakeIntensity,
                Random.Range(-1f, 1f) * currentShakeIntensity,
                0f
            );
            currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, 0f, shakeDecay * Time.deltaTime);
        }
        else
        {
            shakeOffset = Vector3.zero;
            currentShakeIntensity = 0f;
        }
    }

    public void TriggerShake(float intensity)
    {
        if (!enableScreenShake) return;
        if (intensity > currentShakeIntensity)
        {
            currentShakeIntensity = intensity;
        }
    }

    public void TriggerBodyCheckShake()
    {
        TriggerShake(bodyCheckShakeIntensity);
    }

    public void TriggerSlapShotShake()
    {
        TriggerShake(slapShotShakeIntensity);
    }

    public void TriggerGoalShake()
    {
        TriggerShake(goalShakeIntensity);
    }

    /// <summary>
    /// Calculate target position for orthographic mode on the XZ plane.
    /// </summary>
    private Vector3 CalculateTargetPositionXZ()
    {
        Vector3 puckPosition = puckTransform.position;

        if (predictiveFollow && puckRb != null)
        {
            Vector3 puckVelocity = puckRb.linearVelocity;
            puckPosition += new Vector3(
                puckVelocity.x * predictionAmount,
                0f,
                puckVelocity.z * predictionAmount
            );
        }

        return new Vector3(puckPosition.x, transform.position.y, puckPosition.z);
    }

    private void OnDrawGizmosSelected()
    {
        if (constrainToBounds)
        {
            Gizmos.color = Color.yellow;
            // Draw bounds rectangle on the XZ plane (Y=0)
            Vector3 corner1 = new Vector3(minBounds.x, 0f, minBounds.y);
            Vector3 corner2 = new Vector3(minBounds.x, 0f, maxBounds.y);
            Vector3 corner3 = new Vector3(maxBounds.x, 0f, maxBounds.y);
            Vector3 corner4 = new Vector3(maxBounds.x, 0f, minBounds.y);

            Gizmos.DrawLine(corner1, corner2);
            Gizmos.DrawLine(corner2, corner3);
            Gizmos.DrawLine(corner3, corner4);
            Gizmos.DrawLine(corner4, corner1);
        }

        // Draw broadcast camera visualization
        if (cameraMode == CameraMode.Broadcast)
        {
            Gizmos.color = Color.cyan;
            // Draw line from camera to look-at point (rink center)
            Gizmos.DrawLine(transform.position, Vector3.zero);
            // Draw rink center cross on XZ plane
            Gizmos.DrawLine(new Vector3(-2, 0, 0), new Vector3(2, 0, 0));
            Gizmos.DrawLine(new Vector3(0, 0, -2), new Vector3(0, 0, 2));
        }
    }
}

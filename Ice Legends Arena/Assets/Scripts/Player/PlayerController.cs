using UnityEngine;

/// <summary>
/// Physics-based player movement controller for ice hockey gameplay.
/// Uses Rigidbody with direct velocity manipulation for smooth, ice-like movement.
/// Movement is on the XZ plane (X = rink length, Z = rink width), Y = height.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Maximum movement speed in units per second")]
    [Range(1f, 40f)]
    public float moveSpeed = 12f; // Made public for CharacterStatsApplier

    [Tooltip("How quickly the player accelerates from rest")]
    [Range(0.1f, 30f)]
    public float acceleration = 10f; // Made public for Shapeshift ability

    [Tooltip("How quickly the player decelerates to a stop (ice sliding)")]
    [Range(0.1f, 30f)]
    [SerializeField] private float deceleration = 15f;

    [Header("Debug")]
    [SerializeField] private bool showVelocityGizmo = true;

    // Component references
    private Rigidbody rb;
    private InputManager inputManager;
    private ShootingController shootingController;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Enforce correct physics settings for ice surface movement
        rb.useGravity = false;
        rb.linearDamping = 0.5f; // Low damping for ice-like sliding
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
    }

    private void Start()
    {
        // Get InputManager singleton
        inputManager = InputManager.Instance;

        if (inputManager == null)
        {
            Debug.LogError("PlayerController: InputManager instance not found!");
        }

        // Get ShootingController for checking shot charging state
        shootingController = GetComponent<ShootingController>();
    }

    private void FixedUpdate()
    {
        if (inputManager == null) return;

        HandleMovement();
    }

    /// <summary>
    /// Main movement logic using velocity interpolation for ice-like physics
    /// </summary>
    private void HandleMovement()
    {
        // Get input converted to world XZ direction
        Vector3 worldDir = PhysicsHelper.InputToWorld(inputManager.MoveInput);

        // Apply movement multiplier if charging a shot
        float speedMultiplier = 1f;
        if (shootingController != null && shootingController.IsChargingShot)
        {
            speedMultiplier = shootingController.ChargingMovementMultiplier;
        }

        // Calculate target velocity based on world direction and multiplier
        Vector3 targetVelocity = worldDir * moveSpeed * speedMultiplier;

        // Choose acceleration or deceleration based on input presence
        float lerpRate = (worldDir.magnitude > 0.1f) ? acceleration : deceleration;

        // Smoothly interpolate current velocity toward target velocity
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 newVelocity = Vector3.Lerp(
            new Vector3(currentVelocity.x, 0f, currentVelocity.z),
            targetVelocity,
            lerpRate * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector3(newVelocity.x, 0f, newVelocity.z);

        // Rotate model to face movement direction
        if (worldDir.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.Euler(0f, targetAngle, 0f),
                10f * Time.fixedDeltaTime
            );
        }
    }


    /// <summary>
    /// Draw debug gizmos to visualize player velocity
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showVelocityGizmo || rb == null) return;

        // Draw velocity vector
        Gizmos.color = Color.green;
        Vector3 start = transform.position;
        Vector3 end = start + rb.linearVelocity;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.1f);
    }

    /// <summary>
    /// Validate values in Inspector
    /// </summary>
    private void OnValidate()
    {
        // Ensure max speed is positive
        if (moveSpeed <= 0)
        {
            moveSpeed = 5f;
            Debug.LogWarning("PlayerController: moveSpeed must be positive", this);
        }

        // Ensure acceleration/deceleration are positive
        if (acceleration <= 0) acceleration = 10f;
        if (deceleration <= 0) deceleration = 15f;
    }
}

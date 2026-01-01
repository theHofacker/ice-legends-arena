using UnityEngine;

/// <summary>
/// Physics-based player movement controller for ice hockey gameplay.
/// Uses Rigidbody2D with direct velocity manipulation for smooth, ice-like movement.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Maximum movement speed in units per second")]
    [Range(1f, 20f)]
    public float moveSpeed = 5f; // Made public for CharacterStatsApplier

    [Tooltip("How quickly the player accelerates from rest")]
    [Range(0.1f, 30f)]
    [SerializeField] private float acceleration = 10f;

    [Tooltip("How quickly the player decelerates to a stop (ice sliding)")]
    [Range(0.1f, 30f)]
    [SerializeField] private float deceleration = 15f;

    [Header("Debug")]
    [SerializeField] private bool showVelocityGizmo = true;

    // Component references
    private Rigidbody2D rb;
    private InputManager inputManager;
    private SpriteRenderer spriteRenderer;
    private ShootingController shootingController;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Validate Rigidbody2D settings
        ValidateRigidbodySettings();
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
        UpdateSpriteDirection();
    }

    /// <summary>
    /// Main movement logic using velocity interpolation for ice-like physics
    /// </summary>
    private void HandleMovement()
    {
        // Get input from InputManager (already normalized -1 to 1)
        Vector2 moveInput = inputManager.MoveInput;

        // Apply movement multiplier if charging a shot
        float speedMultiplier = 1f;
        if (shootingController != null && shootingController.IsChargingShot)
        {
            speedMultiplier = shootingController.ChargingMovementMultiplier;
        }

        // Calculate target velocity based on input direction and multiplier
        Vector2 targetVelocity = moveInput * moveSpeed * speedMultiplier;

        // Choose acceleration or deceleration based on input presence
        // Deceleration is faster to allow for quick stops while maintaining ice slide feel
        float lerpRate = (moveInput.magnitude > 0.1f) ? acceleration : deceleration;

        // Smoothly interpolate current velocity toward target velocity
        // This creates smooth acceleration/deceleration curves
        Vector2 newVelocity = Vector2.Lerp(
            rb.linearVelocity,
            targetVelocity,
            lerpRate * Time.fixedDeltaTime
        );

        // Apply the calculated velocity to the rigidbody
        rb.linearVelocity = newVelocity;
    }

    /// <summary>
    /// Updates sprite direction based on horizontal movement input
    /// </summary>
    private void UpdateSpriteDirection()
    {
        if (spriteRenderer == null) return;

        Vector2 moveInput = inputManager.MoveInput;

        // Flip sprite based on horizontal movement
        if (moveInput.x > 0.1f) // Moving right
        {
            spriteRenderer.flipX = false;
        }
        else if (moveInput.x < -0.1f) // Moving left
        {
            spriteRenderer.flipX = true;
        }
        // Don't flip if no horizontal input (keep last direction)
    }

    /// <summary>
    /// Validates that Rigidbody2D is configured correctly for ice hockey physics
    /// </summary>
    private void ValidateRigidbodySettings()
    {
        if (rb.gravityScale != 0)
        {
            Debug.LogWarning($"PlayerController: Rigidbody2D gravity scale should be 0 for top-down movement. Current: {rb.gravityScale}", this);
        }

        if (rb.constraints != RigidbodyConstraints2D.FreezeRotation)
        {
            Debug.LogWarning("PlayerController: Consider freezing rotation to prevent spinning", this);
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
        Vector3 end = start + new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, 0);
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

using UnityEngine;

/// <summary>
/// Handles puck possession mechanics - makes puck "stick" to player when in possession.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PuckController : MonoBehaviour
{
    [Header("Possession Settings")]
    [Tooltip("Distance to automatically possess puck")]
    [Range(0.5f, 3f)]
    [SerializeField] private float possessionRadius = 1.5f;

    [Tooltip("Offset position from player when possessed (stick position)")]
    [SerializeField] private Vector2 possessionOffset = new Vector2(0.5f, 0f);

    [Tooltip("How smoothly puck follows player")]
    [Range(1f, 50f)]
    [SerializeField] private float followSpeed = 20f;

    [Header("Release Settings")]
    [Tooltip("Minimum shot speed to release puck from possession")]
    [Range(5f, 30f)]
    [SerializeField] private float releaseThreshold = 10f;

    // Component references
    private Rigidbody2D rb;
    private Transform playerTransform;
    private Collider2D puckCollider;
    private Collider2D playerCollider;
    private SpriteRenderer puckRenderer;

    // State
    private bool isPossessed = false;
    private Vector2 lastPlayerDirection = Vector2.right;
    private int originalSortingOrder;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        puckCollider = GetComponent<Collider2D>();
        puckRenderer = GetComponent<SpriteRenderer>();

        // Store original sorting order
        if (puckRenderer != null)
        {
            originalSortingOrder = puckRenderer.sortingOrder;
        }
    }

    private void Start()
    {
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerCollider = player.GetComponent<Collider2D>();
        }
        else
        {
            Debug.LogError("PuckController: No Player found! Tag your player with 'Player' tag.");
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        // Check for possession
        CheckPossession();

        // Update player direction for possession offset
        UpdatePlayerDirection();
    }

    private void FixedUpdate()
    {
        if (isPossessed && playerTransform != null)
        {
            // Follow player with offset
            FollowPlayer();
        }
    }

    private void CheckPossession()
    {
        float distance = Vector2.Distance(transform.position, playerTransform.position);

        // Auto-possess when close and puck is moving slowly
        if (!isPossessed && distance <= possessionRadius && rb.linearVelocity.magnitude < releaseThreshold)
        {
            isPossessed = true;
            rb.linearVelocity = Vector2.zero; // Stop puck movement

            // Disable collision between puck and player
            if (puckCollider != null && playerCollider != null)
            {
                Physics2D.IgnoreCollision(puckCollider, playerCollider, true);
            }

            // Render puck above player when possessed
            if (puckRenderer != null)
            {
                puckRenderer.sortingOrder = 15; // Above player (player is at 10)
            }
        }

        // Release when puck moves fast (from shooting)
        if (isPossessed && rb.linearVelocity.magnitude > releaseThreshold)
        {
            isPossessed = false;

            // Re-enable collision between puck and player
            if (puckCollider != null && playerCollider != null)
            {
                Physics2D.IgnoreCollision(puckCollider, playerCollider, false);
            }

            // Restore original sorting order
            if (puckRenderer != null)
            {
                puckRenderer.sortingOrder = originalSortingOrder;
            }
        }
    }

    private void FollowPlayer()
    {
        // Calculate target position (player + offset in facing direction)
        Vector2 offsetDirection = lastPlayerDirection.magnitude > 0.1f ? lastPlayerDirection : Vector2.right;
        Vector2 targetPosition = (Vector2)playerTransform.position + offsetDirection * possessionOffset.x;

        // Smoothly move puck to target position
        Vector2 newPosition = Vector2.Lerp(
            rb.position,
            targetPosition,
            followSpeed * Time.fixedDeltaTime
        );

        rb.MovePosition(newPosition);

        // Keep velocity at zero while possessed (except when shot is applied)
        if (rb.linearVelocity.magnitude < releaseThreshold)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void UpdatePlayerDirection()
    {
        // Track player movement direction from InputManager
        if (InputManager.Instance != null)
        {
            Vector2 moveInput = InputManager.Instance.MoveInput;
            if (moveInput.magnitude > 0.1f)
            {
                lastPlayerDirection = moveInput.normalized;
            }
        }
    }

    /// <summary>
    /// Public API to check if puck is currently possessed
    /// </summary>
    public bool IsPossessed()
    {
        return isPossessed;
    }

    /// <summary>
    /// Force release puck from possession (for special moves)
    /// </summary>
    public void ReleasePuck()
    {
        isPossessed = false;

        // Re-enable collision
        if (puckCollider != null && playerCollider != null)
        {
            Physics2D.IgnoreCollision(puckCollider, playerCollider, false);
        }

        // Restore original sorting order
        if (puckRenderer != null)
        {
            puckRenderer.sortingOrder = originalSortingOrder;
        }
    }

    /// <summary>
    /// Force possession (for game resets, etc.)
    /// </summary>
    public void ForcePossession(Transform owner)
    {
        playerTransform = owner;
        isPossessed = true;
        rb.linearVelocity = Vector2.zero;

        // Disable collision
        if (puckCollider != null && playerCollider != null)
        {
            Physics2D.IgnoreCollision(puckCollider, playerCollider, true);
        }

        // Render puck above player
        if (puckRenderer != null)
        {
            puckRenderer.sortingOrder = 15;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw possession radius
        Gizmos.color = isPossessed ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        // Draw possession offset indicator
        if (isPossessed && playerTransform != null)
        {
            Gizmos.color = Color.cyan;
            Vector2 offsetPos = (Vector2)playerTransform.position + lastPlayerDirection * possessionOffset.x;
            Gizmos.DrawLine(transform.position, offsetPos);
        }
    }
}

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
    private float timeSinceRelease = 0f;
    private bool collisionDisabledAfterShot = false;
    private bool wasOpponentPossessionLastFrame = false; // Track state changes for logging

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
        // Don't find player here - will be updated each frame from PlayerManager
    }

    private void Update()
    {
        // Update current player reference from PlayerManager
        UpdateCurrentPlayer();

        if (playerTransform == null) return;

        // Check for possession
        CheckPossession();

        // Update player direction for possession offset
        UpdatePlayerDirection();
    }

    /// <summary>
    /// Update current player reference to track the controlled player
    /// </summary>
    private void UpdateCurrentPlayer()
    {
        // Get currently controlled player from PlayerManager
        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
        {
            GameObject currentPlayer = PlayerManager.Instance.CurrentPlayer;

            // Only update if player changed
            if (playerTransform == null || playerTransform.gameObject != currentPlayer)
            {
                // Re-enable collision with old player (if exists)
                if (puckCollider != null && playerCollider != null && isPossessed)
                {
                    Physics2D.IgnoreCollision(puckCollider, playerCollider, false);
                }

                // Switch to new player
                playerTransform = currentPlayer.transform;
                playerCollider = currentPlayer.GetComponent<Collider2D>();

                // Re-disable collision if currently possessed
                if (puckCollider != null && playerCollider != null && isPossessed)
                {
                    Physics2D.IgnoreCollision(puckCollider, playerCollider, true);
                }

                Debug.Log($"PuckController now tracking: {currentPlayer.name}");
            }
        }
        else if (playerTransform == null)
        {
            // Fallback: find any player with "Player" tag (for when PlayerManager doesn't exist)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerCollider = player.GetComponent<Collider2D>();
            }
        }
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

        // Check if an OPPONENT has clearer possession than the controlled player
        // KEY CHANGE: Teammates share possession freely - only OPPONENTS block possession
        bool opponentHasPuck = false;

        // Only check if puck is slow enough to be possessed
        if (rb.linearVelocity.magnitude < 2f)
        {
            // Check OpponentController (old simple AI)
            OpponentController[] opponents = FindObjectsByType<OpponentController>(FindObjectsSortMode.None);
            foreach (OpponentController opponent in opponents)
            {
                float distanceToOpponent = Vector2.Distance(transform.position, opponent.transform.position);
                // Only prevent if opponent is CLOSER than controlled player AND very close
                if (distanceToOpponent < distance && distanceToOpponent <= possessionRadius * 0.75f)
                {
                    opponentHasPuck = true;
                    break;
                }
            }

            // Check AIController (new smart AI opponents)
            if (!opponentHasPuck)
            {
                AIController[] aiOpponents = FindObjectsByType<AIController>(FindObjectsSortMode.None);
                foreach (AIController ai in aiOpponents)
                {
                    float distanceToAI = Vector2.Distance(transform.position, ai.transform.position);
                    // Only prevent if AI is CLOSER than controlled player AND very close
                    if (distanceToAI < distance && distanceToAI <= possessionRadius * 0.75f)
                    {
                        opponentHasPuck = true;
                        break;
                    }
                }
            }

            // REMOVED: Teammate blocking check
            // Teammates can freely share possession with controlled player
            // This allows pass reception to work smoothly
        }

        // Only log when state CHANGES (not every frame)
        if (opponentHasPuck && !wasOpponentPossessionLastFrame)
        {
            Debug.Log($"Opponent has puck - preventing steal (they're closer)");
        }
        wasOpponentPossessionLastFrame = opponentHasPuck;

        // Auto-possess when close and puck is moving slowly (and opponent doesn't have it)
        bool canPossess = !isPossessed && distance <= possessionRadius && rb.linearVelocity.magnitude < releaseThreshold && !opponentHasPuck;

        if (canPossess)
        {
            isPossessed = true;
            rb.linearVelocity = Vector2.zero; // Stop puck movement
            Debug.Log($"Player auto-possessed puck (distance: {distance:F2}, velocity: {rb.linearVelocity.magnitude:F2}, opponentNearby: {opponentHasPuck})");

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
            collisionDisabledAfterShot = true;
            timeSinceRelease = 0f;

            // DON'T re-enable collision immediately - wait for puck to get away from player
            // Collision will be re-enabled after delay (see below)

            // Restore original sorting order
            if (puckRenderer != null)
            {
                puckRenderer.sortingOrder = originalSortingOrder;
            }
        }

        // Re-enable collision after short delay (prevents puck from hitting player after shot)
        if (collisionDisabledAfterShot)
        {
            timeSinceRelease += Time.deltaTime;

            // Re-enable after 0.2 seconds OR when puck is far enough away from ANY entity
            bool farEnoughAway = IsPuckFarFromAllEntities(2f);

            if (timeSinceRelease > 0.2f || farEnoughAway)
            {
                collisionDisabledAfterShot = false;

                // Re-enable collision with ALL entities (not just current player!)
                ReEnableCollisionWithAllEntities();

                Debug.Log("Puck collision re-enabled with all entities");
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
    /// Check if puck is far enough away from all entities (players, teammates, opponents)
    /// </summary>
    private bool IsPuckFarFromAllEntities(float minDistance)
    {
        // Check all players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < minDistance)
            {
                return false; // Too close to a player
            }
        }

        // Check all teammates
        TeammateController[] teammates = FindObjectsByType<TeammateController>(FindObjectsSortMode.None);
        foreach (TeammateController teammate in teammates)
        {
            float distance = Vector2.Distance(transform.position, teammate.transform.position);
            if (distance < minDistance)
            {
                return false; // Too close to a teammate
            }
        }

        // Check all AI opponents
        AIController[] aiOpponents = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        foreach (AIController ai in aiOpponents)
        {
            float distance = Vector2.Distance(transform.position, ai.transform.position);
            if (distance < minDistance)
            {
                return false; // Too close to an opponent
            }
        }

        // Check legacy opponents
        OpponentController[] opponents = FindObjectsByType<OpponentController>(FindObjectsSortMode.None);
        foreach (OpponentController opponent in opponents)
        {
            float distance = Vector2.Distance(transform.position, opponent.transform.position);
            if (distance < minDistance)
            {
                return false; // Too close to an opponent
            }
        }

        return true; // Far enough from everyone
    }

    /// <summary>
    /// Re-enable collision with all entities (players, teammates, opponents)
    /// Fixes bug where puck becomes untouchable after passing
    /// </summary>
    private void ReEnableCollisionWithAllEntities()
    {
        if (puckCollider == null) return;

        // Re-enable collision with all players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            Collider2D playerCol = player.GetComponent<Collider2D>();
            if (playerCol != null)
            {
                Physics2D.IgnoreCollision(puckCollider, playerCol, false);
            }
        }

        // Re-enable collision with all teammates
        TeammateController[] teammates = FindObjectsByType<TeammateController>(FindObjectsSortMode.None);
        foreach (TeammateController teammate in teammates)
        {
            Collider2D teammateCol = teammate.GetComponent<Collider2D>();
            if (teammateCol != null)
            {
                Physics2D.IgnoreCollision(puckCollider, teammateCol, false);
            }
        }

        // Re-enable collision with all AI opponents
        AIController[] aiOpponents = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        foreach (AIController ai in aiOpponents)
        {
            Collider2D aiCol = ai.GetComponent<Collider2D>();
            if (aiCol != null)
            {
                Physics2D.IgnoreCollision(puckCollider, aiCol, false);
            }
        }

        // Re-enable collision with legacy opponents
        OpponentController[] opponents = FindObjectsByType<OpponentController>(FindObjectsSortMode.None);
        foreach (OpponentController opponent in opponents)
        {
            Collider2D opponentCol = opponent.GetComponent<Collider2D>();
            if (opponentCol != null)
            {
                Physics2D.IgnoreCollision(puckCollider, opponentCol, false);
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

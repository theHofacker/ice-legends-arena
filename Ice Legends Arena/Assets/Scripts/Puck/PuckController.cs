using UnityEngine;

/// <summary>
/// Handles puck possession mechanics - makes puck "stick" to player when in possession.
/// 3D physics: movement on XZ plane, Y = height above ice (for saucer passes, roof shots).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PuckController : MonoBehaviour
{
    [Header("Possession Settings")]
    [Tooltip("Distance to automatically possess puck")]
    [Range(0.5f, 5f)]
    [SerializeField] private float possessionRadius = 3.0f;

    [Tooltip("Offset distance from player center when possessed (stick position)")]
    [SerializeField] private float possessionOffsetDistance = 1.5f;

    [Tooltip("How smoothly puck follows player")]
    [Range(1f, 50f)]
    [SerializeField] private float followSpeed = 20f;

    [Header("Release Settings")]
    [Tooltip("Minimum shot speed to release puck from possession")]
    [Range(5f, 30f)]
    [SerializeField] private float releaseThreshold = 10f;

    // Component references
    private Rigidbody rb;
    private Transform playerTransform;
    private Collider puckCollider;
    private Collider playerCollider;

    // State
    private bool isPossessed = false;
    private bool shotFired = false; // Flag set by shooting/passing to release puck
    private Vector3 lastPlayerDirection = Vector3.right;
    private float timeSinceRelease = 0f;
    private float repossessionCooldown = 0f; // Prevents instant re-possession after shot
    private bool collisionDisabledAfterShot = false;
    private bool wasOpponentPossessionLastFrame = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        puckCollider = GetComponent<Collider>();

        // Force possession values (scene serialization may have old 2D values)
        possessionRadius = 3.0f;
        possessionOffsetDistance = 1.5f;

        // Ensure puck has proper physics settings
        rb.useGravity = false; // Keep puck on ice, saucer passes add Y force directly
        rb.linearDamping = 0.5f; // Low friction on ice
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Ensure bouncy physics material so puck bounces off boards
        if (puckCollider.sharedMaterial == null)
        {
            PhysicsMaterial puckMat = new PhysicsMaterial("PuckMaterial");
            puckMat.bounciness = 0.6f;
            puckMat.dynamicFriction = 0.1f;
            puckMat.staticFriction = 0.1f;
            puckMat.bounceCombine = PhysicsMaterialCombine.Maximum;
            puckMat.frictionCombine = PhysicsMaterialCombine.Minimum;
            puckCollider.sharedMaterial = puckMat;
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

        // Keep puck on ice surface - clamp Y and strip vertical velocity
        Vector3 puckPos = transform.position;
        if (puckPos.y < 0f || puckPos.y > 0.5f)
        {
            puckPos.y = 0.05f;
            transform.position = puckPos;
        }
        // Always zero out Y velocity to keep puck sliding on ice
        // (saucer passes will temporarily override this)
        if (rb.linearVelocity.y != 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
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
                    Physics.IgnoreCollision(puckCollider, playerCollider, false);
                }

                // Switch to new player
                playerTransform = currentPlayer.transform;
                playerCollider = currentPlayer.GetComponent<Collider>();

                // Re-disable collision if currently possessed
                if (puckCollider != null && playerCollider != null && isPossessed)
                {
                    Physics.IgnoreCollision(puckCollider, playerCollider, true);
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
                playerCollider = player.GetComponent<Collider>();
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
        float distance = PhysicsHelper.DistanceXZ(transform.position, playerTransform.position);

        // Tick down repossession cooldown
        if (repossessionCooldown > 0f)
        {
            repossessionCooldown -= Time.deltaTime;
        }

        // Check if an OPPONENT has clearer possession than the controlled player
        bool opponentHasPuck = false;

        // Only check if puck is slow enough to be possessed
        if (PhysicsHelper.SpeedXZ(rb.linearVelocity) < 2f)
        {
            // Check OpponentController (old simple AI)
            OpponentController[] opponents = FindObjectsByType<OpponentController>(FindObjectsSortMode.None);
            foreach (OpponentController opponent in opponents)
            {
                float distanceToOpponent = PhysicsHelper.DistanceXZ(transform.position, opponent.transform.position);
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
                    float distanceToAI = PhysicsHelper.DistanceXZ(transform.position, ai.transform.position);
                    if (distanceToAI < distance && distanceToAI <= possessionRadius * 0.75f)
                    {
                        opponentHasPuck = true;
                        break;
                    }
                }
            }
        }

        // Only log when state CHANGES
        if (opponentHasPuck && !wasOpponentPossessionLastFrame)
        {
            Debug.Log($"Opponent has puck - preventing steal (they're closer)");
        }
        wasOpponentPossessionLastFrame = opponentHasPuck;

        // Auto-possess when close, puck is slow, no cooldown active, and opponent doesn't have it
        bool canPossess = !isPossessed && distance <= possessionRadius &&
                          PhysicsHelper.SpeedXZ(rb.linearVelocity) < releaseThreshold &&
                          !opponentHasPuck && repossessionCooldown <= 0f;

        if (canPossess)
        {
            isPossessed = true;
            rb.linearVelocity = Vector3.zero;
            Debug.Log($"Player auto-possessed puck (distance: {distance:F2}, velocity: {rb.linearVelocity.magnitude:F2}, opponentNearby: {opponentHasPuck})");

            // Disable collision between puck and player
            if (puckCollider != null && playerCollider != null)
            {
                Physics.IgnoreCollision(puckCollider, playerCollider, true);
            }
        }

        // Release when a shot or pass is fired (flag set by ShootingController/PassingController)
        if (isPossessed && shotFired)
        {
            isPossessed = false;
            shotFired = false;
            collisionDisabledAfterShot = true;
            timeSinceRelease = 0f;
            repossessionCooldown = 1.0f; // Can't re-possess for 1 second after shot
        }

        // Re-enable collision after short delay
        if (collisionDisabledAfterShot)
        {
            timeSinceRelease += Time.deltaTime;

            bool farEnoughAway = IsPuckFarFromAllEntities(2f);

            if (timeSinceRelease > 0.2f || farEnoughAway)
            {
                collisionDisabledAfterShot = false;
                ReEnableCollisionWithAllEntities();
                Debug.Log("Puck collision re-enabled with all entities");
            }
        }
    }

    private void FollowPlayer()
    {
        // Calculate target position (player + offset in facing direction)
        Vector3 offsetDirection = lastPlayerDirection.magnitude > 0.1f ? lastPlayerDirection : Vector3.right;
        offsetDirection = PhysicsHelper.FlattenY(offsetDirection).normalized;

        // Offset along facing direction on the XZ plane, keep puck on ice
        Vector3 targetPosition = playerTransform.position + offsetDirection * possessionOffsetDistance;
        targetPosition.y = 0.05f; // Puck sits just above ice surface

        // Smoothly move puck to target position
        Vector3 newPosition = Vector3.Lerp(
            rb.position,
            targetPosition,
            followSpeed * Time.fixedDeltaTime
        );

        rb.MovePosition(newPosition);

        // Keep velocity at zero while possessed (except when shot is applied)
        if (PhysicsHelper.SpeedXZ(rb.linearVelocity) < releaseThreshold)
        {
            rb.linearVelocity = Vector3.zero;
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
                lastPlayerDirection = PhysicsHelper.InputToWorld(moveInput).normalized;
            }
        }
    }

    /// <summary>
    /// Check if puck is far enough away from all entities
    /// </summary>
    private bool IsPuckFarFromAllEntities(float minDistance)
    {
        // Check all players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            if (PhysicsHelper.DistanceXZ(transform.position, player.transform.position) < minDistance)
                return false;
        }

        // Check all teammates
        TeammateController[] teammates = FindObjectsByType<TeammateController>(FindObjectsSortMode.None);
        foreach (TeammateController teammate in teammates)
        {
            if (PhysicsHelper.DistanceXZ(transform.position, teammate.transform.position) < minDistance)
                return false;
        }

        // Check all AI opponents
        AIController[] aiOpponents = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        foreach (AIController ai in aiOpponents)
        {
            if (PhysicsHelper.DistanceXZ(transform.position, ai.transform.position) < minDistance)
                return false;
        }

        // Check legacy opponents
        OpponentController[] opponents = FindObjectsByType<OpponentController>(FindObjectsSortMode.None);
        foreach (OpponentController opponent in opponents)
        {
            if (PhysicsHelper.DistanceXZ(transform.position, opponent.transform.position) < minDistance)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Re-enable collision with all entities
    /// </summary>
    private void ReEnableCollisionWithAllEntities()
    {
        if (puckCollider == null) return;

        // Re-enable collision with all players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            Collider playerCol = player.GetComponent<Collider>();
            if (playerCol != null)
                Physics.IgnoreCollision(puckCollider, playerCol, false);
        }

        // Re-enable collision with all teammates
        TeammateController[] teammates = FindObjectsByType<TeammateController>(FindObjectsSortMode.None);
        foreach (TeammateController teammate in teammates)
        {
            Collider teammateCol = teammate.GetComponent<Collider>();
            if (teammateCol != null)
                Physics.IgnoreCollision(puckCollider, teammateCol, false);
        }

        // Re-enable collision with all AI opponents
        AIController[] aiOpponents = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        foreach (AIController ai in aiOpponents)
        {
            Collider aiCol = ai.GetComponent<Collider>();
            if (aiCol != null)
                Physics.IgnoreCollision(puckCollider, aiCol, false);
        }

        // Re-enable collision with legacy opponents
        OpponentController[] opponents = FindObjectsByType<OpponentController>(FindObjectsSortMode.None);
        foreach (OpponentController opponent in opponents)
        {
            Collider opponentCol = opponent.GetComponent<Collider>();
            if (opponentCol != null)
                Physics.IgnoreCollision(puckCollider, opponentCol, false);
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
    /// Signal that a shot or pass was fired - puck will be released next frame.
    /// Called by ShootingController and PassingController after applying force.
    /// </summary>
    public void SignalShotFired()
    {
        shotFired = true;
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
            Physics.IgnoreCollision(puckCollider, playerCollider, false);
        }
    }

    /// <summary>
    /// Force possession (for game resets, etc.)
    /// </summary>
    public void ForcePossession(Transform owner)
    {
        playerTransform = owner;
        isPossessed = true;
        rb.linearVelocity = Vector3.zero;

        // Update player collider reference
        playerCollider = owner.GetComponent<Collider>();

        // Disable collision
        if (puckCollider != null && playerCollider != null)
        {
            Physics.IgnoreCollision(puckCollider, playerCollider, true);
        }
    }

    /// <summary>
    /// Force immediate possession for current controlled player (for pass reception)
    /// </summary>
    public void ForceImmediatePossession()
    {
        if (playerTransform == null) return;

        isPossessed = true;
        rb.linearVelocity = Vector3.zero;

        // Disable collision
        if (puckCollider != null && playerCollider != null)
        {
            Physics.IgnoreCollision(puckCollider, playerCollider, true);
        }

        Debug.Log($"Forced immediate possession for {playerTransform.name}");
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
            Vector3 offsetPos = playerTransform.position + PhysicsHelper.FlattenY(lastPlayerDirection).normalized * possessionOffsetDistance;
            Gizmos.DrawLine(transform.position, offsetPos);
        }
    }
}

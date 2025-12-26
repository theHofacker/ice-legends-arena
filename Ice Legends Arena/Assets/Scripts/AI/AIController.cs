using UnityEngine;

/// <summary>
/// AI State Machine for opponent hockey players.
/// Handles decision-making and behavior based on game context.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class AIController : MonoBehaviour
{
    [Header("AI Settings")]
    [Tooltip("AI difficulty level")]
    public AIDifficulty difficulty = AIDifficulty.Medium;

    [Header("Movement Settings")]
    [Tooltip("AI movement speed")]
    [Range(1f, 10f)]
    public float moveSpeed = 4f;

    [Header("Possession Settings")]
    [Tooltip("Distance to consider AI has puck")]
    [Range(0.5f, 3f)]
    public float possessionRadius = 1.5f;

    [Header("Detection Ranges")]
    [Tooltip("Distance to detect puck for chasing")]
    [Range(5f, 30f)]
    public float puckDetectionRange = 15f;

    [Tooltip("Distance to detect opponents for checking")]
    [Range(3f, 15f)]
    public float opponentDetectionRange = 8f;

    [Tooltip("Distance to shoot at goal")]
    [Range(5f, 20f)]
    public float shootingRange = 12f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    // AI States
    public enum AIState
    {
        Idle,           // Standing around, waiting for action
        ChasePuck,      // Moving toward loose puck
        AttackGoal,     // Has puck, moving toward opponent goal
        DefendGoal,     // Opponent has puck, defending own goal
        PassToTeammate, // Looking to pass puck to teammate
        CheckOpponent   // Attempting to check opponent with puck
    }

    // AI Difficulty Levels
    public enum AIDifficulty
    {
        Easy,   // Slow reaction, poor decisions
        Medium, // Average reaction, decent decisions
        Hard    // Fast reaction, smart decisions
    }

    // Component references
    private Rigidbody2D rb;
    private Transform puckTransform;
    private Rigidbody2D puckRb;
    private Transform playerGoal;  // Opponent's goal (AI's target)
    private Transform ownGoal;     // AI's goal (AI defends this)

    // State
    private AIState currentState = AIState.Idle;
    private bool hasPuck = false;
    private float lastStateChangeTime = 0f;
    private float reactionDelay = 0f;

    // Difficulty modifiers
    private float reactionTime;     // How fast AI reacts to changes
    private float accuracyModifier; // How accurate AI shots/passes are
    private float speedModifier;    // Movement speed multiplier

    // Public properties
    public AIState CurrentState => currentState;
    public bool HasPuck => hasPuck;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Set up physics
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearDamping = 2f;
    }

    private void Start()
    {
        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
            puckRb = puck.GetComponent<Rigidbody2D>();
        }

        // Find goals
        GameObject[] goals = GameObject.FindGameObjectsWithTag("Goal");
        if (goals.Length >= 2)
        {
            // TODO: Assign goals based on team (for now, just assign)
            playerGoal = goals[0].transform; // Opponent's goal (AI attacks)
            ownGoal = goals[1].transform;     // AI's goal (AI defends)
        }

        // Set difficulty modifiers
        SetDifficultyModifiers();

        Debug.Log($"AIController initialized on {gameObject.name} - Difficulty: {difficulty}");
    }

    private void Update()
    {
        if (puckTransform == null) return;

        // Check possession
        CheckPuckPossession();

        // Update AI state based on context
        UpdateState();

        // Execute current state behavior
        ExecuteState();
    }

    /// <summary>
    /// Set modifiers based on difficulty level
    /// </summary>
    private void SetDifficultyModifiers()
    {
        switch (difficulty)
        {
            case AIDifficulty.Easy:
                reactionTime = 0.5f;      // 500ms reaction delay
                accuracyModifier = 0.6f;  // 60% accuracy
                speedModifier = 0.8f;     // 80% speed
                break;

            case AIDifficulty.Medium:
                reactionTime = 0.3f;      // 300ms reaction delay
                accuracyModifier = 0.8f;  // 80% accuracy
                speedModifier = 1.0f;     // 100% speed
                break;

            case AIDifficulty.Hard:
                reactionTime = 0.1f;      // 100ms reaction delay
                accuracyModifier = 0.95f; // 95% accuracy
                speedModifier = 1.2f;     // 120% speed
                break;
        }
    }

    /// <summary>
    /// Check if AI has possession of the puck
    /// </summary>
    private void CheckPuckPossession()
    {
        float distance = Vector2.Distance(transform.position, puckTransform.position);
        hasPuck = distance <= possessionRadius && puckRb.linearVelocity.magnitude < 8f;
    }

    /// <summary>
    /// Update AI state based on game context
    /// </summary>
    private void UpdateState()
    {
        // Add reaction delay for difficulty
        if (Time.time - lastStateChangeTime < reactionDelay)
        {
            return; // Still in reaction delay, don't change state
        }

        AIState previousState = currentState;
        AIState newState = DetermineState();

        if (newState != previousState)
        {
            currentState = newState;
            lastStateChangeTime = Time.time;
            reactionDelay = reactionTime;

            Debug.Log($"{gameObject.name} state changed: {previousState} -> {newState}");
        }
    }

    /// <summary>
    /// Determine what state AI should be in based on context
    /// </summary>
    private AIState DetermineState()
    {
        // Priority 1: If AI has puck
        if (hasPuck)
        {
            // Check if in shooting range
            float distanceToGoal = Vector2.Distance(transform.position, playerGoal.position);
            if (distanceToGoal <= shootingRange)
            {
                return AIState.AttackGoal; // Close enough to shoot
            }
            else
            {
                // TODO: Check if should pass
                return AIState.AttackGoal; // Move toward goal
            }
        }

        // Priority 2: Check if opponent has puck
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);
            float distancePuckToPlayer = Vector2.Distance(puckTransform.position, player.transform.position);

            // If player has puck and is within range
            if (distancePuckToPlayer <= possessionRadius && distanceToPlayer <= opponentDetectionRange)
            {
                return AIState.CheckOpponent; // Try to check opponent
            }
        }

        // Priority 3: Check if puck is loose and nearby
        float distanceToPuck = Vector2.Distance(transform.position, puckTransform.position);
        if (distanceToPuck <= puckDetectionRange && puckRb.linearVelocity.magnitude > 1f)
        {
            return AIState.ChasePuck; // Chase loose puck
        }

        // Priority 4: Defend goal if puck is near
        if (ownGoal != null)
        {
            float puckDistanceToOwnGoal = Vector2.Distance(puckTransform.position, ownGoal.position);
            if (puckDistanceToOwnGoal < 20f) // Puck is threatening
            {
                return AIState.DefendGoal; // Get between puck and goal
            }
        }

        // Default: Idle
        return AIState.Idle;
    }

    /// <summary>
    /// Execute behavior for current state
    /// </summary>
    private void ExecuteState()
    {
        switch (currentState)
        {
            case AIState.Idle:
                ExecuteIdle();
                break;

            case AIState.ChasePuck:
                ExecuteChasePuck();
                break;

            case AIState.AttackGoal:
                ExecuteAttackGoal();
                break;

            case AIState.DefendGoal:
                ExecuteDefendGoal();
                break;

            case AIState.PassToTeammate:
                ExecutePassToTeammate();
                break;

            case AIState.CheckOpponent:
                ExecuteCheckOpponent();
                break;
        }
    }

    // ========== STATE BEHAVIORS ==========

    private void ExecuteIdle()
    {
        // Slow down to a stop
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 5f * Time.deltaTime);
    }

    private void ExecuteChasePuck()
    {
        // Move toward puck
        Vector2 direction = (puckTransform.position - transform.position).normalized;
        rb.linearVelocity = direction * (moveSpeed * speedModifier);
    }

    private void ExecuteAttackGoal()
    {
        if (!hasPuck)
        {
            // Lost puck, state will change next frame
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Move toward opponent's goal
        Vector2 direction = (playerGoal.position - transform.position).normalized;
        rb.linearVelocity = direction * (moveSpeed * speedModifier * 0.8f); // Slower with puck

        // TODO: Check if should shoot (will implement in Issue #42)
    }

    private void ExecuteDefendGoal()
    {
        if (ownGoal == null) return;

        // Position between puck and own goal
        Vector2 puckToGoal = (ownGoal.position - puckTransform.position).normalized;
        Vector2 defendPosition = (Vector2)puckTransform.position + puckToGoal * 3f; // 3 units in front of puck

        Vector2 direction = (defendPosition - (Vector2)transform.position).normalized;
        float distanceToPosition = Vector2.Distance(transform.position, defendPosition);

        if (distanceToPosition > 1f)
        {
            rb.linearVelocity = direction * (moveSpeed * speedModifier);
        }
        else
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 5f * Time.deltaTime);
        }
    }

    private void ExecutePassToTeammate()
    {
        // TODO: Implement in Issue #42 (AI Shooting & Passing Logic)
        Debug.Log($"{gameObject.name} wants to pass (not implemented yet)");
    }

    private void ExecuteCheckOpponent()
    {
        // Find nearest opponent with puck
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        GameObject targetOpponent = null;
        float nearestDistance = float.MaxValue;

        foreach (GameObject player in players)
        {
            float distancePuckToPlayer = Vector2.Distance(puckTransform.position, player.transform.position);
            float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);

            if (distancePuckToPlayer <= possessionRadius && distanceToPlayer < nearestDistance)
            {
                targetOpponent = player;
                nearestDistance = distanceToPlayer;
            }
        }

        if (targetOpponent != null)
        {
            // Move toward opponent to check them
            Vector2 direction = (targetOpponent.transform.position - transform.position).normalized;
            rb.linearVelocity = direction * (moveSpeed * speedModifier * 1.1f); // Slightly faster when checking

            // TODO: Execute actual check when close enough (will implement in Issue #43)
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // Draw possession radius
        Gizmos.color = hasPuck ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        // Draw detection ranges
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, puckDetectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, opponentDetectionRange);

        // Draw line to target based on state
        if (Application.isPlaying && puckTransform != null)
        {
            Gizmos.color = Color.magenta;
            switch (currentState)
            {
                case AIState.ChasePuck:
                    Gizmos.DrawLine(transform.position, puckTransform.position);
                    break;

                case AIState.AttackGoal:
                    if (playerGoal != null)
                        Gizmos.DrawLine(transform.position, playerGoal.position);
                    break;

                case AIState.DefendGoal:
                    if (ownGoal != null)
                        Gizmos.DrawLine(transform.position, ownGoal.position);
                    break;
            }
        }
    }
}

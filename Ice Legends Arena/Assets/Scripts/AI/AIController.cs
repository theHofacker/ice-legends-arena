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

    [Tooltip("Player position/role (legacy - use playerRole instead)")]
    public PlayerPosition position = PlayerPosition.Center;

    [Header("Formation Settings")]
    [Tooltip("Player role for FormationManager (Center, LW, RW, LD, RD)")]
    public FormationManager.PlayerRole playerRole = FormationManager.PlayerRole.Center;

    [Tooltip("Home position for this AI (FALLBACK if no FormationManager)")]
    public Vector2 homePosition = Vector2.zero;

    [Tooltip("How far AI can chase from home position")]
    [Range(5f, 30f)]
    public float zoneRadius = 15f;

    [Tooltip("Enable formation discipline (AI returns to position)")]
    public bool useFormation = true;

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
        CheckOpponent,  // Attempting to check opponent with puck
        ReturnToPosition // Returning to home position/zone
    }

    // Player Positions
    public enum PlayerPosition
    {
        Center,
        LeftWing,
        RightWing,
        LeftDefense,
        RightDefense
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

        // Set home position if not set
        if (homePosition == Vector2.zero)
        {
            homePosition = transform.position;
        }

        // Set difficulty modifiers
        SetDifficultyModifiers();

        Debug.Log($"AIController initialized on {gameObject.name} - Position: {position}, Difficulty: {difficulty}");
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

        // HYSTERESIS: Prevent rapid state changes (state thrashing)
        // Don't change state unless we've been in current state for minimum duration
        float minStateDuration = 0.5f; // Must stay in a state for at least 0.5 seconds
        float timeInCurrentState = Time.time - lastStateChangeTime;

        // Exception: Always allow transitioning FROM Idle (no hysteresis for initial movement)
        bool canChange = (previousState == AIState.Idle) || (timeInCurrentState >= minStateDuration);

        if (newState != previousState && canChange)
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

            // If player has puck and is within range, check if WE should be the one to challenge them
            if (distancePuckToPlayer <= possessionRadius && distanceToPlayer <= opponentDetectionRange)
            {
                // ALWAYS check if we're nearest (prevents all AI from swarming)
                // This check applies regardless of useFormation setting
                if (IsSignificantlyNearestAIToTarget(player.transform.position))
                {
                    return AIState.CheckOpponent; // Try to check opponent
                }
                else
                {
                    // Not nearest, so don't check - let nearest AI handle it
                    // Skip to next priority (defend goal or return to position)
                    continue;
                }
            }
        }

        // Priority 3: Check if puck is loose and nearby (with formation logic)
        float distanceToPuck = Vector2.Distance(transform.position, puckTransform.position);
        float distanceFromHome = Vector2.Distance(transform.position, homePosition);

        // Chase puck if it's loose (no one has it) OR it's moving
        // Changed from "velocity > 1f" to allow chasing stationary pucks
        bool puckIsLoose = !hasPuck; // If we don't have it, it might be available

        if (distanceToPuck <= puckDetectionRange && puckIsLoose)
        {
            // Only chase if:
            // 1. Formation is disabled, OR
            // 2. We're SIGNIFICANTLY the nearest AI to the puck (at least 3 units closer than anyone else)
            if (!useFormation || IsSignificantlyNearestAIToPuck())
            {
                return AIState.ChasePuck; // Chase loose puck
            }
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

        // Priority 5: Return to position if too far from home
        if (useFormation && distanceFromHome > zoneRadius)
        {
            return AIState.ReturnToPosition;
        }

        // Default: Idle
        return AIState.Idle;
    }

    /// <summary>
    /// Check if this AI is the nearest teammate to the puck (original version, unused)
    /// </summary>
    private bool IsNearestAIToPuck()
    {
        AIController[] allAI = FindObjectsOfType<AIController>();
        float myDistance = Vector2.Distance(transform.position, puckTransform.position);
        float nearestDistance = myDistance;

        foreach (AIController ai in allAI)
        {
            if (ai == this) continue; // Skip self

            float theirDistance = Vector2.Distance(ai.transform.position, puckTransform.position);
            if (theirDistance < nearestDistance)
            {
                return false; // Someone else is closer
            }
        }

        return true; // We're the nearest!
    }

    /// <summary>
    /// Check if this AI is the NEAREST to the puck (simpler check, no threshold)
    /// This prevents multiple AI from chasing - only the closest one chases
    /// </summary>
    private bool IsSignificantlyNearestAIToPuck()
    {
        AIController[] allAI = FindObjectsOfType<AIController>();
        float myDistance = Vector2.Distance(transform.position, puckTransform.position);

        foreach (AIController ai in allAI)
        {
            if (ai == this) continue; // Skip self

            float theirDistance = Vector2.Distance(ai.transform.position, puckTransform.position);

            // If ANYONE else is closer (or equal distance), I shouldn't chase
            if (theirDistance < myDistance)
            {
                return false;
            }
        }

        return true; // I'm the nearest!
    }

    /// <summary>
    /// Check if this AI is the NEAREST to a target position
    /// Used for checking opponents with puck - only nearest AI should challenge
    /// </summary>
    private bool IsSignificantlyNearestAIToTarget(Vector3 targetPosition)
    {
        AIController[] allAI = FindObjectsOfType<AIController>();
        float myDistance = Vector2.Distance(transform.position, targetPosition);

        foreach (AIController ai in allAI)
        {
            if (ai == this) continue; // Skip self

            float theirDistance = Vector2.Distance(ai.transform.position, targetPosition);

            // If ANYONE else is closer (or equal distance), I shouldn't check
            if (theirDistance < myDistance)
            {
                return false;
            }
        }

        return true; // I'm the nearest!
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

            case AIState.ReturnToPosition:
                ExecuteReturnToPosition();
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

        // Use OPPONENT team's FormationManager (not player team!)
        FormationManager opponentFormation = FormationManager.GetFormationManager(FormationManager.Team.Opponent);
        Vector2 defendPosition;

        if (opponentFormation != null)
        {
            // Get formation position based on defensive system (Box +1, Sagging Zone, etc.)
            defendPosition = opponentFormation.GetFormationPosition(playerRole);
        }
        else
        {
            // FALLBACK: Position between puck and own goal (old logic)
            Vector2 puckToGoal = (ownGoal.position - puckTransform.position).normalized;
            defendPosition = (Vector2)puckTransform.position + puckToGoal * 3f; // 3 units in front of puck
            Debug.LogWarning("AIController: No OpponentFormationManager found! Using fallback positioning.");
        }

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
            // Get opponent's rigidbody for velocity info
            Rigidbody2D opponentRb = targetOpponent.GetComponent<Rigidbody2D>();
            Vector2 opponentVelocity = opponentRb != null ? opponentRb.linearVelocity : Vector2.zero;

            // Evaluate Force vs Contain
            PuckControlEvaluator.DefensiveAction action = PuckControlEvaluator.EvaluateDefense(
                targetOpponent.transform.position,
                opponentVelocity,
                puckTransform.position,
                puckRb.linearVelocity,
                transform.position
            );

            // Get aggression level based on Force vs Contain
            float aggressionLevel = PuckControlEvaluator.GetAggressionLevel(action);

            // Move toward opponent based on aggression
            Vector2 direction = (targetOpponent.transform.position - transform.position).normalized;

            if (action == PuckControlEvaluator.DefensiveAction.Force)
            {
                // FORCE: Attack aggressively - full speed
                rb.linearVelocity = direction * (moveSpeed * speedModifier * 1.2f);
            }
            else
            {
                // CONTAIN: Play passive - maintain gap, don't overcommit
                float distanceToOpponent = Vector2.Distance(transform.position, targetOpponent.transform.position);

                if (distanceToOpponent > 3f)
                {
                    // Too far, close the gap slowly
                    rb.linearVelocity = direction * (moveSpeed * speedModifier * 0.6f);
                }
                else
                {
                    // Good gap, mirror opponent's movement (don't commit)
                    rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 3f * Time.deltaTime);
                }
            }

            // TODO: Execute actual check when close enough (will implement in Issue #43)
        }
    }

    private void ExecuteReturnToPosition()
    {
        // Use OPPONENT team's FormationManager (not player team!)
        FormationManager opponentFormation = FormationManager.GetFormationManager(FormationManager.Team.Opponent);
        Vector2 targetPosition;

        if (opponentFormation != null)
        {
            // Get formation position (could be offensive, defensive, or neutral)
            targetPosition = opponentFormation.GetFormationPosition(playerRole);
        }
        else
        {
            // FALLBACK: Use homePosition
            targetPosition = homePosition;
            Debug.LogWarning("AIController: No OpponentFormationManager found! Using homePosition fallback.");
        }

        Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;
        float distanceToTarget = Vector2.Distance(transform.position, targetPosition);

        if (distanceToTarget > 1f)
        {
            rb.linearVelocity = direction * (moveSpeed * speedModifier);
        }
        else
        {
            // Close enough to target, slow down
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, 5f * Time.deltaTime);
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

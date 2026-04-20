using UnityEngine;

/// <summary>
/// AI State Machine for opponent hockey players.
/// Handles decision-making and behavior based on game context.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
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
    public Vector3 homePosition = Vector3.zero;

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
        ReturnToPosition, // Returning to home position/zone
        Stunned         // Stunned by ability, cannot move or act
    }

    // AI Difficulty Levels
    public enum AIDifficulty
    {
        Easy,   // Slow reaction, poor decisions
        Medium, // Average reaction, decent decisions
        Hard    // Fast reaction, smart decisions
    }

    // Component references
    private Rigidbody rb;
    private Transform puckTransform;
    private Rigidbody puckRb;
    private Transform playerGoal;  // Opponent's goal (AI's target)
    private Transform ownGoal;     // AI's goal (AI defends this)

    // State
    private AIState currentState = AIState.Idle;
    private bool hasPuck = false;
    private float lastStateChangeTime = 0f;
    private float reactionDelay = 0f;

    // Stun state
    private bool isStunned = false;
    private float stunEndTime = 0f;
    private AIState preStunState = AIState.Idle;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    // Difficulty modifiers
    private float reactionTime;     // How fast AI reacts to changes
    private float accuracyModifier; // How accurate AI shots/passes are
    private float speedModifier;    // Movement speed multiplier

    // AI Action cooldowns and settings
    private float lastShotTime = -10f;
    private float lastPassTime = -10f;
    private float lastCheckTime = -10f;
    private float shotCooldown = 2f;        // Time between shots
    private float passCooldown = 1.5f;      // Time between passes
    private float checkCooldown = 1f;       // Time between checks
    private float aiShotPower = 18f;        // Base shot power
    private float aiPassPower = 12f;        // Base pass power
    private float pokeCheckRange = 2.5f;    // Range to attempt poke check
    private float bodyCheckRange = 1.5f;    // Range to attempt body check

    // Public properties
    public AIState CurrentState => currentState;
    public bool HasPuck => hasPuck;
    public bool IsStunned => isStunned;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Set up physics
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        rb.linearDamping = 2f;

        // Get sprite renderer for stun visual feedback
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    private void Start()
    {
        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
            puckRb = puck.GetComponent<Rigidbody>();
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
        if (homePosition == Vector3.zero)
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

        // Handle stun state
        if (isStunned)
        {
            UpdateStunState();
            return; // Skip all AI logic while stunned
        }

        // Check possession
        CheckPuckPossession();

        // Update AI state based on context
        UpdateState();

        // Execute current state behavior
        ExecuteState();

        // Safety: clamp to ice surface (prevents any Y drift from position assignments)
        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.y) > 0.1f)
        {
            pos.y = 0f;
            transform.position = pos;
        }
    }

    /// <summary>
    /// Update stun state - check if stun has ended
    /// </summary>
    private void UpdateStunState()
    {
        // Keep velocity at zero while stunned
        rb.linearVelocity = Vector3.zero;

        // Check if stun has ended
        if (Time.time >= stunEndTime)
        {
            EndStun();
        }
        else
        {
            // Flash effect while stunned
            if (spriteRenderer != null)
            {
                float flash = Mathf.PingPong(Time.time * 8f, 1f);
                spriteRenderer.color = Color.Lerp(Color.yellow, Color.white, flash);
            }
        }
    }

    /// <summary>
    /// Apply stun effect to this AI for specified duration
    /// </summary>
    public void Stun(float duration)
    {
        if (isStunned)
        {
            // Extend existing stun if new stun is longer
            float newEndTime = Time.time + duration;
            if (newEndTime > stunEndTime)
            {
                stunEndTime = newEndTime;
                Debug.Log($"{gameObject.name} stun extended to {duration}s");
            }
            return;
        }

        isStunned = true;
        preStunState = currentState;
        currentState = AIState.Stunned;
        stunEndTime = Time.time + duration;

        // Stop movement immediately
        rb.linearVelocity = Vector3.zero;

        // Visual feedback - turn yellow
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.yellow;
        }

        Debug.Log($"⚡ {gameObject.name} STUNNED for {duration}s!");
    }

    /// <summary>
    /// End stun effect and restore normal behavior
    /// </summary>
    private void EndStun()
    {
        isStunned = false;
        currentState = AIState.Idle; // Reset to idle, will determine new state next frame

        // Restore original color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        Debug.Log($"{gameObject.name} recovered from stun!");
    }

    /// <summary>
    /// Set modifiers based on difficulty level (legacy method)
    /// </summary>
    private void SetDifficultyModifiers()
    {
        // Try to use DifficultyManager settings first
        if (DifficultyManager.ActiveDifficulty != null)
        {
            ApplyDifficultySettings(DifficultyManager.ActiveDifficulty);
            return;
        }

        // Fallback to basic enum-based settings
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
    /// Apply detailed difficulty settings from AIDifficultySettings ScriptableObject
    /// </summary>
    public void ApplyDifficultySettings(AIDifficultySettings settings)
    {
        if (settings == null) return;

        // Timing
        reactionTime = settings.reactionTime;
        shotCooldown = settings.shotCooldown;
        passCooldown = settings.passCooldown;
        checkCooldown = settings.checkCooldown;

        // Movement
        speedModifier = settings.speedModifier;

        // Accuracy (use shot accuracy as the main accuracy modifier)
        accuracyModifier = settings.shotAccuracy;

        // Shot power
        aiShotPower = 18f * settings.shotPowerModifier;

        // Pass power (scaled from pass accuracy)
        aiPassPower = 12f * (0.8f + settings.passAccuracy * 0.4f);

        // Check ranges modified by aggression
        pokeCheckRange = 2.5f * (0.8f + settings.aggression * 0.4f);
        bodyCheckRange = 1.5f * (0.8f + settings.aggression * 0.4f);

        Debug.Log($"{gameObject.name}: Applied difficulty '{settings.difficultyName}' - " +
                  $"Reaction:{reactionTime:F2}s, Speed:{speedModifier:F2}x, Accuracy:{accuracyModifier:P0}");
    }

    /// <summary>
    /// Check if AI has possession of the puck
    /// </summary>
    private void CheckPuckPossession()
    {
        float distance = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);
        hasPuck = distance <= possessionRadius && PhysicsHelper.SpeedXZ(puckRb.linearVelocity) < 8f;
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
            float distanceToGoal = PhysicsHelper.DistanceXZ(transform.position, playerGoal.position);
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
            float distanceToPlayer = PhysicsHelper.DistanceXZ(transform.position, player.transform.position);
            float distancePuckToPlayer = PhysicsHelper.DistanceXZ(puckTransform.position, player.transform.position);

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
        float distanceToPuck = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);
        float distanceFromHome = PhysicsHelper.DistanceXZ(transform.position, homePosition);

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
            float puckDistanceToOwnGoal = PhysicsHelper.DistanceXZ(puckTransform.position, ownGoal.position);
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
        AIController[] allAI = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        float myDistance = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);
        float nearestDistance = myDistance;

        foreach (AIController ai in allAI)
        {
            if (ai == this) continue; // Skip self

            float theirDistance = PhysicsHelper.DistanceXZ(ai.transform.position, puckTransform.position);
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
        AIController[] allAI = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        float myDistance = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);

        foreach (AIController ai in allAI)
        {
            if (ai == this) continue; // Skip self

            float theirDistance = PhysicsHelper.DistanceXZ(ai.transform.position, puckTransform.position);

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
        AIController[] allAI = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        float myDistance = PhysicsHelper.DistanceXZ(transform.position, targetPosition);

        foreach (AIController ai in allAI)
        {
            if (ai == this) continue; // Skip self

            float theirDistance = PhysicsHelper.DistanceXZ(ai.transform.position, targetPosition);

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
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 5f * Time.deltaTime);
    }

    private void ExecuteChasePuck()
    {
        // Move toward puck
        Vector3 direction = PhysicsHelper.DirectionXZ(transform.position, puckTransform.position);
        rb.linearVelocity = direction * (moveSpeed * speedModifier);
    }

    private void ExecuteAttackGoal()
    {
        if (!hasPuck)
        {
            // Lost puck, state will change next frame
            rb.linearVelocity = Vector3.zero;
            return;
        }

        float distanceToGoal = PhysicsHelper.DistanceXZ(transform.position, playerGoal.position);

        // Check if should shoot
        if (distanceToGoal <= shootingRange && CanShoot())
        {
            // Check if there's a clear shot (no defenders blocking)
            if (HasClearShot() || distanceToGoal < shootingRange * 0.5f)
            {
                TryShoot();
                return;
            }
            // If no clear shot, consider passing
            else if (CanPass())
            {
                AIController openTeammate = FindOpenTeammate();
                if (openTeammate != null)
                {
                    TryPassTo(openTeammate);
                    return;
                }
            }
        }

        // Move toward opponent's goal while carrying puck
        Vector3 direction = PhysicsHelper.DirectionXZ(transform.position, playerGoal.position);

        // Carry puck with player (puck follows AI)
        if (puckRb != null)
        {
            Vector3 puckTargetPos = transform.position + direction * 0.8f;
            puckTargetPos.y = puckTransform.position.y;
            Vector3 puckDir = (puckTargetPos - puckTransform.position);
            puckRb.linearVelocity = puckDir * 10f;
        }

        rb.linearVelocity = direction * (moveSpeed * speedModifier * 0.8f); // Slower with puck
    }

    /// <summary>
    /// Check if AI can shoot (cooldown passed)
    /// </summary>
    private bool CanShoot()
    {
        return Time.time - lastShotTime >= shotCooldown;
    }

    /// <summary>
    /// Check if AI can pass (cooldown passed)
    /// </summary>
    private bool CanPass()
    {
        return Time.time - lastPassTime >= passCooldown;
    }

    /// <summary>
    /// Check if there's a clear path to goal (no defenders blocking)
    /// </summary>
    private bool HasClearShot()
    {
        if (playerGoal == null) return false;

        Vector3 toGoal = PhysicsHelper.FlattenY(playerGoal.position - transform.position);
        float distance = toGoal.magnitude;
        Vector3 direction = toGoal.normalized;

        // Raycast toward goal to check for blockers
        RaycastHit[] hits = Physics.RaycastAll(transform.position, direction, distance);

        foreach (RaycastHit hit in hits)
        {
            // Skip self and puck
            if (hit.transform == transform || hit.transform == puckTransform) continue;

            // Check if it's a player (defender)
            if (hit.transform.CompareTag("Player"))
            {
                return false; // Defender in the way
            }
        }

        return true;
    }

    /// <summary>
    /// Attempt to shoot at goal
    /// </summary>
    private void TryShoot()
    {
        if (puckRb == null || playerGoal == null) return;

        // Calculate shot direction toward goal with accuracy modifier
        Vector3 goalDirection = PhysicsHelper.DirectionXZ(transform.position, playerGoal.position);

        // Add inaccuracy based on difficulty (lower accuracy = more random spread)
        float spreadAngle = (1f - accuracyModifier) * 30f; // Max 30 degree spread for Easy
        float randomAngle = Random.Range(-spreadAngle, spreadAngle);
        goalDirection = Quaternion.Euler(0, randomAngle, 0) * goalDirection;

        // Apply shot force
        float shotPower = aiShotPower * speedModifier;
        puckRb.linearVelocity = Vector3.zero;
        puckRb.AddForce(goalDirection * shotPower, ForceMode.Impulse);

        lastShotTime = Time.time;
        hasPuck = false;

        Debug.Log($"🏒 {gameObject.name} SHOOTS! Power: {shotPower:F1}, Accuracy: {accuracyModifier:P0}");
    }

    /// <summary>
    /// Find an open teammate to pass to
    /// </summary>
    private AIController FindOpenTeammate()
    {
        AIController[] allAI = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        AIController bestTarget = null;
        float bestScore = float.MinValue;

        foreach (AIController ai in allAI)
        {
            if (ai == this) continue; // Skip self

            float distance = PhysicsHelper.DistanceXZ(transform.position, ai.transform.position);

            // Skip teammates too close or too far
            if (distance < 3f || distance > 20f) continue;

            // Calculate "openness" score
            float score = 0f;

            // Prefer teammates closer to goal
            float theirDistanceToGoal = PhysicsHelper.DistanceXZ(ai.transform.position, playerGoal.position);
            score += (30f - theirDistanceToGoal); // Higher score for closer to goal

            // Check if pass lane is clear
            if (IsPassLaneClear(ai.transform.position))
            {
                score += 20f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = ai;
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// Check if there's a clear passing lane to target position
    /// </summary>
    private bool IsPassLaneClear(Vector3 targetPosition)
    {
        Vector3 toTarget = PhysicsHelper.FlattenY(targetPosition - transform.position);
        float distance = toTarget.magnitude;
        Vector3 direction = toTarget.normalized;

        RaycastHit[] hits = Physics.RaycastAll(transform.position, direction, distance);

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform == puckTransform) continue;

            // Check if it's an opponent (player)
            if (hit.transform.CompareTag("Player"))
            {
                return false; // Opponent blocking pass lane
            }
        }

        return true;
    }

    /// <summary>
    /// Attempt to pass to a teammate
    /// </summary>
    private void TryPassTo(AIController teammate)
    {
        if (puckRb == null || teammate == null) return;

        // Calculate pass direction with lead (anticipate teammate movement)
        Vector3 passDirection = PhysicsHelper.DirectionXZ(transform.position, teammate.transform.position);

        // Add inaccuracy based on difficulty
        float spreadAngle = (1f - accuracyModifier) * 20f;
        float randomAngle = Random.Range(-spreadAngle, spreadAngle);
        passDirection = Quaternion.Euler(0, randomAngle, 0) * passDirection;

        // Apply pass force
        float passPower = aiPassPower * speedModifier;
        puckRb.linearVelocity = Vector3.zero;
        puckRb.AddForce(passDirection * passPower, ForceMode.Impulse);

        lastPassTime = Time.time;
        hasPuck = false;

        Debug.Log($"🏒 {gameObject.name} PASSES to {teammate.gameObject.name}!");
    }

    private void ExecuteDefendGoal()
    {
        if (ownGoal == null) return;

        // Use OPPONENT team's FormationManager (not player team!)
        FormationManager opponentFormation = FormationManager.GetFormationManager(FormationManager.Team.Opponent);
        Vector3 defendPosition;

        if (opponentFormation != null)
        {
            // Get formation position based on defensive system (Box +1, Sagging Zone, etc.)
            defendPosition = opponentFormation.GetFormationPosition(playerRole);
        }
        else
        {
            // FALLBACK: Position between puck and own goal (old logic)
            Vector3 puckToGoal = PhysicsHelper.DirectionXZ(puckTransform.position, ownGoal.position);
            defendPosition = puckTransform.position + puckToGoal * 3f; // 3 units in front of puck
            defendPosition.y = transform.position.y;
            Debug.LogWarning("AIController: No OpponentFormationManager found! Using fallback positioning.");
        }

        Vector3 direction = PhysicsHelper.DirectionXZ(transform.position, defendPosition);
        float distanceToPosition = PhysicsHelper.DistanceXZ(transform.position, defendPosition);

        if (distanceToPosition > 1f)
        {
            rb.linearVelocity = direction * (moveSpeed * speedModifier);
        }
        else
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 5f * Time.deltaTime);
        }
    }

    private void ExecutePassToTeammate()
    {
        if (!hasPuck || !CanPass())
        {
            // No puck or on cooldown, transition to different state
            return;
        }

        AIController openTeammate = FindOpenTeammate();
        if (openTeammate != null)
        {
            TryPassTo(openTeammate);
        }
        else
        {
            // No open teammate, try to advance
            currentState = AIState.AttackGoal;
        }
    }

    private void ExecuteCheckOpponent()
    {
        // Find nearest opponent with puck
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        GameObject targetOpponent = null;
        float nearestDistance = float.MaxValue;

        foreach (GameObject player in players)
        {
            float distancePuckToPlayer = PhysicsHelper.DistanceXZ(puckTransform.position, player.transform.position);
            float distanceToPlayer = PhysicsHelper.DistanceXZ(transform.position, player.transform.position);

            if (distancePuckToPlayer <= possessionRadius && distanceToPlayer < nearestDistance)
            {
                targetOpponent = player;
                nearestDistance = distanceToPlayer;
            }
        }

        if (targetOpponent != null)
        {
            // Get opponent's rigidbody for velocity info
            Rigidbody opponentRb = targetOpponent.GetComponent<Rigidbody>();
            Vector3 opponentVelocity = opponentRb != null ? opponentRb.linearVelocity : Vector3.zero;

            // Evaluate Force vs Contain
            PuckControlEvaluator.DefensiveAction action = PuckControlEvaluator.EvaluateDefense(
                targetOpponent.transform.position,
                opponentVelocity,
                puckTransform.position,
                puckRb.linearVelocity,
                transform.position
            );

            // Move toward opponent based on aggression
            Vector3 direction = PhysicsHelper.DirectionXZ(transform.position, targetOpponent.transform.position);
            float distanceToOpponent = nearestDistance;

            if (action == PuckControlEvaluator.DefensiveAction.Force)
            {
                // FORCE: Attack aggressively - full speed
                rb.linearVelocity = direction * (moveSpeed * speedModifier * 1.2f);

                // Execute check when close enough
                if (distanceToOpponent <= bodyCheckRange && CanCheck())
                {
                    TryBodyCheck(targetOpponent, opponentRb);
                }
                else if (distanceToOpponent <= pokeCheckRange && CanCheck())
                {
                    TryPokeCheck(targetOpponent);
                }
            }
            else
            {
                // CONTAIN: Play passive - maintain gap, don't overcommit
                if (distanceToOpponent > 3f)
                {
                    // Too far, close the gap slowly
                    rb.linearVelocity = direction * (moveSpeed * speedModifier * 0.6f);
                }
                else
                {
                    // Good gap, try poke check if in range
                    if (distanceToOpponent <= pokeCheckRange && CanCheck())
                    {
                        TryPokeCheck(targetOpponent);
                    }
                    // Mirror opponent's movement (don't commit)
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 3f * Time.deltaTime);
                }
            }
        }
    }

    /// <summary>
    /// Check if AI can perform a check (cooldown passed)
    /// </summary>
    private bool CanCheck()
    {
        return Time.time - lastCheckTime >= checkCooldown;
    }

    /// <summary>
    /// Attempt a poke check on the opponent
    /// </summary>
    private void TryPokeCheck(GameObject opponent)
    {
        // Calculate success chance based on distance and difficulty
        float distance = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);
        float normalizedDistance = distance / pokeCheckRange;
        float baseSuccessChance = Mathf.Lerp(0.8f, 0.3f, normalizedDistance);
        float successChance = baseSuccessChance * accuracyModifier;

        lastCheckTime = Time.time;

        if (Random.value <= successChance)
        {
            // SUCCESS - knock puck loose
            Vector3 pokeDirection = PhysicsHelper.DirectionXZ(transform.position, puckTransform.position);
            pokeDirection = Quaternion.Euler(0, Random.Range(-30f, 30f), 0) * pokeDirection;

            puckRb.AddForce(pokeDirection * 8f, ForceMode.Impulse);

            Debug.Log($"✓ {gameObject.name} POKE CHECK SUCCESS! (chance: {successChance:P0})");

            // Visual feedback - flash
            StartCoroutine(CheckFlashEffect(Color.green));
        }
        else
        {
            Debug.Log($"✗ {gameObject.name} poke check missed (chance: {successChance:P0})");

            // Visual feedback - flash red for miss
            StartCoroutine(CheckFlashEffect(Color.red));
        }
    }

    /// <summary>
    /// Attempt a body check on the opponent
    /// </summary>
    private void TryBodyCheck(GameObject opponent, Rigidbody opponentRb)
    {
        lastCheckTime = Time.time;

        // Calculate hit direction
        Vector3 hitDirection = PhysicsHelper.DirectionXZ(transform.position, opponent.transform.position);

        // Apply knockback to opponent
        float knockbackForce = 15f * speedModifier;
        if (opponentRb != null)
        {
            opponentRb.AddForce(hitDirection * knockbackForce, ForceMode.Impulse);
        }

        // Knock puck loose
        Vector3 puckKnockDirection = hitDirection + new Vector3(0f, 0.5f, 0f);
        puckRb.AddForce(puckKnockDirection.normalized * 10f, ForceMode.Impulse);

        // Self knockback (recovery)
        rb.AddForce(-hitDirection * knockbackForce * 0.3f, ForceMode.Impulse);

        Debug.Log($"💥 {gameObject.name} BODY CHECK on {opponent.name}!");

        // Trigger screen shake
        if (PuckFollowCamera.Instance != null)
        {
            PuckFollowCamera.Instance.TriggerBodyCheckShake();
        }

        // Visual feedback
        StartCoroutine(CheckFlashEffect(Color.yellow));

        // Try to stun opponent AI if they have AIController
        AIController opponentAI = opponent.GetComponent<AIController>();
        if (opponentAI != null)
        {
            opponentAI.Stun(1.5f);
        }

        // Try OpponentController stun
        OpponentController opponentController = opponent.GetComponent<OpponentController>();
        if (opponentController != null)
        {
            opponentController.ApplyBodyCheckStun(1.5f);
        }
    }

    /// <summary>
    /// Visual feedback for check attempts
    /// </summary>
    private System.Collections.IEnumerator CheckFlashEffect(Color flashColor)
    {
        if (spriteRenderer == null) yield break;

        Color original = spriteRenderer.color;
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(0.15f);
        spriteRenderer.color = original;
    }

    private void ExecuteReturnToPosition()
    {
        // Use OPPONENT team's FormationManager (not player team!)
        FormationManager opponentFormation = FormationManager.GetFormationManager(FormationManager.Team.Opponent);
        Vector3 targetPosition;

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

        Vector3 direction = PhysicsHelper.DirectionXZ(transform.position, targetPosition);
        float distanceToTarget = PhysicsHelper.DistanceXZ(transform.position, targetPosition);

        if (distanceToTarget > 1f)
        {
            rb.linearVelocity = direction * (moveSpeed * speedModifier);
        }
        else
        {
            // Close enough to target, slow down
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 5f * Time.deltaTime);
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

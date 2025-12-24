using UnityEngine;

/// <summary>
/// Handles defensive checking mechanics - poke checks, body checks, and stick lifts.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CheckingController : MonoBehaviour
{
    [Header("Poke Check Settings")]
    [Tooltip("Range for poke check in units")]
    [Range(1f, 5f)]
    [SerializeField] private float pokeCheckRange = 2f;

    [Tooltip("Success chance when puck is at max range")]
    [Range(0f, 1f)]
    [SerializeField] private float minSuccessChance = 0.3f;

    [Tooltip("Success chance when puck is very close")]
    [Range(0f, 1f)]
    [SerializeField] private float maxSuccessChance = 0.9f;

    [Header("Body Check Settings")]
    [Tooltip("Minimum charge time for body check (seconds)")]
    [Range(0.3f, 1f)]
    [SerializeField] private float minBodyCheckChargeTime = 0.5f;

    [Tooltip("Maximum charge time for body check (seconds)")]
    [Range(0.5f, 1.5f)]
    [SerializeField] private float maxBodyCheckChargeTime = 0.8f;

    [Tooltip("Knockback force applied to opponent")]
    [Range(10f, 50f)]
    [SerializeField] private float bodyCheckKnockbackForce = 30f;

    [Tooltip("Opponent stun duration (seconds)")]
    [Range(1f, 5f)]
    [SerializeField] private float opponentStunDuration = 2f;

    [Tooltip("Player recovery time after body check (seconds)")]
    [Range(0.3f, 1.5f)]
    [SerializeField] private float playerRecoveryTime = 0.5f;

    [Tooltip("Range for body check in units")]
    [Range(1f, 5f)]
    [SerializeField] private float bodyCheckRange = 2.5f;

    [Header("Perfect Timing Settings")]
    [Tooltip("Enable perfect timing for body checks")]
    [SerializeField] private bool enablePerfectTiming = true;

    [Tooltip("Distance to boards for glass hit (units)")]
    [Range(1f, 5f)]
    [SerializeField] private float glassHitDistance = 3f;

    [Header("Debug")]
    [SerializeField] private bool showCheckDebug = true;

    // Component references
    private Rigidbody2D playerRb;
    private Transform puckTransform;
    private Rigidbody2D puckRb;
    private PuckController puckController;
    private TimingMeter timingMeter;

    // State
    private Vector2 lastMoveDirection = Vector2.right;
    private bool isChargingBodyCheck = false;
    private float bodyCheckChargeStartTime = 0f;
    private bool isRecovering = false;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
            puckRb = puck.GetComponent<Rigidbody2D>();
            puckController = puck.GetComponent<PuckController>();
        }
        else
        {
            Debug.LogError("CheckingController: No Puck found! Tag your puck with 'Puck' tag.");
        }

        // Get or add TimingMeter component (shared with ShootingController and PassingController)
        timingMeter = GetComponent<TimingMeter>();
        if (timingMeter == null)
        {
            timingMeter = gameObject.AddComponent<TimingMeter>();
            Debug.Log("CheckingController: Added TimingMeter component");
        }

        // Subscribe to button manager CHECK events
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnCheckRequested += HandleCheckRequested;
            ContextButtonManager.Instance.OnCheckChargeStarted += HandleCheckChargeStarted;
            ContextButtonManager.Instance.OnCheckChargeEnded += HandleCheckChargeEnded;
        }
    }

    private void Update()
    {
        // Track movement direction for poke check direction
        if (InputManager.Instance != null)
        {
            Vector2 moveInput = InputManager.Instance.MoveInput;
            if (moveInput.magnitude > 0.1f)
            {
                lastMoveDirection = moveInput.normalized;
            }
        }

        // Update button color based on timing meter
        UpdateCheckButtonVisuals();
    }

    /// <summary>
    /// Update CHECK button color based on timing meter zone
    /// </summary>
    private void UpdateCheckButtonVisuals()
    {
        if (!isChargingBodyCheck || !enablePerfectTiming || timingMeter == null)
            return;

        // Get current zone and color
        TimingMeter.TimingResult currentZone = timingMeter.GetCurrentZone();
        Color zoneColor = timingMeter.GetZoneColor(currentZone);

        // Update button1 (CHECK button) color via ContextButtonManager
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.UpdateButton1Color(zoneColor);
        }
    }

    /// <summary>
    /// Handle check requests from button manager
    /// </summary>
    private void HandleCheckRequested(bool isCharged)
    {
        if (!isCharged)
        {
            // Tap = poke check
            ExecutePokeCheck();
        }
        // Note: Body check is now handled by charge events (HandleCheckChargeEnded)
    }

    /// <summary>
    /// Handle body check charge started
    /// </summary>
    private void HandleCheckChargeStarted()
    {
        if (isRecovering)
        {
            Debug.Log("Cannot start body check - still recovering");
            return;
        }

        isChargingBodyCheck = true;
        bodyCheckChargeStartTime = Time.time;

        // Start timing meter if enabled
        if (enablePerfectTiming && timingMeter != null)
        {
            timingMeter.StartCharging();
            Debug.Log("Body check charging started with timing meter!");
        }
        else
        {
            Debug.Log("Body check charging started!");
        }
    }

    /// <summary>
    /// Handle body check charge ended - execute the body check
    /// </summary>
    private void HandleCheckChargeEnded()
    {
        if (!isChargingBodyCheck) return;

        isChargingBodyCheck = false;
        float chargeTime = Time.time - bodyCheckChargeStartTime;

        Debug.Log($"Body check charge released after {chargeTime:F2}s");

        // Reset button color
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.ResetButton1Color();
        }

        // Check if charge time is within valid range
        if (chargeTime >= minBodyCheckChargeTime && chargeTime <= maxBodyCheckChargeTime)
        {
            // Get timing result if enabled
            TimingMeter.TimingResult timingResult = TimingMeter.TimingResult.Weak;
            float powerMultiplier = 1f;

            if (enablePerfectTiming && timingMeter != null)
            {
                timingResult = timingMeter.StopCharging();
                powerMultiplier = timingMeter.GetPowerMultiplier(timingResult);
                Debug.Log($"Timing result: {timingResult}, Power multiplier: {powerMultiplier}");
            }

            ExecuteBodyCheck(timingResult, powerMultiplier);
        }
        else if (chargeTime < minBodyCheckChargeTime)
        {
            Debug.Log($"Body check failed - not held long enough ({chargeTime:F2}s < {minBodyCheckChargeTime}s)");

            // Stop timing meter if active
            if (enablePerfectTiming && timingMeter != null)
            {
                timingMeter.StopCharging();
            }
        }
        else
        {
            Debug.Log($"Body check failed - held too long ({chargeTime:F2}s > {maxBodyCheckChargeTime}s)");

            // Stop timing meter and treat as overcharged miss
            if (enablePerfectTiming && timingMeter != null)
            {
                timingMeter.StopCharging();
            }

            // Execute miss/stumble
            ExecuteBodyCheckMiss();
        }
    }

    /// <summary>
    /// Execute poke check - quick stick poke to steal puck
    /// </summary>
    public void ExecutePokeCheck()
    {
        Debug.Log("=== POKE CHECK EXECUTED ===");

        if (puckTransform == null || puckRb == null)
        {
            Debug.LogWarning("Poke check failed: puckTransform or puckRb is null");
            return;
        }

        // Check if puck is already possessed by this player
        if (puckController != null && puckController.IsPossessed())
        {
            Debug.Log("Already have possession - can't poke check");
            return;
        }

        Debug.Log($"Puck position: {puckTransform.position}, Player position: {transform.position}");

        // Check if puck is within range of player (simple distance check)
        float distanceToPuck = Vector2.Distance(transform.position, puckTransform.position);
        bool puckInRange = distanceToPuck <= pokeCheckRange;

        Debug.Log($"Distance to puck: {distanceToPuck:F2} units, Range: {pokeCheckRange}, In range: {puckInRange}");

        if (puckInRange)
        {
            // Calculate success chance based on distance (closer = higher chance)
            float normalizedDistance = Mathf.Clamp01(distanceToPuck / pokeCheckRange);
            float successChance = Mathf.Lerp(maxSuccessChance, minSuccessChance, normalizedDistance);

            // Roll for success
            float roll = Random.Range(0f, 1f);

            if (roll <= successChance)
            {
                // SUCCESS - Knock puck toward player
                Vector2 toPuck = (puckTransform.position - transform.position).normalized;
                Vector2 knockDirection = -toPuck; // Knock puck away from opponent, toward player

                // Apply force to puck (strong enough to dislodge it)
                puckRb.linearVelocity = knockDirection * 12f;

                // Check if we hit an opponent and stun them (search nearby)
                Collider2D[] nearbyObjects = Physics2D.OverlapCircleAll(transform.position, pokeCheckRange);
                foreach (Collider2D obj in nearbyObjects)
                {
                    OpponentController opponent = obj.GetComponent<OpponentController>();
                    if (opponent != null)
                    {
                        opponent.ApplyPokeCheckStun();
                        break;
                    }

                    TeammateController teammate = obj.GetComponent<TeammateController>();
                    if (teammate != null)
                    {
                        // Could add teammate stun here too if needed
                        Debug.Log("Poke checked teammate!");
                        break;
                    }
                }

                Debug.Log($"POKE CHECK SUCCESS! ({successChance * 100:F0}% chance, rolled {roll:F2})");

                // TODO: Add sound effect (stick hitting puck)
                // TODO: Add visual feedback (puck spark effect)
            }
            else
            {
                // FAILED - Puck stays with opponent
                Debug.Log($"Poke check missed! ({successChance * 100:F0}% chance, rolled {roll:F2})");

                // TODO: Add whiff sound effect
            }
        }
        else
        {
            Debug.Log("Poke check - no puck in range");
            // TODO: Add whiff sound effect
        }
    }

    /// <summary>
    /// Execute body check - shoulder charge that knocks down opponents
    /// </summary>
    public void ExecuteBodyCheck(TimingMeter.TimingResult timingResult, float powerMultiplier)
    {
        Debug.Log($"=== BODY CHECK EXECUTED ({timingResult}) ===");

        // Find nearby opponents
        Collider2D[] nearbyObjects = Physics2D.OverlapCircleAll(transform.position, bodyCheckRange);
        bool hitOpponent = false;
        Transform hitOpponentTransform = null;

        foreach (Collider2D obj in nearbyObjects)
        {
            OpponentController opponent = obj.GetComponent<OpponentController>();
            if (opponent != null)
            {
                // Calculate knockback direction (away from player)
                Vector2 knockbackDirection = (opponent.transform.position - transform.position).normalized;

                // Calculate final knockback force based on timing
                float finalKnockbackForce = bodyCheckKnockbackForce * powerMultiplier;

                // Apply knockback force to opponent
                Rigidbody2D opponentRb = opponent.GetComponent<Rigidbody2D>();
                if (opponentRb != null)
                {
                    opponentRb.linearVelocity = knockbackDirection * finalKnockbackForce;
                    Debug.Log($"Applied {finalKnockbackForce:F1} knockback force to {opponent.name}");
                }

                // Stun opponent (perfect timing = guaranteed stun, weak = shorter stun)
                float stunDuration = timingResult == TimingMeter.TimingResult.Perfect ? opponentStunDuration : opponentStunDuration * 0.5f;
                opponent.ApplyBodyCheckStun(stunDuration);

                // Make puck pop loose if opponent had it
                if (puckTransform != null && puckRb != null)
                {
                    float distanceToPuck = Vector2.Distance(opponent.transform.position, puckTransform.position);
                    if (distanceToPuck <= opponent.possessionRadius)
                    {
                        // Pop puck in random direction (stronger with perfect timing)
                        Vector2 randomDirection = Random.insideUnitCircle.normalized;
                        float puckForce = timingResult == TimingMeter.TimingResult.Perfect ? 20f : 15f;
                        puckRb.linearVelocity = randomDirection * puckForce;
                        Debug.Log("Puck popped loose from opponent!");
                    }
                }

                hitOpponent = true;
                hitOpponentTransform = opponent.transform;
                Debug.Log($"BODY CHECK HIT {opponent.name}! Stunned for {stunDuration}s ({timingResult})");
                break; // Only hit one opponent
            }
        }

        if (!hitOpponent)
        {
            Debug.Log("Body check MISSED - no opponent in range");
        }
        else if (timingResult == TimingMeter.TimingResult.Perfect)
        {
            // Check for glass hit (opponent near boards)
            CheckForGlassHit(hitOpponentTransform);
        }

        // Enter recovery state for player (perfect timing = no recovery!)
        if (timingResult == TimingMeter.TimingResult.Perfect)
        {
            Debug.Log("PERFECT BODY CHECK! No recovery penalty!");
            // No recovery for perfect timing
        }
        else if (timingResult == TimingMeter.TimingResult.Weak)
        {
            Debug.Log("Weak body check - off-balance!");
            StartCoroutine(PlayerRecoveryCoroutine(playerRecoveryTime));
        }
        else // Overcharged
        {
            Debug.Log("Overcharged body check - stumble!");
            StartCoroutine(PlayerRecoveryCoroutine(playerRecoveryTime * 1.5f));
        }

        // TODO: Add impact sound effect
        // TODO: Add visual feedback (collision particles, screen shake)
    }

    /// <summary>
    /// Execute failed body check - miss/stumble
    /// </summary>
    private void ExecuteBodyCheckMiss()
    {
        Debug.Log("=== BODY CHECK MISS/STUMBLE ===");

        // Player stumbles with long recovery
        StartCoroutine(PlayerRecoveryCoroutine(1f)); // 1 second recovery penalty

        // TODO: Add stumble animation
        // TODO: Add whiff sound effect
    }

    /// <summary>
    /// Check if opponent hit the boards for a glass hit
    /// </summary>
    private void CheckForGlassHit(Transform opponentTransform)
    {
        if (opponentTransform == null) return;

        // Try to find all objects with "Board" tag
        GameObject[] boards = null;
        try
        {
            boards = GameObject.FindGameObjectsWithTag("Board");
        }
        catch (UnityEngine.UnityException)
        {
            // Board tag doesn't exist yet - will be added when rink is built
            return;
        }

        if (boards == null || boards.Length == 0)
        {
            // No boards in scene yet
            return;
        }

        // Check distance to nearest board
        float nearestDistance = float.MaxValue;
        foreach (GameObject board in boards)
        {
            float distance = Vector2.Distance(opponentTransform.position, board.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
            }
        }

        // Trigger glass hit if close to boards
        if (nearestDistance <= glassHitDistance)
        {
            Debug.Log($"GLASS HIT! Opponent hit boards at {nearestDistance:F2} units away!");
            StartCoroutine(GlassHitEffect());
        }
    }

    /// <summary>
    /// Glass hit visual effect - screen shake, slow-mo, etc.
    /// </summary>
    private System.Collections.IEnumerator GlassHitEffect()
    {
        Debug.Log("=== GLASS HIT EFFECT ===");
        Debug.Log("Screen shake, glass crack, crowd 'OHHH!'");

        // TODO: Add screen shake
        // TODO: Add glass crack particle effect
        // TODO: Add crowd 'OHHH!' sound effect

        // Slow motion effect
        Time.timeScale = 0.5f;
        Debug.Log("Slow-mo activated (0.5x speed)");

        yield return new WaitForSecondsRealtime(0.5f); // Use real-time for slow-mo duration

        // Restore normal speed
        Time.timeScale = 1f;
        Debug.Log("Slow-mo ended");
    }

    /// <summary>
    /// Player recovery state after body check
    /// </summary>
    private System.Collections.IEnumerator PlayerRecoveryCoroutine(float recoveryDuration)
    {
        isRecovering = true;
        Debug.Log($"Player entering recovery state for {recoveryDuration}s");

        // Slow down player during recovery
        if (playerRb != null)
        {
            playerRb.linearVelocity *= 0.3f; // Reduce velocity to 30%
        }

        yield return new WaitForSeconds(recoveryDuration);

        isRecovering = false;
        Debug.Log("Player recovery complete");
    }

    /// <summary>
    /// Public property to check if player is recovering
    /// </summary>
    public bool IsRecovering => isRecovering;

    private void OnDrawGizmosSelected()
    {
        if (!showCheckDebug) return;

        // Draw poke check range (circle around player)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, pokeCheckRange);

        // Draw body check range (larger circle)
        Gizmos.color = isChargingBodyCheck ? Color.yellow : Color.magenta;
        Gizmos.DrawWireSphere(transform.position, bodyCheckRange);

        // Draw direction indicator
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, lastMoveDirection * pokeCheckRange);
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnCheckRequested -= HandleCheckRequested;
            ContextButtonManager.Instance.OnCheckChargeStarted -= HandleCheckChargeStarted;
            ContextButtonManager.Instance.OnCheckChargeEnded -= HandleCheckChargeEnded;
        }
    }
}

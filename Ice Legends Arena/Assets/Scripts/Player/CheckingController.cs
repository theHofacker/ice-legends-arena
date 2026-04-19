using UnityEngine;

/// <summary>
/// Handles defensive checking mechanics - poke checks, body checks, and stick lifts.
/// 3D physics: all distance/direction calculations on XZ plane.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
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
    public float checkForce = 30f; // Made public for CharacterStatsApplier

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
    private Rigidbody playerRb;
    private Transform puckTransform;
    private Rigidbody puckRb;
    private PuckController puckController;
    private TimingMeter timingMeter;

    // State
    private Vector3 lastMoveDirection = Vector3.right;
    private bool isChargingBodyCheck = false;
    private float bodyCheckChargeStartTime = 0f;
    private bool isRecovering = false;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
            puckRb = puck.GetComponent<Rigidbody>();
            puckController = puck.GetComponent<PuckController>();
        }
        else
        {
            Debug.LogError("CheckingController: No Puck found! Tag your puck with 'Puck' tag.");
        }

        // Get or add TimingMeter component
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
            ContextButtonManager.Instance.OnFakeCheckRequested += HandleFakeCheckRequested;
        }
    }

    private void Update()
    {
        // Track movement direction for poke check direction
        if (InputManager.Instance != null)
        {
            Vector3 moveInput = PhysicsHelper.InputToWorld(InputManager.Instance.MoveInput);
            if (moveInput.magnitude > 0.1f)
            {
                lastMoveDirection = moveInput.normalized;
            }
        }

        UpdateCheckButtonVisuals();
    }

    private void UpdateCheckButtonVisuals()
    {
        if (!isChargingBodyCheck || !enablePerfectTiming || timingMeter == null)
            return;

        TimingMeter.TimingResult currentZone = timingMeter.GetCurrentZone();
        Color zoneColor = timingMeter.GetZoneColor(currentZone);

        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.UpdateButton1Color(zoneColor);
        }
    }

    private void HandleCheckRequested(bool isCharged)
    {
        if (!isCharged)
        {
            ExecutePokeCheck();
        }
    }

    private void HandleFakeCheckRequested()
    {
        if (isChargingBodyCheck)
        {
            isChargingBodyCheck = false;

            if (enablePerfectTiming && timingMeter != null)
            {
                timingMeter.StopCharging();
            }

            if (ContextButtonManager.Instance != null)
            {
                ContextButtonManager.Instance.ResetButton1Color();
            }
        }

        ExecuteFakeCheck();
    }

    private void HandleCheckChargeStarted()
    {
        if (isRecovering)
        {
            Debug.Log("Cannot start body check - still recovering");
            return;
        }

        isChargingBodyCheck = true;
        bodyCheckChargeStartTime = Time.time;

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

    private void HandleCheckChargeEnded()
    {
        if (!isChargingBodyCheck) return;

        isChargingBodyCheck = false;
        float chargeTime = Time.time - bodyCheckChargeStartTime;

        Debug.Log($"Body check charge released after {chargeTime:F2}s");

        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.ResetButton1Color();
        }

        if (chargeTime >= minBodyCheckChargeTime && chargeTime <= maxBodyCheckChargeTime)
        {
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

            if (enablePerfectTiming && timingMeter != null)
            {
                timingMeter.StopCharging();
            }
        }
        else
        {
            Debug.Log($"Body check failed - held too long ({chargeTime:F2}s > {maxBodyCheckChargeTime}s)");

            if (enablePerfectTiming && timingMeter != null)
            {
                timingMeter.StopCharging();
            }

            ExecuteBodyCheckMiss();
        }
    }

    public void ExecutePokeCheck()
    {
        Debug.Log("=== POKE CHECK EXECUTED ===");

        if (puckTransform == null || puckRb == null)
        {
            Debug.LogWarning("Poke check failed: puckTransform or puckRb is null");
            return;
        }

        if (puckController != null && puckController.IsPossessed())
        {
            Debug.Log("Already have possession - can't poke check");
            return;
        }

        float distanceToPuck = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);
        bool puckInRange = distanceToPuck <= pokeCheckRange;

        Debug.Log($"Distance to puck: {distanceToPuck:F2} units, Range: {pokeCheckRange}, In range: {puckInRange}");

        if (puckInRange)
        {
            float normalizedDistance = Mathf.Clamp01(distanceToPuck / pokeCheckRange);
            float successChance = Mathf.Lerp(maxSuccessChance, minSuccessChance, normalizedDistance);

            float roll = Random.Range(0f, 1f);

            if (roll <= successChance)
            {
                // SUCCESS - Knock puck toward player
                Vector3 toPuck = PhysicsHelper.DirectionXZ(transform.position, puckTransform.position);
                Vector3 knockDirection = -toPuck;

                puckRb.linearVelocity = knockDirection * 12f;

                // Check for nearby opponents using 3D OverlapSphere
                Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, pokeCheckRange);
                foreach (Collider obj in nearbyObjects)
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
                        Debug.Log("Poke checked teammate!");
                        break;
                    }
                }

                Debug.Log($"POKE CHECK SUCCESS! ({successChance * 100:F0}% chance, rolled {roll:F2})");
            }
            else
            {
                Debug.Log($"Poke check missed! ({successChance * 100:F0}% chance, rolled {roll:F2})");
            }
        }
        else
        {
            Debug.Log("Poke check - no puck in range");
        }
    }

    public void ExecuteBodyCheck(TimingMeter.TimingResult timingResult, float powerMultiplier)
    {
        Debug.Log($"=== BODY CHECK EXECUTED ({timingResult}) ===");

        PlayerAnimator playerAnimator = GetComponent<PlayerAnimator>();
        if (playerAnimator != null)
        {
            playerAnimator.TriggerBodyCheck();
        }

        RampageCheckModifier rampageModifier = GetComponent<RampageCheckModifier>();
        bool isRampaging = rampageModifier != null && rampageModifier.IsActive;

        if (isRampaging)
        {
            Debug.Log("RAMPAGE MODE ACTIVE - Enhanced body check!");
        }

        // Find nearby opponents using 3D OverlapSphere
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, bodyCheckRange);
        bool hitOpponent = false;
        Transform hitOpponentTransform = null;

        foreach (Collider obj in nearbyObjects)
        {
            OpponentController opponent = obj.GetComponent<OpponentController>();
            if (opponent != null)
            {
                // Knockback on XZ plane
                Vector3 knockbackDirection = PhysicsHelper.DirectionXZ(transform.position, opponent.transform.position);

                float finalKnockbackForce = checkForce * powerMultiplier;
                if (isRampaging)
                {
                    finalKnockbackForce *= rampageModifier.CheckMultiplier;
                }

                Rigidbody opponentRb = opponent.GetComponent<Rigidbody>();
                if (opponentRb != null)
                {
                    opponentRb.linearVelocity = knockbackDirection * finalKnockbackForce;
                    Debug.Log($"Applied {finalKnockbackForce:F1} knockback force to {opponent.name}{(isRampaging ? " (RAMPAGE!)" : "")}");
                }

                float stunDuration;
                if (isRampaging)
                {
                    stunDuration = rampageModifier.StunDuration;
                    Debug.Log($"RAMPAGE STUN: {stunDuration}s guaranteed!");
                }
                else
                {
                    stunDuration = timingResult == TimingMeter.TimingResult.Perfect ? opponentStunDuration : opponentStunDuration * 0.5f;
                }
                opponent.ApplyBodyCheckStun(stunDuration);

                // Make puck pop loose if opponent had it
                if (puckTransform != null && puckRb != null)
                {
                    float distanceToPuck = PhysicsHelper.DistanceXZ(opponent.transform.position, puckTransform.position);
                    if (distanceToPuck <= opponent.possessionRadius)
                    {
                        // Random direction on XZ plane
                        Vector3 randomDirection = PhysicsHelper.RandomDirectionXZ();
                        float puckForce = timingResult == TimingMeter.TimingResult.Perfect ? 20f : 15f;
                        puckRb.linearVelocity = randomDirection * puckForce;
                        Debug.Log("Puck popped loose from opponent!");
                    }
                }

                hitOpponent = true;
                hitOpponentTransform = opponent.transform;

                if (PuckFollowCamera.Instance != null)
                {
                    PuckFollowCamera.Instance.TriggerBodyCheckShake();
                }

                Debug.Log($"BODY CHECK HIT {opponent.name}! Stunned for {stunDuration}s ({timingResult})");
                break;
            }
        }

        if (!hitOpponent)
        {
            Debug.Log("Body check MISSED - no opponent in range");
        }
        else if (timingResult == TimingMeter.TimingResult.Perfect)
        {
            CheckForGlassHit(hitOpponentTransform);
        }

        if (timingResult == TimingMeter.TimingResult.Perfect)
        {
            Debug.Log("PERFECT BODY CHECK! No recovery penalty!");
        }
        else if (timingResult == TimingMeter.TimingResult.Weak)
        {
            Debug.Log("Weak body check - off-balance!");
            StartCoroutine(PlayerRecoveryCoroutine(playerRecoveryTime));
        }
        else
        {
            Debug.Log("Overcharged body check - stumble!");
            StartCoroutine(PlayerRecoveryCoroutine(playerRecoveryTime * 1.5f));
        }
    }

    private void ExecuteBodyCheckMiss()
    {
        Debug.Log("=== BODY CHECK MISS/STUMBLE ===");
        StartCoroutine(PlayerRecoveryCoroutine(1f));
    }

    private void ExecuteFakeCheck()
    {
        Debug.Log("=== FAKE CHECK EXECUTED ===");

        if (isRecovering)
        {
            Debug.Log("Cannot fake check - still recovering");
            return;
        }

        if (playerRb != null)
        {
            Vector3 lungeDirection = lastMoveDirection.magnitude > 0.1f ? lastMoveDirection : Vector3.right;
            lungeDirection = PhysicsHelper.FlattenY(lungeDirection).normalized;

            float lungeForce = 2.5f;
            playerRb.linearVelocity += lungeDirection * lungeForce;

            Debug.Log($"Fake check lunge: {lungeDirection} with velocity boost {lungeForce}");
        }

        Debug.Log("Fake check - no collision, instant recovery!");
    }

    private void CheckForGlassHit(Transform opponentTransform)
    {
        if (opponentTransform == null) return;

        GameObject[] boards = null;
        try
        {
            boards = GameObject.FindGameObjectsWithTag("Board");
        }
        catch (UnityEngine.UnityException)
        {
            return;
        }

        if (boards == null || boards.Length == 0)
        {
            return;
        }

        float nearestDistance = float.MaxValue;
        foreach (GameObject board in boards)
        {
            float distance = PhysicsHelper.DistanceXZ(opponentTransform.position, board.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
            }
        }

        if (nearestDistance <= glassHitDistance)
        {
            Debug.Log($"GLASS HIT! Opponent hit boards at {nearestDistance:F2} units away!");
            StartCoroutine(GlassHitEffect());
        }
    }

    private System.Collections.IEnumerator GlassHitEffect()
    {
        Debug.Log("=== GLASS HIT EFFECT ===");
        Debug.Log("Screen shake, glass crack, crowd 'OHHH!'");

        Time.timeScale = 0.5f;
        Debug.Log("Slow-mo activated (0.5x speed)");

        yield return new WaitForSecondsRealtime(0.5f);

        Time.timeScale = 1f;
        Debug.Log("Slow-mo ended");
    }

    private System.Collections.IEnumerator PlayerRecoveryCoroutine(float recoveryDuration)
    {
        isRecovering = true;
        Debug.Log($"Player entering recovery state for {recoveryDuration}s");

        if (playerRb != null)
        {
            playerRb.linearVelocity *= 0.3f;
        }

        yield return new WaitForSeconds(recoveryDuration);

        isRecovering = false;
        Debug.Log("Player recovery complete");
    }

    public bool IsRecovering => isRecovering;

    private void OnDrawGizmosSelected()
    {
        if (!showCheckDebug) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, pokeCheckRange);

        Gizmos.color = isChargingBodyCheck ? Color.yellow : Color.magenta;
        Gizmos.DrawWireSphere(transform.position, bodyCheckRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, lastMoveDirection * pokeCheckRange);
    }

    private void OnDestroy()
    {
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnCheckRequested -= HandleCheckRequested;
            ContextButtonManager.Instance.OnCheckChargeStarted -= HandleCheckChargeStarted;
            ContextButtonManager.Instance.OnCheckChargeEnded -= HandleCheckChargeEnded;
            ContextButtonManager.Instance.OnFakeCheckRequested -= HandleFakeCheckRequested;
        }
    }
}

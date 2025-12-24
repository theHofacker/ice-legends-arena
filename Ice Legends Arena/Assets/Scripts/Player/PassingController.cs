using UnityEngine;

/// <summary>
/// Handles passing mechanics - basic pass, saucer pass, and one-timers.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PassingController : MonoBehaviour
{
    [Header("Pass Power Settings")]
    [Tooltip("Power for basic tap pass")]
    [Range(5f, 30f)]
    [SerializeField] private float basicPassPower = 20f;

    [Tooltip("Power multiplier for saucer pass (hold)")]
    [Range(1.2f, 2.5f)]
    [SerializeField] private float saucerPassMultiplier = 1.5f;

    [Tooltip("Vertical force for saucer pass arc")]
    [Range(5f, 25f)]
    [SerializeField] private float saucerArcHeight = 12f;

    [Header("Targeting Settings")]
    [Tooltip("Maximum angle to search for teammates (forward cone)")]
    [Range(0f, 180f)]
    [SerializeField] private float maxPassAngle = 180f;

    [Tooltip("Maximum distance to pass")]
    [Range(5f, 30f)]
    [SerializeField] private float maxPassDistance = 25f;

    [Header("Possession Settings")]
    [Tooltip("Distance to consider player 'has' the puck")]
    [Range(0.5f, 3f)]
    [SerializeField] private float possessionRadius = 1.5f;

    [Header("Saucer Pass Settings")]
    [Tooltip("Enable perfect timing for saucer passes")]
    [SerializeField] private bool enableSaucerTiming = true;

    [Tooltip("How long puck ignores player collision during saucer pass")]
    [Range(0.2f, 2f)]
    [SerializeField] private float saucerFlightDuration = 0.8f;

    [Header("One-Timer Settings")]
    [Tooltip("Enable one-timer shots (tap SHOOT after passing)")]
    [SerializeField] private bool enableOneTimer = true;

    [Tooltip("Time window after pass to trigger one-timer")]
    [Range(0.1f, 3f)]
    [SerializeField] private float oneTimerWindow = 1.0f;

    [Tooltip("Power multiplier for one-timer shots (+50% bonus)")]
    [Range(1.2f, 2f)]
    [SerializeField] private float oneTimerPowerMultiplier = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool showPassDebug = true;

    // Component references
    private Rigidbody2D playerRb;
    private Transform puckTransform;
    private Rigidbody2D puckRb;
    private TimingMeter timingMeter;

    // State
    private Vector2 lastMoveDirection = Vector2.right;
    private bool isChargingSaucerPass = false;
    private Transform lastPassTarget = null;
    private float lastPassTime = -999f;
    private float lastFakePassTime = -999f; // For cooldown tracking
    private float fakePassCooldown = 3f; // 3 second cooldown between fakes

    // Public properties
    public bool IsChargingSaucerPass => isChargingSaucerPass;

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
        }
        else
        {
            Debug.LogError("PassingController: No Puck found! Tag your puck with 'Puck' tag.");
        }

        // Get or add TimingMeter component (shared with ShootingController)
        timingMeter = GetComponent<TimingMeter>();
        if (timingMeter == null)
        {
            timingMeter = gameObject.AddComponent<TimingMeter>();
            Debug.Log("PassingController: Added TimingMeter component");
        }

        // Subscribe to button manager
        ContextButtonManager.Instance.OnPassRequested += HandlePassRequested;
        ContextButtonManager.Instance.OnPassChargeStarted += StartChargingSaucerPass;
        ContextButtonManager.Instance.OnPassChargeEnded += StopChargingSaucerPass;
        ContextButtonManager.Instance.OnFakePassRequested += HandleFakePassRequested;
        ContextButtonManager.Instance.OnShootRequested += HandleShootForOneTimer;
        ContextButtonManager.Instance.OnCheckRequested += HandleCheckForOneTimer; // Also listen for CHECK button (in Defense mode)
    }

    private void Update()
    {
        // Track movement direction for passing
        if (InputManager.Instance != null)
        {
            Vector2 moveInput = InputManager.Instance.MoveInput;
            if (moveInput.magnitude > 0.1f)
            {
                lastMoveDirection = moveInput.normalized;
            }
        }
    }

    private void StartChargingSaucerPass()
    {
        if (enableSaucerTiming && timingMeter != null && HasPossession())
        {
            isChargingSaucerPass = true;
            timingMeter.StartCharging();
            Debug.Log("Saucer pass charging started!");
        }
    }

    private void StopChargingSaucerPass()
    {
        if (!isChargingSaucerPass) return;

        isChargingSaucerPass = false;

        // Stop the timing meter and get result
        if (enableSaucerTiming && timingMeter != null)
        {
            TimingMeter.TimingResult result = timingMeter.StopCharging();
            float powerMultiplier = timingMeter.GetPowerMultiplier(result);

            // Execute saucer pass with timing multiplier
            ExecuteSaucerPass(powerMultiplier, result);
        }
    }

    private void HandlePassRequested(bool isCharged)
    {
        // Tap = basic pass, Hold = saucer pass (handled by charge events)
        if (!isCharged && HasPossession())
        {
            ExecuteBasicPass();
        }
    }

    /// <summary>
    /// Handle fake pass request (swipe off)
    /// </summary>
    private void HandleFakePassRequested()
    {
        // Cancel any active saucer pass charging
        if (isChargingSaucerPass)
        {
            isChargingSaucerPass = false;

            // Stop timing meter
            if (enableSaucerTiming && timingMeter != null)
            {
                timingMeter.StopCharging();
            }
        }

        // Execute fake pass
        ExecuteFakePass();
    }

    /// <summary>
    /// Execute fake pass - wind up without actually passing
    /// </summary>
    private void ExecuteFakePass()
    {
        // Check cooldown
        float timeSinceLastFake = Time.time - lastFakePassTime;
        if (timeSinceLastFake < fakePassCooldown)
        {
            float remainingCooldown = fakePassCooldown - timeSinceLastFake;
            Debug.LogWarning($"Fake pass on cooldown! {remainingCooldown:F1}s remaining");
            return;
        }

        // Wind-up motion: subtle movement in pass direction to sell the fake
        Vector2 windUpDirection = lastMoveDirection.magnitude > 0.1f ? lastMoveDirection : Vector2.right;

        // Apply subtle wind-up force (similar to fake check but in pass direction)
        float windUpForce = 2.5f; // Subtle forward movement to sell the fake
        playerRb.linearVelocity += windUpDirection * windUpForce;

        // Keep possession - don't touch the puck (that's the point of a fake!)

        // Update cooldown
        lastFakePassTime = Time.time;

        Debug.Log($"FAKE PASS! Wind-up in direction {windUpDirection}. Cooldown: {fakePassCooldown}s");

        // TODO: AI defenders will react to this (future feature)
    }

    private void HandleShootForOneTimer(bool isCharged)
    {
        // Only trigger one-timer on tap (not charged shots)
        if (!isCharged && enableOneTimer)
        {
            TryArmOneTimer();
        }
    }

    private void HandleCheckForOneTimer(bool isCharged)
    {
        // When player taps CHECK after passing (in Defense mode), try to arm one-timer instead
        if (!isCharged && enableOneTimer)
        {
            float timeSincePass = Time.time - lastPassTime;

            // Only intercept CHECK taps if within one-timer window
            if (lastPassTarget != null && timeSincePass <= oneTimerWindow)
            {
                TryArmOneTimer();
            }
        }
    }

    private void TryArmOneTimer()
    {
        // Check if within one-timer window after a pass
        float timeSincePass = Time.time - lastPassTime;

        if (lastPassTarget != null && timeSincePass <= oneTimerWindow)
        {
            // Arm the teammate for a one-timer
            TeammateController teammate = lastPassTarget.GetComponent<TeammateController>();
            if (teammate != null)
            {
                teammate.ArmOneTimer(oneTimerPowerMultiplier);
                Debug.Log($"ONE-TIMER ARMED for {lastPassTarget.name}! (Time since pass: {timeSincePass:F2}s)");
            }
        }
    }

    private void ExecuteBasicPass()
    {
        // Find nearest teammate
        Transform targetTeammate = FindNearestTeammate();

        if (targetTeammate != null)
        {
            // Calculate direction to teammate
            Vector2 passDirection = (targetTeammate.position - transform.position).normalized;

            // Apply pass force to puck
            ApplyPassForce(passDirection, basicPassPower);

            // Track pass for one-timer setup
            lastPassTarget = targetTeammate;
            lastPassTime = Time.time;

            Debug.Log($"Pass to {targetTeammate.name}! Power: {basicPassPower}");
        }
        else
        {
            Debug.LogWarning("No teammate found to pass to!");
        }
    }

    private void ExecuteSaucerPass(float powerMultiplier, TimingMeter.TimingResult result)
    {
        if (!HasPossession()) return;

        // Find nearest teammate
        Transform targetTeammate = FindNearestTeammate();

        if (targetTeammate != null)
        {
            // Calculate direction to teammate
            Vector2 passDirection = (targetTeammate.position - transform.position).normalized;

            // Calculate pass power with timing multiplier
            float basePower = basicPassPower * saucerPassMultiplier;
            float finalPower = basePower * powerMultiplier;

            // Apply arc trajectory (horizontal + vertical)
            ApplySaucerPassForce(passDirection, finalPower);

            // Disable puck collision with players during flight
            StartCoroutine(DisablePlayerCollisionDuringFlight());

            // Track pass for one-timer setup
            lastPassTarget = targetTeammate;
            lastPassTime = Time.time;

            string resultText = result == TimingMeter.TimingResult.Perfect ? "PERFECT" :
                               result == TimingMeter.TimingResult.Weak ? "WEAK" : "OVERCHARGED";

            Debug.Log($"SAUCER PASS ({resultText}) to {targetTeammate.name}! Power: {finalPower}");
        }
        else
        {
            Debug.LogWarning("No teammate found for saucer pass!");
        }
    }

    private Transform FindNearestTeammate()
    {
        // Find all GameObjects with TeammateController component
        TeammateController[] teammates = FindObjectsOfType<TeammateController>();

        Transform nearestTeammate = null;
        float nearestDistance = maxPassDistance;

        foreach (TeammateController teammate in teammates)
        {
            // Skip if TeammateController is disabled (currently controlled player)
            if (!teammate.enabled || !teammate.isAI)
            {
                continue;
            }

            Vector2 toTeammate = teammate.transform.position - transform.position;
            float distance = toTeammate.magnitude;

            // Check if within max distance
            if (distance > maxPassDistance) continue;

            // Check if within forward cone
            float angle = Vector2.Angle(lastMoveDirection, toTeammate);
            if (angle > maxPassAngle) continue;

            // Check if closer than current nearest
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTeammate = teammate.transform;
            }
        }

        return nearestTeammate;
    }

    private void ApplyPassForce(Vector2 direction, float power)
    {
        if (puckRb == null) return;

        // Apply impulse force to puck
        puckRb.linearVelocity = Vector2.zero; // Reset current velocity
        puckRb.AddForce(direction * power, ForceMode2D.Impulse);
    }

    private void ApplySaucerPassForce(Vector2 direction, float power)
    {
        if (puckRb == null) return;

        // Reset current velocity
        puckRb.linearVelocity = Vector2.zero;

        // Apply horizontal force (toward teammate)
        puckRb.AddForce(direction * power, ForceMode2D.Impulse);

        // Apply vertical force for arc (perpendicular to direction)
        // In 2D, we simulate "height" by moving the puck along an arc path
        // Use a coroutine to apply upward then downward force over time
        StartCoroutine(ApplyArcTrajectory(direction, power));
    }

    private System.Collections.IEnumerator ApplyArcTrajectory(Vector2 direction, float power)
    {
        float elapsed = 0f;
        float arcDuration = 0.5f; // How long the arc takes

        Vector2 perpendicular = new Vector2(-direction.y, direction.x); // Perpendicular to pass direction

        while (elapsed < arcDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / arcDuration;

            // Parabolic arc: up then down (using sin curve)
            float arcForce = Mathf.Sin(normalizedTime * Mathf.PI) * saucerArcHeight;

            // Apply arc force perpendicular to pass direction
            if (puckRb != null)
            {
                puckRb.AddForce(perpendicular * arcForce * Time.deltaTime * 60f, ForceMode2D.Force);
            }

            yield return null;
        }
    }

    private System.Collections.IEnumerator DisablePlayerCollisionDuringFlight()
    {
        // Find all players (including teammates) and disable collision with puck
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        TeammateController[] teammates = FindObjectsOfType<TeammateController>();

        Collider2D puckCollider = puckTransform?.GetComponent<Collider2D>();
        if (puckCollider == null) yield break;

        // Disable collision with all players
        foreach (GameObject player in players)
        {
            Collider2D playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider != null)
            {
                Physics2D.IgnoreCollision(puckCollider, playerCollider, true);
            }
        }

        // Disable collision with all teammates
        foreach (TeammateController teammate in teammates)
        {
            Collider2D teammateCollider = teammate.GetComponent<Collider2D>();
            if (teammateCollider != null)
            {
                Physics2D.IgnoreCollision(puckCollider, teammateCollider, true);
            }
        }

        Debug.Log($"Puck collision disabled for {saucerFlightDuration}s (saucer pass flight)");

        // Wait for flight duration
        yield return new WaitForSeconds(saucerFlightDuration);

        // Re-enable collision
        foreach (GameObject player in players)
        {
            Collider2D playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider != null)
            {
                Physics2D.IgnoreCollision(puckCollider, playerCollider, false);
            }
        }

        foreach (TeammateController teammate in teammates)
        {
            Collider2D teammateCollider = teammate.GetComponent<Collider2D>();
            if (teammateCollider != null)
            {
                Physics2D.IgnoreCollision(puckCollider, teammateCollider, false);
            }
        }

        Debug.Log("Puck collision re-enabled");
    }

    private bool HasPossession()
    {
        if (puckTransform == null) return false;

        float distance = Vector2.Distance(transform.position, puckTransform.position);
        return distance <= possessionRadius;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showPassDebug) return;

        // Draw possession radius
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        // Draw pass cone
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Vector3 forward = lastMoveDirection * maxPassDistance;
            Gizmos.DrawRay(transform.position, forward);

            // Draw nearest teammate connection
            Transform nearest = FindNearestTeammate();
            if (nearest != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, nearest.position);
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnPassRequested -= HandlePassRequested;
            ContextButtonManager.Instance.OnPassChargeStarted -= StartChargingSaucerPass;
            ContextButtonManager.Instance.OnPassChargeEnded -= StopChargingSaucerPass;
            ContextButtonManager.Instance.OnFakePassRequested -= HandleFakePassRequested;
            ContextButtonManager.Instance.OnShootRequested -= HandleShootForOneTimer;
            ContextButtonManager.Instance.OnCheckRequested -= HandleCheckForOneTimer;
        }
    }
}

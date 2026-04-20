using UnityEngine;

/// <summary>
/// Handles passing mechanics - basic pass, saucer pass, and one-timers.
/// 3D physics: passes on XZ plane, saucer passes use real Y-axis arc with gravity.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PassingController : MonoBehaviour
{
    [Header("Pass Power Settings")]
    [Tooltip("Power for basic tap pass")]
    [Range(5f, 30f)]
    public float passPower = 20f; // Made public for CharacterStatsApplier

    [Tooltip("Power for saucer pass")]
    [Range(1.2f, 2.5f)]
    public float saucerPassPower = 1.5f; // Made public for CharacterStatsApplier

    [Tooltip("Upward force for saucer pass arc (Y-axis lift)")]
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
    [SerializeField] private float possessionRadius = 3.0f;

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
    private Rigidbody playerRb;
    private Transform puckTransform;
    private Rigidbody puckRb;
    private TimingMeter timingMeter;

    // State
    private Vector3 lastMoveDirection = Vector3.right;
    private bool isChargingSaucerPass = false;
    private Transform lastPassTarget = null;
    private float lastPassTime = -999f;
    private float lastFakePassTime = -999f;
    private float fakePassCooldown = 3f;

    // Public properties
    public bool IsChargingSaucerPass => isChargingSaucerPass;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();
        // Force possession radius (scene serialization may have old 2D values)
        possessionRadius = 3.0f;
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
        else
        {
            Debug.LogError("PassingController: No Puck found! Tag your puck with 'Puck' tag.");
        }

        // Get or add TimingMeter component
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
        ContextButtonManager.Instance.OnCheckRequested += HandleCheckForOneTimer;
    }

    private void Update()
    {
        // Track movement direction for passing
        if (InputManager.Instance != null)
        {
            Vector3 moveInput = PhysicsHelper.InputToWorld(InputManager.Instance.MoveInput);
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

        if (enableSaucerTiming && timingMeter != null)
        {
            TimingMeter.TimingResult result = timingMeter.StopCharging();
            float powerMultiplier = timingMeter.GetPowerMultiplier(result);
            ExecuteSaucerPass(powerMultiplier, result);
        }
    }

    private void HandlePassRequested(bool isCharged)
    {
        if (!isCharged && HasPossession())
        {
            ExecuteBasicPass();
        }
    }

    private void HandleFakePassRequested()
    {
        if (isChargingSaucerPass)
        {
            isChargingSaucerPass = false;

            if (enableSaucerTiming && timingMeter != null)
            {
                timingMeter.StopCharging();
            }
        }

        ExecuteFakePass();
    }

    private void ExecuteFakePass()
    {
        float timeSinceLastFake = Time.time - lastFakePassTime;
        if (timeSinceLastFake < fakePassCooldown)
        {
            float remainingCooldown = fakePassCooldown - timeSinceLastFake;
            Debug.LogWarning($"Fake pass on cooldown! {remainingCooldown:F1}s remaining");
            return;
        }

        Vector3 windUpDirection = lastMoveDirection.magnitude > 0.1f ? lastMoveDirection : Vector3.right;
        windUpDirection = PhysicsHelper.FlattenY(windUpDirection).normalized;

        float windUpForce = 2.5f;
        playerRb.linearVelocity += windUpDirection * windUpForce;

        lastFakePassTime = Time.time;

        Debug.Log($"FAKE PASS! Wind-up in direction {windUpDirection}. Cooldown: {fakePassCooldown}s");
    }

    private void HandleShootForOneTimer(bool isCharged)
    {
        if (!isCharged && enableOneTimer)
        {
            TryArmOneTimer();
        }
    }

    private void HandleCheckForOneTimer(bool isCharged)
    {
        if (!isCharged && enableOneTimer)
        {
            float timeSincePass = Time.time - lastPassTime;
            if (lastPassTarget != null && timeSincePass <= oneTimerWindow)
            {
                TryArmOneTimer();
            }
        }
    }

    private void TryArmOneTimer()
    {
        float timeSincePass = Time.time - lastPassTime;

        if (lastPassTarget != null && timeSincePass <= oneTimerWindow)
        {
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
        Transform targetTeammate = FindNearestTeammate();

        if (targetTeammate != null)
        {
            Vector3 passDirection = PhysicsHelper.DirectionXZ(transform.position, targetTeammate.position);

            ApplyPassForce(passDirection, passPower);

            PlayerAnimator playerAnimator = GetComponent<PlayerAnimator>();
            if (playerAnimator != null)
            {
                playerAnimator.TriggerPass();
            }

            lastPassTarget = targetTeammate;
            lastPassTime = Time.time;

            Debug.Log($"Pass to {targetTeammate.name}! Power: {passPower}");
        }
        else
        {
            Debug.LogWarning("No teammate found to pass to!");
        }
    }

    private void ExecuteSaucerPass(float powerMultiplier, TimingMeter.TimingResult result)
    {
        if (!HasPossession()) return;

        Transform targetTeammate = FindNearestTeammate();

        if (targetTeammate != null)
        {
            Vector3 passDirection = PhysicsHelper.DirectionXZ(transform.position, targetTeammate.position);

            float basePower = passPower * saucerPassPower;
            float finalPower = basePower * powerMultiplier;

            // In 3D, saucer pass is simple: apply XZ force + upward Y force, gravity does the rest
            ApplySaucerPassForce(passDirection, finalPower);

            // Disable puck collision with players during flight
            StartCoroutine(DisablePlayerCollisionDuringFlight());

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
        TeammateController[] teammates = FindObjectsByType<TeammateController>(FindObjectsSortMode.None);

        Transform nearestTeammate = null;
        float nearestDistance = maxPassDistance;

        foreach (TeammateController teammate in teammates)
        {
            if (!teammate.enabled || !teammate.isAI)
            {
                continue;
            }

            Vector3 toTeammate = teammate.transform.position - transform.position;
            toTeammate.y = 0f; // Ignore height difference
            float distance = toTeammate.magnitude;

            if (distance > maxPassDistance) continue;

            float angle = PhysicsHelper.AngleXZ(lastMoveDirection, toTeammate);
            if (angle > maxPassAngle) continue;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTeammate = teammate.transform;
            }
        }

        return nearestTeammate;
    }

    private void ApplyPassForce(Vector3 direction, float power)
    {
        if (puckRb == null) return;

        // Signal puck to release possession
        PuckController puckController = puckRb.GetComponent<PuckController>();
        if (puckController != null)
        {
            puckController.SignalShotFired();
        }

        puckRb.linearVelocity = Vector3.zero;
        Vector3 force = PhysicsHelper.FlattenY(direction).normalized * power;
        puckRb.AddForce(force, ForceMode.Impulse);
    }

    private void ApplySaucerPassForce(Vector3 direction, float power)
    {
        if (puckRb == null) return;

        // Signal puck to release possession
        PuckController puckController = puckRb.GetComponent<PuckController>();
        if (puckController != null)
        {
            puckController.SignalShotFired();
        }

        puckRb.linearVelocity = Vector3.zero;

        // Real 3D saucer pass: horizontal force + upward Y lift, gravity brings it down naturally
        Vector3 force = PhysicsHelper.FlattenY(direction).normalized * power;
        force.y = saucerArcHeight; // Add upward lift
        puckRb.AddForce(force, ForceMode.Impulse);
    }

    private System.Collections.IEnumerator DisablePlayerCollisionDuringFlight()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        TeammateController[] teammates = FindObjectsByType<TeammateController>(FindObjectsSortMode.None);

        Collider puckCollider = puckTransform?.GetComponent<Collider>();
        if (puckCollider == null) yield break;

        // Disable collision with all players
        foreach (GameObject player in players)
        {
            Collider playerCollider = player.GetComponent<Collider>();
            if (playerCollider != null)
            {
                Physics.IgnoreCollision(puckCollider, playerCollider, true);
            }
        }

        foreach (TeammateController teammate in teammates)
        {
            Collider teammateCollider = teammate.GetComponent<Collider>();
            if (teammateCollider != null)
            {
                Physics.IgnoreCollision(puckCollider, teammateCollider, true);
            }
        }

        Debug.Log($"Puck collision disabled for {saucerFlightDuration}s (saucer pass flight)");

        yield return new WaitForSeconds(saucerFlightDuration);

        // Re-enable collision
        foreach (GameObject player in players)
        {
            Collider playerCollider = player.GetComponent<Collider>();
            if (playerCollider != null)
            {
                Physics.IgnoreCollision(puckCollider, playerCollider, false);
            }
        }

        foreach (TeammateController teammate in teammates)
        {
            Collider teammateCollider = teammate.GetComponent<Collider>();
            if (teammateCollider != null)
            {
                Physics.IgnoreCollision(puckCollider, teammateCollider, false);
            }
        }

        Debug.Log("Puck collision re-enabled");
    }

    private bool HasPossession()
    {
        if (puckTransform == null) return false;

        float distance = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);
        return distance <= possessionRadius;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showPassDebug) return;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, lastMoveDirection * maxPassDistance);

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

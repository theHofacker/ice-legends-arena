using UnityEngine;

/// <summary>
/// Handles player shooting mechanics including wrist shots, slapshots, and auto-aim.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ShootingController : MonoBehaviour
{
    [Header("Shot Power Settings")]
    [Tooltip("Base power for wrist shot (tap)")]
    [Range(5f, 50f)]
    public float wristShotPower = 20f; // Made public for CharacterStatsApplier

    [Tooltip("Power multiplier for slapshot (hold)")]
    [Range(1.5f, 3f)]
    public float slapShotPower = 2f; // Made public for CharacterStatsApplier (renamed from slapshotMultiplier)

    [Header("Auto-Aim Settings")]
    [Tooltip("Enable auto-aim toward goal")]
    [SerializeField] private bool autoAimEnabled = true;

    [Tooltip("Auto-aim strength (0 = no aim assist, 1 = direct to goal)")]
    [Range(0f, 1f)]
    [SerializeField] private float autoAimStrength = 0.6f;

    [Tooltip("Maximum angle for auto-aim (degrees)")]
    [Range(0f, 90f)]
    [SerializeField] private float maxAutoAimAngle = 45f;

    [Header("Possession Settings")]
    [Tooltip("Distance to consider player 'has' the puck")]
    [Range(0.5f, 3f)]
    [SerializeField] private float possessionRadius = 1.5f;

    [Header("Aimed Shooting")]
    [Tooltip("Enable manual aiming with joystick during charge")]
    [SerializeField] private bool aimedShootingEnabled = true;

    [Tooltip("Minimum joystick input to register as manual aim")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float aimInputThreshold = 0.3f;

    [Tooltip("Maximum angle player can aim from facing direction (degrees)")]
    [Range(30f, 180f)]
    [SerializeField] private float maxAimAngle = 90f;

    [Tooltip("Movement speed multiplier while charging shot")]
    [Range(0f, 1f)]
    [SerializeField] private float chargingMovementMultiplier = 0.5f;

    [Header("Roof Shot (Top Shelf)")]
    [Tooltip("Enable roof shot by pulling joystick down while charging")]
    [SerializeField] private bool roofShotEnabled = true;

    [Tooltip("Angle threshold to trigger roof shot (degrees from forward)")]
    [Range(90f, 180f)]
    [SerializeField] private float roofShotAngleThreshold = 135f;

    [Tooltip("Power multiplier for roof shot")]
    [Range(0.8f, 1.5f)]
    [SerializeField] private float roofShotPowerMultiplier = 1.2f;

    [Header("Visual Feedback")]
    [SerializeField] private bool showAimDebug = true;
    [SerializeField] private LineRenderer aimIndicator;
    [SerializeField] private float aimIndicatorLength = 5f;

    // Component references
    private Rigidbody2D playerRb;
    private Transform puckTransform;
    private Rigidbody2D puckRb;
    private Transform nearestGoal;
    private TimingMeter timingMeter;

    // State
    private Vector2 lastMoveDirection = Vector2.right;
    private Vector2 facingDirectionAtChargeStart = Vector2.right; // Lock facing direction when charge starts
    private bool isChargingShot = false;
    private bool isManuallyAiming = false;
    private Vector2 manualAimDirection = Vector2.right;
    private bool isRoofShot = false;

    // Public properties for other components
    public bool IsChargingShot => isChargingShot;
    public float ChargingMovementMultiplier => chargingMovementMultiplier;
    public bool IsRoofShot => isRoofShot;

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
            Debug.LogError("ShootingController: No Puck found! Tag your puck with 'Puck' tag.");
        }

        // Find nearest goal (for now, just find any goal)
        // TODO: Determine which goal to shoot at based on team
        GameObject[] goals = GameObject.FindGameObjectsWithTag("Goal");
        if (goals.Length > 0)
        {
            nearestGoal = goals[0].transform;
            Debug.Log($"ShootingController: Aiming at goal: {nearestGoal.name}");
        }
        else
        {
            Debug.LogWarning("ShootingController: No goals found. Auto-aim disabled.");
        }

        // Get or add TimingMeter component
        timingMeter = GetComponent<TimingMeter>();
        if (timingMeter == null)
        {
            timingMeter = gameObject.AddComponent<TimingMeter>();
            Debug.Log("ShootingController: Added TimingMeter component");
        }

        // Subscribe to button manager
        ContextButtonManager.Instance.OnShootRequested += HandleShootRequested;
        ContextButtonManager.Instance.OnShotChargeStarted += StartChargingShot;
        ContextButtonManager.Instance.OnShotChargeEnded += StopChargingShot;
    }

    private void Update()
    {
        // Track movement direction for shooting
        if (InputManager.Instance != null)
        {
            Vector2 moveInput = InputManager.Instance.MoveInput;

            // Only update facing direction when NOT charging (so player doesn't turn around during roof shot)
            if (moveInput.magnitude > 0.1f && !isChargingShot)
            {
                lastMoveDirection = moveInput.normalized;
            }

            // Track manual aiming and roof shot during charge
            if (isChargingShot && aimedShootingEnabled)
            {
                if (moveInput.magnitude >= aimInputThreshold)
                {
                    // Player is aiming with joystick - clamp to valid angle
                    Vector2 desiredAim = moveInput.normalized;

                    // Constrain aim to cone relative to ORIGINAL facing direction (when charge started)
                    float angleToDesired = Vector2.SignedAngle(facingDirectionAtChargeStart, desiredAim);

                    // Check for ROOF SHOT: pulling joystick back/down (opposite of facing direction)
                    if (roofShotEnabled && Mathf.Abs(angleToDesired) >= roofShotAngleThreshold)
                    {
                        isRoofShot = true;
                        // Aim indicator shows roof shot (different color)
                        if (aimIndicator != null)
                        {
                            aimIndicator.startColor = Color.magenta;
                            aimIndicator.endColor = Color.magenta;
                        }
                        Debug.Log("ROOF SHOT armed! Pull back detected.");
                    }
                    else
                    {
                        isRoofShot = false;

                        if (Mathf.Abs(angleToDesired) > maxAimAngle)
                        {
                            // Clamp to max angle
                            float clampedAngle = Mathf.Sign(angleToDesired) * maxAimAngle;
                            float radians = clampedAngle * Mathf.Deg2Rad;

                            // Rotate lastMoveDirection by clamped angle
                            float cos = Mathf.Cos(radians);
                            float sin = Mathf.Sin(radians);
                            manualAimDirection = new Vector2(
                                lastMoveDirection.x * cos - lastMoveDirection.y * sin,
                                lastMoveDirection.x * sin + lastMoveDirection.y * cos
                            ).normalized;
                        }
                        else
                        {
                            // Within valid cone
                            manualAimDirection = desiredAim;
                        }
                    }

                    isManuallyAiming = true;
                    UpdateAimIndicator(isRoofShot ? facingDirectionAtChargeStart : manualAimDirection);
                }
                else
                {
                    // Below threshold - use auto-aim
                    isManuallyAiming = false;
                    isRoofShot = false;
                    UpdateAimIndicator(CalculateShotDirection());
                }
            }
            else
            {
                // Not charging - hide aim indicator
                if (aimIndicator != null)
                {
                    aimIndicator.enabled = false;
                }
                isManuallyAiming = false;
            }
        }
    }

    private void StartChargingShot()
    {
        if (timingMeter != null && HasPossession())
        {
            isChargingShot = true;
            facingDirectionAtChargeStart = lastMoveDirection; // Lock the facing direction
            timingMeter.StartCharging();
        }
    }

    private void StopChargingShot()
    {
        if (!isChargingShot) return;

        bool wasRoofShot = isRoofShot;
        isChargingShot = false;
        isRoofShot = false; // Reset roof shot state

        // Stop the timing meter and get result
        if (timingMeter != null)
        {
            TimingMeter.TimingResult result = timingMeter.StopCharging();
            float powerMultiplier = timingMeter.GetPowerMultiplier(result);

            // Execute shot with timing multiplier (and roof shot if applicable)
            ExecuteTimedShot(powerMultiplier, result, wasRoofShot);
        }
    }

    private void HandleShootRequested(bool isCharged)
    {
        // This is now mainly for tap shots (wrist shots without timing)
        // Charged shots use the timing system via StartChargingShot/StopChargingShot

        if (!isCharged)
        {
            // Quick tap - execute wrist shot immediately
            if (HasPossession())
            {
                ExecuteWristShot();
            }
        }
    }

    private void ExecuteWristShot()
    {
        Vector2 shotDirection = CalculateShotDirection();
        ApplyShotForce(shotDirection, wristShotPower);

        Debug.Log($"Wrist shot! Power: {wristShotPower}, Direction: {shotDirection}");
    }

    private void ExecuteSlapshot()
    {
        Vector2 shotDirection = CalculateShotDirection();
        float slapshotPower = wristShotPower * slapShotPower;
        ApplyShotForce(shotDirection, slapshotPower);

        Debug.Log($"Slapshot! Power: {slapshotPower}, Direction: {shotDirection}");

        // TODO: Add screen shake effect
    }

    private void ExecuteTimedShot(float powerMultiplier, TimingMeter.TimingResult result, bool isRoof = false)
    {
        if (!HasPossession()) return;

        Vector2 shotDirection = CalculateShotDirection();
        float basePower = wristShotPower * slapShotPower; // Use slapshot base power
        float finalPower = basePower * powerMultiplier;

        // Apply roof shot multiplier if applicable
        if (isRoof)
        {
            finalPower *= roofShotPowerMultiplier;
        }

        ApplyShotForce(shotDirection, finalPower);

        string resultText = result == TimingMeter.TimingResult.Perfect ? "PERFECT" :
                           result == TimingMeter.TimingResult.Weak ? "WEAK" : "OVERCHARGED";

        if (isRoof)
        {
            Debug.Log($"ROOF SHOT ({resultText})! Power: {finalPower} (roof multiplier: {roofShotPowerMultiplier}x)");
            // TODO: Show "ROOF!" text overlay
            // TODO: Add roof shot visual effects (arc trajectory)
        }
        else
        {
            Debug.Log($"Timed shot ({resultText})! Power: {finalPower} (base: {basePower}, multiplier: {powerMultiplier}x)");
        }

        // TODO: Add visual/audio feedback based on timing result
    }

    private Vector2 CalculateShotDirection()
    {
        // If roof shot, ALWAYS shoot forward (ignore the backwards pull)
        if (isRoofShot)
        {
            return facingDirectionAtChargeStart;
        }

        // If manually aiming, use that direction
        if (isManuallyAiming)
        {
            return manualAimDirection;
        }

        // Start with player's facing direction
        Vector2 baseDirection = lastMoveDirection;

        // Apply auto-aim if enabled and goal exists
        if (autoAimEnabled && nearestGoal != null)
        {
            // Calculate direction to goal
            Vector2 toGoal = (nearestGoal.position - transform.position).normalized;

            // Check if goal is within auto-aim cone
            float angleToGoal = Vector2.Angle(baseDirection, toGoal);

            if (angleToGoal <= maxAutoAimAngle)
            {
                // Blend between player direction and goal direction
                baseDirection = Vector2.Lerp(baseDirection, toGoal, autoAimStrength).normalized;
            }
        }

        return baseDirection;
    }

    private void ApplyShotForce(Vector2 direction, float power)
    {
        if (puckRb == null) return;

        // Apply impulse force to puck
        puckRb.linearVelocity = Vector2.zero; // Reset current velocity
        puckRb.AddForce(direction * power, ForceMode2D.Impulse);
    }

    private bool HasPossession()
    {
        if (puckTransform == null) return false;

        float distance = Vector2.Distance(transform.position, puckTransform.position);
        return distance <= possessionRadius;
    }

    private void UpdateAimIndicator(Vector2 direction)
    {
        if (aimIndicator == null) return;

        // Enable and position the line
        aimIndicator.enabled = true;

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + (Vector3)(direction * aimIndicatorLength);

        aimIndicator.SetPosition(0, startPos);
        aimIndicator.SetPosition(1, endPos);

        // Color the line based on shot type
        if (isRoofShot)
        {
            // Magenta for roof shot
            aimIndicator.startColor = Color.magenta;
            aimIndicator.endColor = Color.magenta;
        }
        else if (isManuallyAiming)
        {
            // Cyan for manual aim
            aimIndicator.startColor = Color.cyan;
            aimIndicator.endColor = Color.cyan;
        }
        else
        {
            // Yellow for auto-aim
            aimIndicator.startColor = Color.yellow;
            aimIndicator.endColor = Color.yellow;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showAimDebug) return;

        // Draw possession radius
        Gizmos.color = HasPossession() ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        // Draw shot direction
        if (Application.isPlaying)
        {
            Vector2 shotDir = CalculateShotDirection();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, shotDir * 5f);

            // Draw auto-aim cone
            if (autoAimEnabled && nearestGoal != null)
            {
                Gizmos.color = Color.cyan;
                Vector2 toGoal = (nearestGoal.position - transform.position).normalized;
                Gizmos.DrawRay(transform.position, toGoal * 5f);
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnShootRequested -= HandleShootRequested;
            ContextButtonManager.Instance.OnShotChargeStarted -= StartChargingShot;
            ContextButtonManager.Instance.OnShotChargeEnded -= StopChargingShot;
        }
    }
}

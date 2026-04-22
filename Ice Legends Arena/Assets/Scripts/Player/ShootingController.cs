using UnityEngine;

/// <summary>
/// Handles player shooting mechanics including wrist shots, slapshots, and auto-aim.
/// 3D physics: shots travel on XZ plane, roof shots add upward Y force.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ShootingController : MonoBehaviour
{
    [Header("Shot Power Settings")]
    [Tooltip("Base power for wrist shot (tap)")]
    [Range(5f, 50f)]
    public float wristShotPower = 20f; // Made public for CharacterStatsApplier

    [Tooltip("Power multiplier for slapshot (hold)")]
    [Range(1.5f, 3f)]
    public float slapShotPower = 2f; // Made public for CharacterStatsApplier

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
    [SerializeField] private float possessionRadius = 3.0f;

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

    [Tooltip("Upward force for roof shots (Y-axis lift)")]
    [Range(1f, 15f)]
    [SerializeField] private float roofShotLiftForce = 5f;

    [Header("Visual Feedback")]
    [SerializeField] private bool showAimDebug = true;
    [SerializeField] private LineRenderer aimIndicator;
    [SerializeField] private float aimIndicatorLength = 5f;

    // Component references
    private Rigidbody playerRb;
    private Transform puckTransform;
    private Rigidbody puckRb;
    private Transform nearestGoal;
    private TimingMeter timingMeter;

    // State - all directions are on XZ plane (Y=0) unless noted
    private Vector3 lastMoveDirection = Vector3.right;
    private Vector3 facingDirectionAtChargeStart = Vector3.right;
    private bool isChargingShot = false;
    private bool isManuallyAiming = false;
    private Vector3 manualAimDirection = Vector3.right;
    private bool isRoofShot = false;

    // Public properties for other components
    public bool IsChargingShot => isChargingShot;
    public float ChargingMovementMultiplier => chargingMovementMultiplier;
    public bool IsRoofShot => isRoofShot;

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
            Debug.LogError("ShootingController: No Puck found! Tag your puck with 'Puck' tag.");
        }

        // Find opponent's goal (the one we shoot at)
        // Player team defends +X goal, so we shoot at the -X goal (lowest X)
        GameObject[] goals = GameObject.FindGameObjectsWithTag("Goal");
        if (goals.Length >= 2)
        {
            // Pick the goal with the lowest X (opponent's goal for player team)
            nearestGoal = goals[0].transform.position.x < goals[1].transform.position.x
                ? goals[0].transform : goals[1].transform;
            Debug.Log($"ShootingController: Aiming at goal: {nearestGoal.name} (X:{nearestGoal.position.x})");
        }
        else if (goals.Length == 1)
        {
            nearestGoal = goals[0].transform;
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
            Vector3 moveInput = PhysicsHelper.InputToWorld(InputManager.Instance.MoveInput);

            // Only update facing direction when NOT charging
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
                    Vector3 desiredAim = moveInput.normalized;

                    // Check for ROOF SHOT: pulling joystick back (opposite of facing direction)
                    float angleToDesired = PhysicsHelper.SignedAngleXZ(facingDirectionAtChargeStart, desiredAim);

                    if (roofShotEnabled && Mathf.Abs(angleToDesired) >= roofShotAngleThreshold)
                    {
                        isRoofShot = true;
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
                            // Clamp to max angle - rotate around Y axis
                            float clampedAngle = Mathf.Sign(angleToDesired) * maxAimAngle;
                            manualAimDirection = Quaternion.Euler(0f, -clampedAngle, 0f) * lastMoveDirection;
                            manualAimDirection = PhysicsHelper.FlattenY(manualAimDirection).normalized;
                        }
                        else
                        {
                            manualAimDirection = desiredAim;
                        }
                    }

                    isManuallyAiming = true;
                    UpdateAimIndicator(isRoofShot ? facingDirectionAtChargeStart : manualAimDirection);
                }
                else
                {
                    isManuallyAiming = false;
                    isRoofShot = false;
                    UpdateAimIndicator(CalculateShotDirection());
                }
            }
            else
            {
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
            facingDirectionAtChargeStart = lastMoveDirection;
            timingMeter.StartCharging();
        }
    }

    private void StopChargingShot()
    {
        if (!isChargingShot) return;

        bool wasRoofShot = isRoofShot;
        isChargingShot = false;
        isRoofShot = false;

        if (timingMeter != null)
        {
            TimingMeter.TimingResult result = timingMeter.StopCharging();
            float powerMultiplier = timingMeter.GetPowerMultiplier(result);
            ExecuteTimedShot(powerMultiplier, result, wasRoofShot);
        }
    }

    private void HandleShootRequested(bool isCharged)
    {
        if (!isCharged)
        {
            if (HasPossession())
            {
                ExecuteWristShot();
            }
        }
    }

    private void ExecuteWristShot()
    {
        Vector3 shotDirection = CalculateShotDirection();
        ApplyShotForce(shotDirection, wristShotPower, false);

        Debug.Log($"Wrist shot! Power: {wristShotPower}, Direction: {shotDirection}");
    }

    private void ExecuteSlapshot()
    {
        Vector3 shotDirection = CalculateShotDirection();
        float slapshotPower = wristShotPower * slapShotPower;
        ApplyShotForce(shotDirection, slapshotPower, false);

        Debug.Log($"Slapshot! Power: {slapshotPower}, Direction: {shotDirection}");

        if (PuckFollowCamera.Instance != null)
        {
            PuckFollowCamera.Instance.TriggerSlapShotShake();
        }
    }

    private void ExecuteTimedShot(float powerMultiplier, TimingMeter.TimingResult result, bool isRoof = false)
    {
        if (!HasPossession()) return;

        Vector3 shotDirection = CalculateShotDirection();
        float basePower = wristShotPower * slapShotPower;
        float finalPower = basePower * powerMultiplier;

        if (isRoof)
        {
            finalPower *= roofShotPowerMultiplier;
        }

        ApplyShotForce(shotDirection, finalPower, isRoof);

        string resultText = result == TimingMeter.TimingResult.Perfect ? "PERFECT" :
                           result == TimingMeter.TimingResult.Weak ? "WEAK" : "OVERCHARGED";

        if (isRoof)
        {
            Debug.Log($"ROOF SHOT ({resultText})! Power: {finalPower} (roof multiplier: {roofShotPowerMultiplier}x, lift: {roofShotLiftForce})");
        }
        else
        {
            Debug.Log($"Timed shot ({resultText})! Power: {finalPower} (base: {basePower}, multiplier: {powerMultiplier}x)");
        }

        if (result == TimingMeter.TimingResult.Perfect || isRoof)
        {
            if (PuckFollowCamera.Instance != null)
            {
                PuckFollowCamera.Instance.TriggerSlapShotShake();
            }
        }
    }

    private Vector3 CalculateShotDirection()
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
        Vector3 baseDirection = lastMoveDirection;

        // Apply auto-aim if enabled and goal exists
        if (autoAimEnabled && nearestGoal != null)
        {
            Vector3 toGoal = PhysicsHelper.DirectionXZ(transform.position, nearestGoal.position);

            float angleToGoal = PhysicsHelper.AngleXZ(baseDirection, toGoal);

            if (angleToGoal <= maxAutoAimAngle)
            {
                baseDirection = Vector3.Lerp(baseDirection, toGoal, autoAimStrength).normalized;
                baseDirection = PhysicsHelper.FlattenY(baseDirection).normalized;
            }
        }

        return baseDirection;
    }

    private void ApplyShotForce(Vector3 direction, float power, bool isRoof)
    {
        if (puckRb == null) return;

        // Signal puck to release possession
        PuckController puckController = puckRb.GetComponent<PuckController>();
        if (puckController != null)
        {
            puckController.SignalShotFired();
        }

        // Check for Trick Shot modifier
        TrickShotModifier trickShot = GetComponent<TrickShotModifier>();
        if (trickShot != null && trickShot.IsLoaded)
        {
            trickShot.ApplyToPuck(puckRb.gameObject);
            Debug.Log("Trick Shot applied to puck!");
        }

        // Reset current velocity
        puckRb.linearVelocity = Vector3.zero;

        // Apply force on XZ plane
        Vector3 force = PhysicsHelper.FlattenY(direction).normalized * power;

        // Add upward Y force for roof shots - puck actually lifts off the ice!
        if (isRoof)
        {
            force.y = roofShotLiftForce;
        }

        puckRb.AddForce(force, ForceMode.Impulse);
    }

    private bool HasPossession()
    {
        if (puckTransform == null) return false;

        float distance = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);
        return distance <= possessionRadius;
    }

    private void UpdateAimIndicator(Vector3 direction)
    {
        if (aimIndicator == null) return;

        aimIndicator.enabled = true;

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + direction * aimIndicatorLength;

        aimIndicator.SetPosition(0, startPos);
        aimIndicator.SetPosition(1, endPos);

        if (isRoofShot)
        {
            aimIndicator.startColor = Color.magenta;
            aimIndicator.endColor = Color.magenta;
        }
        else if (isManuallyAiming)
        {
            aimIndicator.startColor = Color.cyan;
            aimIndicator.endColor = Color.cyan;
        }
        else
        {
            aimIndicator.startColor = Color.yellow;
            aimIndicator.endColor = Color.yellow;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showAimDebug) return;

        Gizmos.color = HasPossession() ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        if (Application.isPlaying)
        {
            Vector3 shotDir = CalculateShotDirection();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, shotDir * 5f);

            if (autoAimEnabled && nearestGoal != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 toGoal = PhysicsHelper.DirectionXZ(transform.position, nearestGoal.position);
                Gizmos.DrawRay(transform.position, toGoal * 5f);
            }
        }
    }

    private void OnDestroy()
    {
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnShootRequested -= HandleShootRequested;
            ContextButtonManager.Instance.OnShotChargeStarted -= StartChargingShot;
            ContextButtonManager.Instance.OnShotChargeEnded -= StopChargingShot;
        }
    }
}

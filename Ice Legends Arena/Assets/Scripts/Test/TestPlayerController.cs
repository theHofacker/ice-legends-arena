using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simplified player controller for testing basic 3D movement.
/// No dependencies on formations, teams, abilities, or other systems.
/// Rink coordinate system: X = width (left/right), Z = length (goal-to-goal), Y = height.
/// Controls: WASD = move, Space = shoot (handled by TestPuckController), F = body check.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(TimingMeter))]
public class TestPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Range(1f, 30f)]
    public float moveSpeed = 14f;

    [Tooltip("How quickly player reaches top speed (original 2D: 10)")]
    [Range(0.1f, 30f)]
    public float acceleration = 10f;

    [Tooltip("How quickly player stops after releasing input (lower = more glide)")]
    [Range(0.1f, 30f)]
    public float deceleration = 0.8f;

    [Header("Rotation")]
    [Range(1f, 20f)]
    public float rotationSpeed = 10f;

    [Header("Body Check")]
    [Range(1f, 5f)]
    public float checkRange = 2.5f;

    [Range(0.5f, 3f)]
    public float checkCooldown = 1f;

    [Tooltip("Player speed (m/s) below this delivers a Light check (stagger, no puck drop)")]
    [Range(1f, 10f)]
    public float lightThreshold = 5f;

    [Tooltip("Player speed (m/s) at or above this delivers a Heavy check (full fall). Between light and heavy is Medium.")]
    [Range(8f, 20f)]
    public float heavyThreshold = 11f;

    [Header("Animation")]
    [Tooltip("Auto-finds Animator in children if empty")]
    public Animator animator;

    [Header("Stats")]
    [Tooltip("Optional CharacterData; its shotPower stat multiplies the timing-based shot force")]
    public CharacterData characterData;

    [Tooltip("Move speed scales down to this fraction of normal while charging a shot")]
    [Range(0.1f, 1f)]
    public float chargeMoveSpeedFactor = 0.5f;

    [Header("Debug")]
    public bool logInput = false;

    private Rigidbody rb;
    private InputManager inputManager;
    private float checkTimer = 0f;
    private TimingMeter timingMeter;

    // Animator hashes
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int HasPuckHash = Animator.StringToHash("HasPuck");
    private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int BodyCheckHash = Animator.StringToHash("BodyCheck");
    private static readonly int HitTierHash = Animator.StringToHash("HitTier");

    private TestPuckController puckCtrl;

    public TimingMeter TimingMeter => timingMeter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        timingMeter = GetComponent<TimingMeter>();

        // Physics setup for ice surface
        // Original 2D had linearDamping = 0! All sliding was from Lerp deceleration only.
        rb.useGravity = false;
        rb.linearDamping = 0f; // ZERO - matches original 2D. Deceleration Lerp handles stopping.
        rb.angularDamping = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        // Apply slippery ice material to player collider
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null && capsule.sharedMaterial == null)
        {
            PhysicsMaterial iceMat = new PhysicsMaterial("PlayerIceMat");
            iceMat.dynamicFriction = 0.02f;  // Nearly frictionless
            iceMat.staticFriction = 0.02f;
            iceMat.bounciness = 0f;
            iceMat.frictionCombine = PhysicsMaterialCombine.Minimum; // Use lowest friction of the two surfaces
            iceMat.bounceCombine = PhysicsMaterialCombine.Minimum;
            capsule.sharedMaterial = iceMat;
        }
    }

    private void Start()
    {
        inputManager = InputManager.Instance;
        if (inputManager == null)
        {
            Debug.LogError("TestPlayerController: No InputManager found!");
        }

        // Auto-find animator in children (Y Bot model)
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // Prevent culling from stopping animation
            Debug.Log($"TestPlayerController: Found animator on {animator.gameObject.name}");
        }

        puckCtrl = FindFirstObjectByType<TestPuckController>();
    }

    private void FixedUpdate()
    {
        if (inputManager == null) return;

        // Convert joystick input to world direction
        // Camera at -Z looking toward +Z:
        //   input.x (+right) → world +X (right on screen)
        //   input.y (+up) → world +Z (up on screen / away from camera)
        Vector2 rawInput = inputManager.MoveInput;
        Vector3 worldDir = new Vector3(rawInput.x, 0f, rawInput.y);

        if (logInput && worldDir.magnitude > 0.1f)
        {
            Debug.Log($"[TestPlayer] raw=({rawInput.x:F2},{rawInput.y:F2}) → world=({worldDir.x:F2},{worldDir.z:F2}) pos={transform.position:F1}");
        }

        // Slow the player while charging a shot — gives the meter UI time to read
        // and matches the "winding up" feel from the 2D version.
        float effectiveMoveSpeed = (timingMeter != null && timingMeter.IsCharging)
            ? moveSpeed * chargeMoveSpeedFactor
            : moveSpeed;

        // Calculate target velocity
        Vector3 targetVelocity = worldDir * effectiveMoveSpeed;

        // Lerp toward target (acceleration when input, deceleration when no input)
        float rate = (worldDir.magnitude > 0.1f) ? acceleration : deceleration;
        Vector3 currentXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 newVelocity = Vector3.Lerp(currentXZ, targetVelocity, rate * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(newVelocity.x, 0f, newVelocity.z);

        // Rotate model to face movement direction
        if (worldDir.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg;
            Quaternion target = Quaternion.Euler(0f, angle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.fixedDeltaTime);
        }

        // Body check cooldown ticks here; the actual key polling lives in Update so
        // it doesn't get sampled at fixed cadence and miss frames.
        if (checkTimer > 0f) checkTimer -= Time.fixedDeltaTime;

        // Mirror charging state to the animator so a wind-up clip can hook into it later.
        if (animator != null && timingMeter != null)
        {
            animator.SetBool(IsChargingHash, timingMeter.IsCharging);
        }

        // Update animation parameters
        if (animator != null)
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float speed = flatVel.magnitude;
            animator.SetFloat(SpeedHash, speed);

            // MoveX = signed strafe relative to facing. Used by SkatingWithPuck blend tree.
            // Component of velocity along transform.right, normalized to [-1, +1].
            float strafe = (speed > 0.05f) ? Vector3.Dot(flatVel, transform.right) / Mathf.Max(0.01f, moveSpeed) : 0f;
            animator.SetFloat(MoveXHash, Mathf.Clamp(strafe, -1f, 1f), 0.1f, Time.fixedDeltaTime);

            // HasPuck mirrors puck possession state.
            bool hasPuck = puckCtrl != null && puckCtrl.IsPossessed;
            animator.SetBool(HasPuckHash, hasPuck);
        }

        // Safety: keep on ice
        if (Mathf.Abs(transform.position.y) > 0.5f)
        {
            Vector3 pos = transform.position;
            pos.y = 0f;
            transform.position = pos;
        }
    }

    /// <summary>
    /// Key press/release events from the new Input System are stable for exactly one
    /// Update frame. FixedUpdate runs at a fixed cadence and may execute 0 times in a
    /// given Update frame at higher framerates — sampling input there silently drops
    /// release events. Keep all wasPressedThisFrame / wasReleasedThisFrame polling
    /// inside Update.
    /// </summary>
    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.fKey.wasPressedThisFrame && checkTimer <= 0f)
        {
            TryBodyCheck();
        }

        HandleShotInput();
    }

    private void HandleShotInput()
    {
        if (timingMeter == null || puckCtrl == null || Keyboard.current == null) return;

        bool spaceDown = Keyboard.current.spaceKey.wasPressedThisFrame;
        bool spaceUp = Keyboard.current.spaceKey.wasReleasedThisFrame;

        // Begin charging only while we actually hold the puck — otherwise the meter
        // would tick down and fire on a phantom release with no shot to take.
        if (spaceDown && puckCtrl.IsPossessed && !timingMeter.IsCharging)
        {
            timingMeter.StartCharging();
        }
        else if (spaceUp && timingMeter.IsCharging)
        {
            TimingMeter.TimingResult result = timingMeter.StopCharging();
            float timingMul = timingMeter.GetPowerMultiplier(result);
            float charStat = (characterData != null) ? characterData.shotPower : 1f;

            // Final = base × character stat × timing zone. Overcharged sprays the puck
            // off-axis to match issue #10's "puck goes wide" spec.
            puckCtrl.FireTimedShot(timingMul * charStat, result == TimingMeter.TimingResult.Overcharged);

            if (logInput)
                Debug.Log($"[TestPlayer] Shot result={result} timingMul={timingMul:F2} charMul={charStat:F2} → finalMul={(timingMul * charStat):F2}");
        }
    }

    private void TryBodyCheck()
    {
        checkTimer = checkCooldown;

        // Classify by impact speed. Slow drift-in is just a stagger; full-skate
        // approach is a full fall. Speed is sampled from the rigidbody at the
        // moment F is pressed, not at first contact, so the player commits to a
        // tier based on their current momentum.
        float impactSpeed = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
        TestOpponentController.CheckTier tier =
            impactSpeed < lightThreshold ? TestOpponentController.CheckTier.Light :
            impactSpeed < heavyThreshold ? TestOpponentController.CheckTier.Medium :
                                           TestOpponentController.CheckTier.Heavy;

        // HitTier must be set BEFORE the trigger — the AnyState transition reads
        // both conditions in the same evaluation pass, so the int has to already
        // be the new value when the trigger fires.
        if (animator != null)
        {
            animator.SetInteger(HitTierHash, (int)tier);
            animator.SetTrigger(BodyCheckHash);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, checkRange);
        foreach (Collider hit in hits)
        {
            TestOpponentController opponent = hit.GetComponent<TestOpponentController>();
            if (opponent != null)
            {
                Vector3 knockDir = PhysicsHelper.DirectionXZ(transform.position, opponent.transform.position);
                opponent.GetBodyChecked(tier, knockDir);
                Debug.Log($"BODY CHECK ({tier}) on {opponent.name} at {impactSpeed:F1} m/s");
                return;
            }
        }

        Debug.Log($"Body check ({tier}) missed - no opponent in range");
    }

    private void OnDrawGizmosSelected()
    {
        // Draw velocity vector
        if (rb != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, rb.linearVelocity);
        }
    }
}

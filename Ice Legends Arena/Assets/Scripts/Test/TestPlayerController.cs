using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simplified player controller for testing basic 3D movement.
/// No dependencies on formations, teams, abilities, or other systems.
/// Rink coordinate system: X = width (left/right), Z = length (goal-to-goal), Y = height.
/// Controls: WASD = move, Space = hold-and-release shot, F = hold-and-release body check.
/// Both Space and F drive the same TimingMeter — Weak release = Light, Perfect = Medium,
/// Overcharged = Heavy on the check side. A chargeIntent enum prevents Space and F from
/// interfering when one is already charging.
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

    [Tooltip("Normalized charge fraction (0-1) below which a release counts as Light (button tap). Above this but still before the green zone counts as Medium (partial charge).")]
    [Range(0.05f, 0.7f)]
    public float lightChargeThreshold = 0.4f;

    [Tooltip("Grace period after F is pressed before the wind-up actually starts. " +
             "Release F within this window and the player goes straight to a quick stick poke (LightCheck) " +
             "with no wind-up animation. Hold past this window and the wind-up commits and the timing meter starts. " +
             "Set to 0 to start the wind-up instantly on press.")]
    [Range(0f, 0.3f)]
    public float checkTapGracePeriod = 0.12f;

    [Header("Animation")]
    [Tooltip("Auto-finds Animator in children if empty")]
    public Animator animator;

    [Header("Stats")]
    [Tooltip("Optional CharacterData; its shotPower stat multiplies the timing-based shot force")]
    public CharacterData characterData;

    [Tooltip("Move speed scales down to this fraction of normal while charging a shot")]
    [Range(0.1f, 1f)]
    public float chargeMoveSpeedFactor = 0.5f;

    [Header("Shot Wind-Up")]
    [Tooltip("Normalized time (0-1) within the Shoot clip where the stick reaches peak pull-back. Animation freezes here while Space is held, resumes on release.")]
    [Range(0.02f, 0.9f)]
    public float shotPeakNormalizedTime = 0.128f;

    [Tooltip("Normalized time (0-1) within the Shoot clip where the stick blade contacts the puck on the forward swing. The puck launches at this frame (not on Space release), so the shot syncs with the visible swing-through. Must be > shotPeakNormalizedTime and < the Shoot->Idle exit time (0.9). Tune to your shot clip.")]
    [Range(0.02f, 0.9f)]
    public float shotContactNormalizedTime = 0.35f;

    [Header("Debug")]
    public bool logInput = false;

    private Rigidbody rb;
    private InputManager inputManager;
    private float checkTimer = 0f;
    // Tap-grace tracker: -1 means "not in grace"; >=0 means "F held this many seconds
    // since the press, charge hasn't committed yet." Crosses checkTapGracePeriod to
    // commit; resets to -1 on commit or on release-during-grace (which fires a poke).
    private float checkPendingTime = -1f;
    private TimingMeter timingMeter;

    /// <summary>
    /// Tracks what the player is currently charging so Space (shot) and F (check)
    /// don't interfere. If Space is held first, F press is ignored until Space
    /// is released, and vice versa.
    /// </summary>
    private enum ChargeIntent { None, Shot, Check }
    private ChargeIntent chargeIntent = ChargeIntent.None;

    // Animator hashes
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int HasPuckHash = Animator.StringToHash("HasPuck");
    private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int BodyCheckHash = Animator.StringToHash("BodyCheck");
    private static readonly int HitTierHash = Animator.StringToHash("HitTier");
    private static readonly int ShotSpeedHash = Animator.StringToHash("ShotSpeed");
    private static readonly int ChargeIntentHash = Animator.StringToHash("ChargeIntent");

    // Set true between Space-press (Shoot trigger fired, animation playing) and the
    // frame normalizedTime reaches shotPeakNormalizedTime. Once peak is hit, we set
    // ShotSpeed=0 to freeze and clear this flag — no further per-frame checking.
    private bool isShotWindingUp = false;

    // Set true on Space release (shot committed, power locked) until the Shoot
    // animation reaches shotContactNormalizedTime, when the puck actually launches.
    // The puck stays possessed (riding the stick) through the forward swing until then.
    private bool isAwaitingContact = false;
    private float pendingShotPower = 0f;
    private bool pendingShotOvercharged = false;

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

            // Foot IK lives on the Animator's own GameObject so OnAnimatorIK fires.
            // Auto-attach so we don't need the user to add it by hand on every Y Bot.
            if (animator.GetComponent<FootIKController>() == null)
            {
                animator.gameObject.AddComponent<FootIKController>();
            }
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

        HandleShotInput();
        HandleCheckInput();
    }

    private void HandleShotInput()
    {
        if (timingMeter == null || puckCtrl == null || Keyboard.current == null) return;

        bool spaceDown = Keyboard.current.spaceKey.wasPressedThisFrame;
        bool spaceUp = Keyboard.current.spaceKey.wasReleasedThisFrame;

        // Begin charging only while we actually hold the puck and the meter isn't
        // already in use for a check. Without the intent guard, mashing both keys
        // would let F release fire a shot or Space release fire a check.
        if (spaceDown && puckCtrl.IsPossessed && !timingMeter.IsCharging && chargeIntent == ChargeIntent.None && !isAwaitingContact)
        {
            chargeIntent = ChargeIntent.Shot;
            timingMeter.StartCharging();

            // Fire the Shoot animation NOW (on press) — it'll play into the wind-up
            // and freeze at the peak via UpdateShotWindUpFreeze. Previously the
            // trigger fired on release; that was fine for instant shots, but doesn't
            // let us hold a visible pull-back pose while charging.
            if (animator != null)
            {
                animator.SetInteger(ChargeIntentHash, 1); // 1 = Shot
                animator.SetFloat(ShotSpeedHash, 1f);     // ensure not stuck frozen from a prior shot
                animator.SetTrigger(ShootHash);
                isShotWindingUp = true;
            }
        }
        else if (spaceUp && chargeIntent == ChargeIntent.Shot && timingMeter.IsCharging)
        {
            TimingMeter.TimingResult result = timingMeter.StopCharging();
            float timingMul = timingMeter.GetPowerMultiplier(result);
            float charStat = (characterData != null) ? characterData.shotPower : 1f;

            // Unfreeze the Shoot state so the rest of the slap-shot plays through.
            // ChargeIntent reset so the Charging state isn't triggered by stale state.
            if (animator != null)
            {
                animator.SetFloat(ShotSpeedHash, 1f);
                animator.SetInteger(ChargeIntentHash, 0);
                isShotWindingUp = false;
            }

            // Lock the shot power now (timing is judged at release) but DON'T launch
            // yet — defer to the stick-contact frame so the puck flies in sync with the
            // visible swing-through. The puck stays possessed and rides the stick until
            // the contact-frame check below fires it. Final = base × character stat × timing zone;
            // Overcharged sprays the puck off-axis (issue #10's "puck goes wide" spec).
            pendingShotPower = timingMul * charStat;
            pendingShotOvercharged = result == TimingMeter.TimingResult.Overcharged;
            isAwaitingContact = true;

            if (logInput)
                Debug.Log($"[TestPlayer] Shot released result={result} timingMul={timingMul:F2} charMul={charStat:F2} → finalMul={pendingShotPower:F2} (launch deferred to contact frame)");

            chargeIntent = ChargeIntent.None;
        }

        // While the wind-up is playing, watch for normalizedTime to cross the peak
        // frame and freeze the Shoot state there. One-shot per shot — flag clears
        // as soon as we freeze (or on release if the player tapped Space too fast
        // to reach the peak).
        if (isShotWindingUp && animator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("Shoot") && info.normalizedTime >= shotPeakNormalizedTime)
            {
                animator.SetFloat(ShotSpeedHash, 0f);
                isShotWindingUp = false;
            }
        }

        // After release, launch the puck the instant the forward swing reaches the
        // contact frame so the shot is synced to the visible stick-on-puck moment.
        // Failsafe: if the Shoot state is interrupted (e.g. a body-check) before
        // contact, fire immediately so the puck never stays stuck in possession.
        if (isAwaitingContact && animator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            bool inShoot = info.IsName("Shoot");
            if ((inShoot && info.normalizedTime >= shotContactNormalizedTime) || !inShoot)
            {
                puckCtrl.FireTimedShot(pendingShotPower, pendingShotOvercharged);
                isAwaitingContact = false;
                if (logInput)
                    Debug.Log($"[TestPlayer] Puck launched at contact (inShoot={inShoot}, nt={(inShoot ? info.normalizedTime : -1f):F2})");
            }
        }
    }

    private void HandleCheckInput()
    {
        if (timingMeter == null || Keyboard.current == null) return;

        bool fDown = Keyboard.current.fKey.wasPressedThisFrame;
        bool fUp = Keyboard.current.fKey.wasReleasedThisFrame;

        // F-down only puts us into a "pending" grace window. The wind-up state and
        // timing meter don't start until the grace expires — releasing inside grace
        // bypasses the wind-up entirely and fires a clean LightCheck poke. Branches
        // below are independent (not else-if) so a same-frame tap (fDown + fUp in one
        // Update at low framerate) still resolves correctly.
        if (fDown && checkTimer <= 0f && !timingMeter.IsCharging && chargeIntent == ChargeIntent.None)
        {
            chargeIntent = ChargeIntent.Check;
            checkPendingTime = 0f;
        }

        if (chargeIntent == ChargeIntent.Check && checkPendingTime >= 0f)
        {
            // Still in grace — decide whether to fire the tap poke or commit to charging.
            if (fUp)
            {
                // Tap: skip the wind-up state entirely, fire LightCheck directly.
                // No timing meter result to consult (it never started), so we hand a
                // synthetic Weak/normalized=0 to keep ChargeToCheckTier on its Light branch.
                DeliverCheck(TestOpponentController.CheckTier.Light, TimingMeter.TimingResult.Weak);
                chargeIntent = ChargeIntent.None;
                checkPendingTime = -1f;
            }
            else
            {
                checkPendingTime += Time.deltaTime;
                if (checkPendingTime >= checkTapGracePeriod)
                {
                    // Grace expired with F still held — NOW commit. Timing meter starts
                    // here so the visible green window aligns with the held time after
                    // the wind-up actually appears, not from the press itself.
                    timingMeter.StartCharging();
                    if (animator != null) animator.SetInteger(ChargeIntentHash, 2); // 2 = Check
                    checkPendingTime = -1f;
                }
            }
        }
        else if (fUp && chargeIntent == ChargeIntent.Check && timingMeter.IsCharging)
        {
            // Released after commit — normal timing-meter resolution path.
            float normalized = timingMeter.NormalizedCharge;
            TimingMeter.TimingResult result = timingMeter.StopCharging();
            TestOpponentController.CheckTier tier = ChargeToCheckTier(normalized, result);
            DeliverCheck(tier, result);
            if (animator != null) animator.SetInteger(ChargeIntentHash, 0);
            chargeIntent = ChargeIntent.None;
        }
    }

    /// <summary>
    /// Maps normalized charge + TimingMeter zone to check tier. Heavy is locked
    /// behind the green zone — perfect timing is the only path to a full-fall hit.
    /// Missing in either direction (released too early, OR held past green) drops
    /// to Medium. Very early release (below lightChargeThreshold, default 40%) is
    /// a Light tap. Mirrors the risk/reward of the shoot mechanic where over-holding
    /// drops your power back from "perfect."
    /// </summary>
    private TestOpponentController.CheckTier ChargeToCheckTier(float normalized, TimingMeter.TimingResult result)
    {
        if (result == TimingMeter.TimingResult.Perfect)
            return TestOpponentController.CheckTier.Heavy;

        // Weak or Overcharged. Inside Weak, split Light vs Medium by lightChargeThreshold;
        // Overcharged always counts as Medium (over-commit penalty).
        if (result == TimingMeter.TimingResult.Weak && normalized < lightChargeThreshold)
            return TestOpponentController.CheckTier.Light;

        return TestOpponentController.CheckTier.Medium;
    }

    private void DeliverCheck(TestOpponentController.CheckTier tier, TimingMeter.TimingResult timingResult)
    {
        // Resolve target first so we can decide whether to commit. If the only
        // opponent in range is already stunned, the check whiffs with full refund
        // (no anim, no cooldown burn) — restarting their fall mid-recovery looked
        // like the body was spinning on the ice.
        Collider[] hits = Physics.OverlapSphere(transform.position, checkRange);
        TestOpponentController target = null;
        bool stunnedInRange = false;
        foreach (Collider hit in hits)
        {
            TestOpponentController opp = hit.GetComponent<TestOpponentController>();
            if (opp == null) continue;
            if (opp.IsStunned) { stunnedInRange = true; continue; }
            target = opp;
            break;
        }

        if (target == null && stunnedInRange)
        {
            Debug.Log($"Body check ({tier}, timing={timingResult}) whiffed - target still recovering");
            return;
        }

        checkTimer = checkCooldown;

        // HitTier must be set BEFORE the trigger — the AnyState transition reads
        // both conditions in the same evaluation pass, so the int has to already
        // be the new value when the trigger fires.
        if (animator != null)
        {
            animator.SetInteger(HitTierHash, (int)tier);
            animator.SetTrigger(BodyCheckHash);
        }

        if (target != null)
        {
            Vector3 knockDir = PhysicsHelper.DirectionXZ(transform.position, target.transform.position);
            target.GetBodyChecked(tier, knockDir);
            Debug.Log($"BODY CHECK ({tier}, timing={timingResult}) on {target.name}");
            return;
        }

        Debug.Log($"Body check ({tier}, timing={timingResult}) missed - no opponent in range");
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

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

    [Header("Shot Overcharge")]
    [Tooltip("Normalized charge units past the green zone over which an overcharged shot's POWER " +
             "decays from full (green) down to the floor. Small = a hair late still rips; you have " +
             "to badly overhold to dribble it. The shot also sails high + wide (handled on the puck).")]
    [Range(0.1f, 1f)]
    public float overchargePowerFalloffRange = 0.5f;

    [Tooltip("Power multiplier floor a badly-overcharged shot decays to (replaces the old flat 0.6 cliff).")]
    [Range(0.2f, 1f)]
    public float overchargeMinPowerMul = 0.6f;

    [Header("Body Check Contact")]
    [Tooltip("Normalized time (0-1) within the HeavyCheck clip where the stick visibly contacts the opponent. The knockback impulse applies at this frame (not on F release), so the opponent flies in sync with the visible swing-through. Set to 0 to keep the old instant-on-release behavior.")]
    [Range(0f, 0.9f)]
    public float heavyCheckContactNormalizedTime = 0.25f;

    [Tooltip("Same as heavyCheckContactNormalizedTime, for the MediumCheck delivery. Pack clip is short, so default 0 keeps Medium snappy/instant.")]
    [Range(0f, 0.9f)]
    public float mediumCheckContactNormalizedTime = 0f;

    [Tooltip("Same as heavyCheckContactNormalizedTime, for the LightCheck poke. Default 0 — the LightCheck clip is fast enough that any deferral feels laggy.")]
    [Range(0f, 0.9f)]
    public float lightCheckContactNormalizedTime = 0f;

    [Tooltip("Hard timeout (s) for deferred check-knockback. If the swing animation never reaches its contact frame within this window (e.g. the player got hit and the check state was cancelled), apply the knockback anyway so a queued check can't silently whiff.")]
    [Range(0.1f, 1.5f)]
    public float checkContactTimeout = 0.6f;

    [Header("Passing")]
    [Tooltip("Press B to pass to the best teammate (nearest one that's forward-ish) within this XZ range.")]
    [Range(5f, 40f)]
    public float passRange = 30f;

    [Tooltip("HOLD B this long (s) to reach full pass power; a quick tap is a soft short pass. Power " +
             "between min/max lives on the puck (Min/Max Pass Power).")]
    [Range(0.1f, 2f)]
    public float passChargeDuration = 0.55f;

    [Header("One-Timer")]
    [Tooltip("Tap Space as an incoming pass arrives (while NOT carrying) to redirect it in one motion. " +
             "Max XZ distance the incoming puck can be to attempt a one-timer.")]
    [Range(1f, 6f)]
    public float oneTimerWindowRadius = 3.5f;

    [Tooltip("Min dot(puck velocity dir, dir to me) for the puck to count as a pass coming AT me.")]
    [Range(0f, 1f)]
    public float oneTimerAlignment = 0.25f;

    [Tooltip("Ignore pucks faster than this for one-timers (so you don't one-time a hard shot flying by).")]
    [Range(6f, 30f)]
    public float oneTimerMaxSpeed = 22f;

    [Tooltip("Time-to-arrival (s) that counts as PERFECT timing (max power + accuracy). Tap when the " +
             "puck is roughly this far out. Earlier = swinging at air, later = it's already past you.")]
    [Range(0.03f, 0.4f)]
    public float oneTimerPerfectWindow = 0.12f;

    [Tooltip("How far (s) the tap can be off the perfect window before the bonus drops to the minimum.")]
    [Range(0.05f, 0.6f)]
    public float oneTimerFalloff = 0.3f;

    [Tooltip("Power multiplier for a badly-timed one-timer.")]
    [Range(0.5f, 1.5f)]
    public float oneTimerMinBonus = 0.9f;

    [Tooltip("Power multiplier for a perfectly-timed one-timer (bigger than a clean green shot — the reward).")]
    [Range(1f, 2f)]
    public float oneTimerMaxBonus = 1.45f;

    [Tooltip("Loft charge fed to the one-timer (0.85 ≈ a clean snipe lifting toward the top of the net).")]
    [Range(0f, 1.2f)]
    public float oneTimerLoftCharge = 0.85f;

    [Tooltip("Max wide-spray (deg) on a badly-timed one-timer; tightens to 0 at perfect timing.")]
    [Range(0f, 40f)]
    public float oneTimerMaxSpray = 25f;

    [Header("Debug")]
    public bool logInput = false;

    private Rigidbody rb;
    private InputManager inputManager;
    private float checkTimer = 0f;
    // Tap-grace tracker: -1 means "not in grace"; >=0 means "F held this many seconds
    // since the press, charge hasn't committed yet." Crosses checkTapGracePeriod to
    // commit; resets to -1 on commit or on release-during-grace (which fires a poke).
    private float checkPendingTime = -1f;
    // Deferred check-knockback state. Mirrors the Shot's isAwaitingContact/pending*
    // pattern: trigger fires on release so the visual swing starts, but the impulse
    // on the opponent (target.GetBodyChecked) waits until the swing's contact frame.
    // Cleared when the impulse fires or the timeout expires (whichever first).
    private bool isAwaitingCheckContact = false;
    private TestOpponentController.CheckTier pendingCheckTier;
    private TestOpponentController pendingCheckTarget;
    private Vector3 pendingCheckKnockDir;
    private TimingMeter.TimingResult pendingCheckTimingResult;
    private float pendingCheckTimeRemaining = 0f;
    private TimingMeter timingMeter;

    /// <summary>
    /// Tracks what the player is currently charging so Space (shot), F (check) and B
    /// (pass) don't interfere. Whichever key is pressed first wins; the others are
    /// ignored until it's released.
    /// </summary>
    private enum ChargeIntent { None, Shot, Check, Pass }
    private ChargeIntent chargeIntent = ChargeIntent.None;
    // Pass charge tracker: -1 = not charging; >=0 = B held this many seconds. Crosses
    // passChargeDuration for full power; released early = a softer/shorter pass.
    private float passChargeTime = -1f;

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
    // Normalized charge (hold time / charge duration) at release — drives the puck's continuous
    // loft and overcharge wide-spray. Can exceed 1.0 when overheld.
    private float pendingShotChargeNormalized = 0f;
    // Aimed shot direction, locked at release from the aimer's cone/assist result. Vector3.zero
    // means "no aimer present" — the puck then falls back to its legacy last-movement-dir shot.
    private Vector3 pendingShotAimDir = Vector3.zero;

    private TestPuckController puckCtrl;
    private Rigidbody puckRb; // for reading incoming-pass speed/direction (one-timer detection)
    // Optional shot aimer (TestShotAimer on this same GameObject). Null = the old straight-down-your-
    // movement-direction shot still works, so the scene runs fine before the component is added.
    private TestShotAimer aimer;

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
        if (puckCtrl != null) puckRb = puckCtrl.GetComponent<Rigidbody>();
        aimer = GetComponent<TestShotAimer>();
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
        HandlePassInput();
    }

    /// <summary>
    /// B = pass to the best teammate, HELD to power it up. Press to begin charging (only while we
    /// actually hold the puck and nothing else is charging, so it can't fire during a shot/check
    /// wind-up); the longer B is held the harder the pass (tap = soft short pass, full hold = hard
    /// cross-ice pass). On release, picks the most forward teammate in range via
    /// <see cref="FindPassTarget"/>, LEADS their skating path so the puck doesn't land behind a moving
    /// receiver, and hands off to TestPuckController.PassTo at the charged power.
    /// </summary>
    private void HandlePassInput()
    {
        if (puckCtrl == null || Keyboard.current == null) return;

        bool bDown = Keyboard.current.bKey.wasPressedThisFrame;
        bool bUp = Keyboard.current.bKey.wasReleasedThisFrame;

        // Begin charging.
        if (bDown && puckCtrl.IsPossessed && chargeIntent == ChargeIntent.None && !isAwaitingContact)
        {
            chargeIntent = ChargeIntent.Pass;
            passChargeTime = 0f;
        }

        // Accumulate hold time while charging.
        if (chargeIntent == ChargeIntent.Pass && passChargeTime >= 0f)
            passChargeTime += Time.deltaTime;

        // Release → fire the charged, led pass.
        if (bUp && chargeIntent == ChargeIntent.Pass)
        {
            float charge = Mathf.Clamp01(passChargeTime / Mathf.Max(0.01f, passChargeDuration));
            passChargeTime = -1f;
            chargeIntent = ChargeIntent.None;

            if (!puckCtrl.IsPossessed) return; // lost the puck mid-charge (got checked, etc.)

            TestTeammateController target = FindPassTarget();
            if (target == null)
            {
                if (logInput) Debug.Log("[TestPlayer] Pass: no teammate in range");
                return;
            }

            // Lead the receiver by how hard this pass is thrown, so a skating teammate meets the puck
            // instead of it arriving behind them.
            float passSpeed = puckCtrl.GetPassSpeed(charge);
            Vector3 leadPoint = PhysicsHelper.LeadPointXZ(
                transform.position, target.transform.position, target.PlanarVelocity, passSpeed);

            if (puckCtrl.PassTo(leadPoint, charge))
            {
                Debug.Log($"[TestPlayer] PASS to {target.name} (charge={charge:F2})");
                // Hand control to the receiver so you become the skater catching the pass (and can
                // one-time it). No-op if there's no TestTeamController in the scene.
                if (TestTeamController.Instance != null)
                    TestTeamController.Instance.OnPassedTo(target.gameObject);
            }
        }
    }

    /// <summary>
    /// Best pass target = the teammate within passRange that's most "forward" (toward where the player
    /// is facing), tie-broken toward closer. Falls back to the nearest teammate if none read as
    /// forward. Returns null if there are no teammates in range.
    /// </summary>
    private TestTeammateController FindPassTarget()
    {
        TestTeammateController[] mates = FindObjectsByType<TestTeammateController>(FindObjectsSortMode.None);
        TestTeammateController best = null, nearest = null;
        float bestScore = float.NegativeInfinity, nearestDist = float.PositiveInfinity;

        foreach (TestTeammateController mate in mates)
        {
            if (mate == null) continue;
            // Skip our OWN (disabled) teammate controller — with the dual-controller switching setup,
            // the skater we're controlling also carries a TestTeammateController, and we can't pass to self.
            if (mate.gameObject == gameObject) continue;
            float dist = PhysicsHelper.DistanceXZ(transform.position, mate.transform.position);
            if (dist > passRange) continue;

            if (dist < nearestDist) { nearestDist = dist; nearest = mate; }

            Vector3 dir = PhysicsHelper.DirectionXZ(transform.position, mate.transform.position);
            float score = Vector3.Dot(dir, transform.forward) - dist * 0.03f; // forward preferred, closer preferred
            if (score > bestScore) { bestScore = score; best = mate; }
        }

        return best != null ? best : nearest;
    }

    /// <summary>
    /// True when a loose puck is flying AT this skater (within the one-timer window, aligned toward me,
    /// not too fast). <paramref name="timeToArrival"/> ≈ XZ distance / XZ speed — how long until it
    /// reaches me, which drives the one-timer timing bonus.
    /// </summary>
    private bool TryGetIncomingPass(out float timeToArrival)
    {
        timeToArrival = 0f;
        if (puckCtrl == null || puckRb == null || puckCtrl.IsPossessed) return false;

        Vector3 puckPos = puckCtrl.transform.position;
        float dist = PhysicsHelper.DistanceXZ(transform.position, puckPos);
        if (dist > oneTimerWindowRadius) return false;

        float speed = PhysicsHelper.SpeedXZ(puckRb.linearVelocity);
        if (speed < 1f || speed > oneTimerMaxSpeed) return false;

        Vector3 puckDir = PhysicsHelper.FlattenY(puckRb.linearVelocity).normalized;
        Vector3 toMe = PhysicsHelper.DirectionXZ(puckPos, transform.position);
        if (Vector3.Dot(puckDir, toMe) < oneTimerAlignment) return false;

        timeToArrival = dist / Mathf.Max(0.01f, speed);
        return true;
    }

    /// <summary>
    /// Redirect the incoming pass in one motion. Power + accuracy scale with how close to the puck's
    /// actual arrival the tap was: perfect timing = max power, no spray; mistimed = weaker and wide.
    /// Aimed along the player's facing (orient the skater toward the net for a one-timer snipe).
    /// </summary>
    private void FireOneTimer(float timeToArrival)
    {
        float err = Mathf.Abs(timeToArrival - oneTimerPerfectWindow);
        float perfect = 1f - Mathf.Clamp01(err / Mathf.Max(0.01f, oneTimerFalloff));

        float bonus = Mathf.Lerp(oneTimerMinBonus, oneTimerMaxBonus, perfect);
        float charStat = (characterData != null) ? characterData.shotPower : 1f;
        float spray = (1f - perfect) * oneTimerMaxSpray * (Random.value < 0.5f ? -1f : 1f);

        Vector3 aimDir = (aimer != null && PhysicsHelper.FlattenY(aimer.AimDirection).sqrMagnitude > 0.01f)
            ? aimer.AimDirection
            : transform.forward;

        puckCtrl.RedirectShot(aimDir, bonus * charStat, oneTimerLoftCharge, spray);

        if (animator != null) animator.SetTrigger(ShootHash);
        if (logInput)
            Debug.Log($"[TestPlayer] ONE-TIMER tta={timeToArrival:F2}s perfect={perfect:F2} bonus={bonus:F2} spray={spray:F0}");
    }

    private void HandleShotInput()
    {
        if (timingMeter == null || puckCtrl == null || Keyboard.current == null) return;

        bool spaceDown = Keyboard.current.spaceKey.wasPressedThisFrame;
        bool spaceUp = Keyboard.current.spaceKey.wasReleasedThisFrame;

        // ONE-TIMER: if we DON'T hold the puck but a pass is flying at us, a Space tap redirects it in
        // one motion (no trap). Consume the press so the normal charge branch below doesn't also react.
        // If you mistime it late and the puck already auto-trapped, IsPossessed is true and this skips,
        // so the tap just begins a normal shot charge instead.
        if (spaceDown && !puckCtrl.IsPossessed && chargeIntent == ChargeIntent.None && !isAwaitingContact
            && TryGetIncomingPass(out float oneTimerTta))
        {
            FireOneTimer(oneTimerTta);
            spaceDown = false;
        }

        // Begin charging only while we actually hold the puck and the meter isn't
        // already in use for a check. Without the intent guard, mashing both keys
        // would let F release fire a shot or Space release fire a check.
        if (spaceDown && puckCtrl.IsPossessed && !timingMeter.IsCharging && chargeIntent == ChargeIntent.None && !isAwaitingContact)
        {
            chargeIntent = ChargeIntent.Shot;
            timingMeter.StartCharging();

            // Freeze the aim cone to the facing direction AT PRESS (the player can keep skating/
            // turning while charging, but the cone stays anchored to where they were pointed when
            // they committed — that's what makes approach angle matter). Origin = the player so the
            // cone-to-net geometry is measured from roughly the puck's launch spot.
            if (aimer != null)
                aimer.BeginAim(transform.forward, transform.position);

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
            float chargeNorm = timingMeter.NormalizedCharge; // valid post-Stop (currentCharge isn't reset)

            // Overcharge: held past the green window. Instead of the flat red-zone cliff, decay
            // power gradually from full (green) toward overchargeMinPowerMul the longer it's held,
            // so a split-second-late shot still rips (it just sails high/wide — handled on the puck)
            // and only a badly-late one dribbles.
            if (result == TimingMeter.TimingResult.Overcharged)
            {
                float over = chargeNorm - timingMeter.GreenZoneEnd;            // how far past green
                float decay = Mathf.Clamp01(over / overchargePowerFalloffRange);
                timingMul = Mathf.Lerp(timingMeter.GetPowerMultiplier(TimingMeter.TimingResult.Perfect),
                                       overchargeMinPowerMul, decay);
            }

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
            // the contact-frame check below fires it. Final power = character stat × timing (green
            // = full, overcharge = decayed above). Loft + overcharge wide-spray ride the puck off
            // the normalized charge we pass through.
            pendingShotPower = timingMul * charStat;
            pendingShotChargeNormalized = chargeNorm;
            // Lock the aim when you commit (release), same instant power is locked. The puck then
            // flies down this direction at the contact frame. Vector3.zero if no aimer → puck falls
            // back to its legacy last-movement-direction shot.
            pendingShotAimDir = aimer != null ? aimer.AimDirection : Vector3.zero;
            if (aimer != null) aimer.EndAim();
            isAwaitingContact = true;

            if (logInput)
                Debug.Log($"[TestPlayer] Shot released result={result} charge={chargeNorm:F2} timingMul={timingMul:F2} charMul={charStat:F2} → finalMul={pendingShotPower:F2} (launch deferred to contact frame)");

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
                puckCtrl.FireTimedShot(pendingShotPower, pendingShotChargeNormalized, pendingShotAimDir);
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

        // Deferred-knockback contact sync (mirrors the shot's contact-frame deferral
        // at line ~344). Once DeliverCheck stages a pending hit, we watch the animator
        // each frame and fire the impulse the moment the tier's check state reaches
        // its contact normalizedTime. Timeout failsafe handles the case where the
        // check state never gets entered (preempted by GotHit, etc.) so a queued hit
        // can't silently whiff. Cleared inside FireDeferredCheck.
        if (isAwaitingCheckContact)
        {
            pendingCheckTimeRemaining -= Time.deltaTime;
            if (pendingCheckTimeRemaining <= 0f)
            {
                FireDeferredCheck("timeout failsafe");
            }
            else if (animator != null)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                string targetState = GetCheckStateName(pendingCheckTier);
                float contactNT = GetCheckContactNormalizedTime(pendingCheckTier);
                if (targetState != null && info.IsName(targetState) && info.normalizedTime >= contactNT)
                {
                    FireDeferredCheck($"contact frame reached (nt={info.normalizedTime:F2})");
                }
            }
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
            float contactNT = GetCheckContactNormalizedTime(tier);
            if (contactNT > 0f)
            {
                // Defer the impulse until the swing reaches the visible contact frame.
                // Knockback direction is locked at trigger time so the opponent flies in
                // the direction the player was facing AT release, not where they end up
                // after the wind-down — feels more like a connected hit than a homing one.
                isAwaitingCheckContact = true;
                pendingCheckTier = tier;
                pendingCheckTarget = target;
                pendingCheckKnockDir = knockDir;
                pendingCheckTimingResult = timingResult;
                pendingCheckTimeRemaining = checkContactTimeout;
                Debug.Log($"BODY CHECK ({tier}, timing={timingResult}) on {target.name} — awaiting contact frame (nt>={contactNT:F2})");
                return;
            }
            target.GetBodyChecked(tier, knockDir);
            Debug.Log($"BODY CHECK ({tier}, timing={timingResult}) on {target.name}");
            return;
        }

        Debug.Log($"Body check ({tier}, timing={timingResult}) missed - no opponent in range");
    }

    /// <summary>
    /// Per-tier contact normalized time. 0 = apply knockback instantly (legacy behavior);
    /// >0 = defer until the corresponding check state's clip reaches that fraction.
    /// </summary>
    private float GetCheckContactNormalizedTime(TestOpponentController.CheckTier tier)
    {
        switch (tier)
        {
            case TestOpponentController.CheckTier.Heavy: return heavyCheckContactNormalizedTime;
            case TestOpponentController.CheckTier.Medium: return mediumCheckContactNormalizedTime;
            case TestOpponentController.CheckTier.Light:  return lightCheckContactNormalizedTime;
            default: return 0f;
        }
    }

    /// <summary>
    /// Animator state name for a given check tier. Used by the contact-sync loop to
    /// verify the animator actually entered the expected state before reading
    /// normalizedTime — guards against stale reads if a higher-priority trigger
    /// (e.g. GotHit) preempted the check.
    /// </summary>
    private static string GetCheckStateName(TestOpponentController.CheckTier tier)
    {
        switch (tier)
        {
            case TestOpponentController.CheckTier.Heavy: return "HeavyCheck";
            case TestOpponentController.CheckTier.Medium: return "MediumCheck";
            case TestOpponentController.CheckTier.Light:  return "LightCheck";
            default: return null;
        }
    }

    /// <summary>
    /// Applies the deferred knockback. Called either when the animator's contact frame
    /// is reached OR when the timeout expires. Always clears the pending state so we
    /// can't double-apply.
    /// </summary>
    private void FireDeferredCheck(string reason)
    {
        if (pendingCheckTarget != null)
        {
            pendingCheckTarget.GetBodyChecked(pendingCheckTier, pendingCheckKnockDir);
            Debug.Log($"BODY CHECK ({pendingCheckTier}, timing={pendingCheckTimingResult}) applied — {reason}");
        }
        isAwaitingCheckContact = false;
        pendingCheckTarget = null;
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

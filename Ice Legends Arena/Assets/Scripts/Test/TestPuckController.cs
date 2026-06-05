using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simplified puck controller for testing possession, following, and shooting.
/// No dependencies on teams, opponents, formations, or context buttons.
/// Uses keyboard shortcuts: Space = shoot.
/// Rink: X = width, Z = length (goal-to-goal), Y = height.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class TestPuckController : MonoBehaviour
{
    [Header("Possession")]
    [Tooltip("Distance to auto-possess puck")]
    [Range(0.5f, 8f)]
    public float possessionRadius = 1.5f;

    [Tooltip("How far in front of player the puck sits")]
    [Range(0.3f, 5f)]
    public float stickOffset = 0.7f;

    [Tooltip("How smoothly puck follows player")]
    [Range(5f, 80f)]
    public float followSpeed = 50f;

    [Tooltip("The puck can only be auto-possessed when it's within this height of the ice — you " +
             "can't scoop a puck that's still airborne.")]
    [Range(0.1f, 2f)]
    public float possessGrabHeight = 0.4f;

    [Header("Shooting")]
    [Tooltip("Base shot impulse — multiplied by timing/character/equipment modifiers")]
    [Range(5f, 50f)]
    public float shotPower = 20f;

    [Tooltip("Seconds before puck can be re-possessed after shot")]
    [Range(0.3f, 3f)]
    public float shotCooldown = 1f;

    [Tooltip("Max angle (degrees) the puck deflects off facing direction on an Overcharged shot")]
    [Range(5f, 45f)]
    public float overchargedSprayAngle = 25f;

    [Header("Shot Loft (auto from how long you hold)")]
    [Tooltip("Vertical launch speed (m/s) of a well-timed shot — peaks around the green zone so a " +
             "clean snipe lifts toward the top of the net. ~5 peaks near the 1.2m crossbar at the " +
             "far net.")]
    [Range(0f, 12f)]
    public float maxLiftSpeed = 5f;

    [Tooltip("Normalized charge (hold time / charge duration) at which loft STARTS ramping in; " +
             "below this the shot stays flat. 0.6 = a little lift already kicks in before the 0.75 " +
             "green zone.")]
    [Range(0f, 1f)]
    public float loftStartCharge = 0.6f;

    [Tooltip("Normalized charge at which loft reaches maxLiftSpeed — set near the green window so a " +
             "perfectly-timed shot gets the full arc.")]
    [Range(0f, 1.2f)]
    public float loftPeakCharge = 0.9f;

    [Tooltip("Extra vertical speed (m/s) per 1.0 of overcharge held PAST loftPeakCharge — this is " +
             "what makes a slightly-late shot sail HIGH over the crossbar. Longer overhold = higher " +
             "(capped by Max Overcharge Lift).")]
    [Range(0f, 20f)]
    public float overchargeLiftPerCharge = 10f;

    [Tooltip("Cap on the extra overcharge loft (m/s) so a wildly late shot doesn't rocket to orbit.")]
    [Range(0f, 12f)]
    public float maxOverchargeLift = 5f;

    [Tooltip("Normalized charge past which the shot starts spraying WIDE (and high) — the overcharge " +
             "penalty. Match to the green-zone end (~0.95).")]
    [Range(0.5f, 1.2f)]
    public float overchargeStartCharge = 0.95f;

    [Tooltip("Wide-spray degrees per 1.0 of overcharge past Overcharge Start Charge. Continuous, so " +
             "a split-second-late shot is only slightly off; way late is wild. Capped by Overcharged " +
             "Spray Angle.")]
    [Range(0f, 200f)]
    public float overchargeSprayPerCharge = 80f;

    [Header("Physics")]
    [Tooltip("Puck linear damping (ice friction)")]
    [Range(0f, 2f)]
    public float puckDamping = 0.5f;

    [Tooltip("Puck bounciness off boards")]
    [Range(0f, 1f)]
    public float bounciness = 0.6f;

    [Tooltip("Fraction of vertical speed the puck keeps each time it lands on the ice (0 = dead " +
             "drop, 1 = perfectly elastic). Scripted so it works even if the arena ice has no " +
             "collider of its own.")]
    [Range(0f, 0.9f)]
    public float groundBounce = 0.4f;

    [Tooltip("Resting height of the puck on the ice surface.")]
    [Range(0f, 0.3f)]
    public float restHeight = 0.05f;

    [Tooltip("Below this downward speed (m/s) a landing puck stops bouncing and settles flat.")]
    [Range(0.2f, 4f)]
    public float minBounceSpeed = 1.5f;

    [Header("Debug")]
    public bool logState = false;

    // State
    private Rigidbody rb;
    private SphereCollider puckCollider;
    private Transform playerTransform;
    private Rigidbody playerRb;
    private StickAttacher playerStick;     // null if the player has no bone-attached stick; we fall back to legacy offset follow
    private bool isPossessed = false;
    private float cooldownTimer = 0f;
    private Vector3 lastPlayerDir = Vector3.forward;

    public bool IsPossessed => isPossessed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        puckCollider = GetComponent<SphereCollider>();

        // Inspector values win — print them so we know what's actually active.
        Debug.Log($"[TestPuck] Tuning: possR={possessionRadius}, stick={stickOffset}, followSpd={followSpeed}");

        // Puck physics. Y is now FREE so the puck can rise on roof shots / saucer passes and
        // arc back down (real 3D depth). Gravity is toggled per-frame in FixedUpdate — OFF while
        // possessed (it rides the stick) and ON while loose (it falls). Rotation stays frozen so
        // the disc doesn't tumble. A scripted ground plane in FixedUpdate handles the landing
        // bounce, so verticality doesn't depend on the arena ice having a perfect collider.
        rb.useGravity = false;
        rb.linearDamping = puckDamping;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Bouncy material for boards
        if (puckCollider.sharedMaterial == null)
        {
            PhysicsMaterial mat = new PhysicsMaterial("TestPuckMat");
            mat.bounciness = bounciness;
            mat.dynamicFriction = 0.05f;
            mat.staticFriction = 0.05f;
            mat.bounceCombine = PhysicsMaterialCombine.Maximum;
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
            puckCollider.sharedMaterial = mat;
        }
    }

    private void Start()
    {
        // Find the test player
        TestPlayerController player = FindFirstObjectByType<TestPlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
            playerRb = player.GetComponent<Rigidbody>();
            // Pin the puck to the bone-attached blade if the player has one. Falls back to
            // the legacy "playerPos + dir * stickOffset" follow if no StickAttacher exists.
            playerStick = player.GetComponentInChildren<StickAttacher>();
            Debug.Log($"TestPuckController: Tracking player {playerTransform.name} " +
                      (playerStick != null ? $"— pinning puck to blade on {playerStick.name}" : "— legacy stickOffset follow (no StickAttacher)"));
        }
        else
        {
            Debug.LogError("TestPuckController: No TestPlayerController found in scene!");
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        // Tick cooldown
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        // Track player facing direction
        Vector2 moveInput = InputManager.Instance != null ? InputManager.Instance.MoveInput : Vector2.zero;
        if (moveInput.magnitude > 0.1f)
        {
            lastPlayerDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        }

        // Auto-possession check
        float dist = PhysicsHelper.DistanceXZ(transform.position, playerTransform.position);

        if (!isPossessed && cooldownTimer <= 0f && dist <= possessionRadius &&
            PhysicsHelper.SpeedXZ(rb.linearVelocity) < 8f &&
            transform.position.y <= restHeight + possessGrabHeight)
        {
            isPossessed = true;
            rb.linearVelocity = Vector3.zero;
            Debug.Log($"Puck possessed! (dist={dist:F1})");

            // Ignore collision with player
            Collider playerCol = playerTransform.GetComponent<Collider>();
            if (playerCol != null)
                Physics.IgnoreCollision(puckCollider, playerCol, true);
        }

        // Shooting is now triggered by TestPlayerController via FireTimedShot() so the
        // puck can stay agnostic about input timing and just react to fire commands.

        // (Puck height is now physics-driven — the scripted ice floor lives in FixedUpdate so the
        // landing bounce stays in sync with the rigidbody step. No per-frame Y clamp here anymore.)

        // Debug logging
        if (logState && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[Puck] possessed={isPossessed}, dist={dist:F1}, speed={PhysicsHelper.SpeedXZ(rb.linearVelocity):F1}, cooldown={cooldownTimer:F1}");
        }
    }

    private void FixedUpdate()
    {
        // Gravity rides with possession: OFF while glued to the stick, ON while loose so the puck
        // falls and arcs. Set every step so every possession entry/exit point stays correct.
        rb.useGravity = !isPossessed;

        // Only follow player if possessed AND not in shot cooldown
        // (cooldown means a shot was just fired - don't override the puck velocity)
        if (isPossessed && cooldownTimer <= 0f && playerTransform != null)
        {
            // Lead the target by the lerp's steady-state lag so the puck settles AT the
            // anchor while skating instead of trailing behind it. A Vector3.Lerp toward a
            // moving target equilibrates a distance (targetSpeed / followSpeed) behind it;
            // adding that vector forward cancels the gap. At rest playerVel is ~0 so the
            // puck still sits exactly on the anchor.
            Vector3 playerVel = playerRb != null ? PhysicsHelper.FlattenY(playerRb.linearVelocity) : Vector3.zero;

            Vector3 target;
            if (playerStick != null)
            {
                // Animator-driven stick: the actual blade contact point in world. Tracks
                // every clip (skate / idle / turn) automatically because StickAttacher
                // recomputes the stick pose from the hand bones each frame.
                target = playerStick.GetBladeContactPoint() + playerVel / followSpeed;
            }
            else
            {
                // Legacy fallback: fixed offset forward of the player. Only correct when
                // the stick is presumed to extend straight ahead by stickOffset units.
                Vector3 dir = lastPlayerDir.magnitude > 0.1f ? lastPlayerDir : Vector3.forward;
                dir = PhysicsHelper.FlattenY(dir).normalized;
                target = playerTransform.position + dir * stickOffset + playerVel / followSpeed;
            }
            target.y = 0.05f;

            Vector3 newPos = Vector3.Lerp(rb.position, target, followSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            rb.linearVelocity = Vector3.zero;
            return;
        }

        // Loose puck: scripted ice floor so the landing bounce is reliable whether or not the
        // arena ice has its own collider. When the puck reaches the ice surface moving downward,
        // reflect + damp its vertical velocity (bounce) until it's slow enough to settle flat.
        // x/z motion (slide, board ricochet) is untouched — only Y is handled here.
        if (rb.position.y <= restHeight && rb.linearVelocity.y <= 0f)
        {
            Vector3 v = rb.linearVelocity;
            v.y = (-v.y > minBounceSpeed) ? -v.y * groundBounce : 0f;
            rb.linearVelocity = v;

            Vector3 p = rb.position; // preserve x/z so the slide stays smooth; only pin Y
            p.y = restHeight;
            rb.position = p;
        }
    }

    /// <summary>
    /// Fires the puck with a timing-derived power multiplier. <paramref name="chargeNormalized"/>
    /// (hold time / charge duration, can exceed 1.0 when overheld) drives the continuous loft and
    /// the overcharge wide-spray: held longer → lifts higher; held past the green window → sails
    /// high AND wide. <paramref name="aimDir"/> is the AIMED shot direction (from TestShotAimer's
    /// cone/assist); pass Vector3.zero to fall back to the legacy "straight down last movement
    /// direction" behavior so the scene still works before the aimer is added.
    /// </summary>
    public void FireTimedShot(float powerMultiplier, float chargeNormalized, Vector3 aimDir = default)
    {
        if (!isPossessed) return;

        isPossessed = false;
        cooldownTimer = shotCooldown;

        Collider playerCol = playerTransform.GetComponent<Collider>();
        if (playerCol != null)
            Physics.IgnoreCollision(puckCollider, playerCol, false);

        // Aimed direction wins; fall back to last movement direction when no aim was supplied.
        Vector3 shotDir;
        if (PhysicsHelper.FlattenY(aimDir).sqrMagnitude > 0.01f)
            shotDir = PhysicsHelper.FlattenY(aimDir).normalized;
        else
            shotDir = (lastPlayerDir.magnitude > 0.1f ? lastPlayerDir : Vector3.forward);
        shotDir = PhysicsHelper.FlattenY(shotDir).normalized;

        // Overcharge wide-spray: the further past the green window you held, the wider the miss.
        // Continuous (replaces the old binary overcharged flag) — a split-second late only nudges
        // it off; way late sprays wild. Side is random; magnitude grows with the overhold.
        float overcharge = Mathf.Max(0f, chargeNormalized - overchargeStartCharge);
        if (overcharge > 0f)
        {
            float spray = Mathf.Min(overcharge * overchargeSprayPerCharge, overchargedSprayAngle);
            spray *= (Random.value < 0.5f ? -1f : 1f);
            shotDir = Quaternion.Euler(0f, spray, 0f) * shotDir;
        }

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(shotDir * shotPower * powerMultiplier, ForceMode.Impulse);

        // Auto-loft from how long you held: ramps in from loftStartCharge, peaks at loftPeakCharge
        // (a clean green snipe lifts toward the top of the net), then KEEPS climbing past the peak
        // so a slightly-overcharged shot sails high over the crossbar. VelocityChange => an exact
        // m/s regardless of puck mass; gravity (on while loose) arcs it back down.
        float baseLift = maxLiftSpeed * Mathf.InverseLerp(loftStartCharge, loftPeakCharge, chargeNormalized);
        float overLift = Mathf.Min(Mathf.Max(0f, chargeNormalized - loftPeakCharge) * overchargeLiftPerCharge, maxOverchargeLift);
        float vy = baseLift + overLift;
        if (vy > 0f)
            rb.AddForce(Vector3.up * vy, ForceMode.VelocityChange);

        // Trick Shot (Gunslinger): if the shooter armed a trick shot, stamp the ricochet
        // behaviour onto the puck now that it's left the stick. No-op when not armed.
        if (playerTransform != null)
        {
            TrickShotModifier trick = playerTransform.GetComponent<TrickShotModifier>();
            if (trick != null && trick.IsLoaded) trick.ApplyToPuck(gameObject);
        }

        // Animation is already playing — it was triggered on Space PRESS by
        // TestPlayerController so a freeze-at-peak wind-up could be held. Don't
        // re-trigger here or the slap-shot restarts from the beginning instead
        // of completing its release phase.

        Debug.Log($"SHOT! dir={shotDir:F2}, power={shotPower * powerMultiplier:F1} (mul={powerMultiplier:F2}, charge={chargeNormalized:F2}, vy={vy:F1}{(overcharge > 0f ? ", OVERCHARGED" : "")})");
    }

    /// <summary>
    /// Called by opponent body check to force the player to lose the puck.
    /// </summary>
    public void ForceLosePuck()
    {
        if (!isPossessed) return;

        isPossessed = false;
        cooldownTimer = shotCooldown;

        // Re-enable collision with player
        if (playerTransform != null)
        {
            Collider playerCol = playerTransform.GetComponent<Collider>();
            if (playerCol != null)
                Physics.IgnoreCollision(puckCollider, playerCol, false);
        }

        Debug.Log("Puck knocked loose!");
    }

    private void OnDrawGizmosSelected()
    {
        // Possession radius
        Gizmos.color = isPossessed ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        // Stick offset direction
        if (isPossessed && playerTransform != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 dir = lastPlayerDir.magnitude > 0.1f ? lastPlayerDir : Vector3.forward;
            Vector3 stickPos = playerTransform.position + dir.normalized * stickOffset;
            Gizmos.DrawLine(playerTransform.position, stickPos);
        }

        // Shot direction
        Gizmos.color = Color.red;
        Vector3 shotDir = lastPlayerDir.magnitude > 0.1f ? lastPlayerDir : Vector3.forward;
        Gizmos.DrawRay(transform.position, shotDir.normalized * 5f);
    }
}

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

    [Header("Physics")]
    [Tooltip("Puck linear damping (ice friction)")]
    [Range(0f, 2f)]
    public float puckDamping = 0.5f;

    [Tooltip("Puck bounciness off boards")]
    [Range(0f, 1f)]
    public float bounciness = 0.6f;

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

        // Puck physics - stays on ice, slides freely
        rb.useGravity = false;
        rb.linearDamping = puckDamping;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

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
            PhysicsHelper.SpeedXZ(rb.linearVelocity) < 8f)
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

        // Keep puck on ice
        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.y) > 0.1f)
        {
            pos.y = 0.05f;
            transform.position = pos;
        }

        // Debug logging
        if (logState && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[Puck] possessed={isPossessed}, dist={dist:F1}, speed={PhysicsHelper.SpeedXZ(rb.linearVelocity):F1}, cooldown={cooldownTimer:F1}");
        }
    }

    private void FixedUpdate()
    {
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
        }
    }

    /// <summary>
    /// Fires the puck with a timing-derived power multiplier. Set <paramref name="overcharged"/>
    /// true to spray the puck off-axis (matches issue #10's "goes wide" spec for the red zone).
    /// </summary>
    public void FireTimedShot(float powerMultiplier, bool overcharged)
    {
        if (!isPossessed) return;

        isPossessed = false;
        cooldownTimer = shotCooldown;

        Collider playerCol = playerTransform.GetComponent<Collider>();
        if (playerCol != null)
            Physics.IgnoreCollision(puckCollider, playerCol, false);

        Vector3 shotDir = lastPlayerDir.magnitude > 0.1f ? lastPlayerDir : Vector3.forward;
        shotDir = PhysicsHelper.FlattenY(shotDir).normalized;

        if (overcharged)
        {
            float spray = Random.Range(-overchargedSprayAngle, overchargedSprayAngle);
            shotDir = Quaternion.Euler(0f, spray, 0f) * shotDir;
        }

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(shotDir * shotPower * powerMultiplier, ForceMode.Impulse);

        // Animation is already playing — it was triggered on Space PRESS by
        // TestPlayerController so a freeze-at-peak wind-up could be held. Don't
        // re-trigger here or the slap-shot restarts from the beginning instead
        // of completing its release phase.

        Debug.Log($"SHOT! dir={shotDir:F2}, power={shotPower * powerMultiplier:F1} (mul={powerMultiplier:F2}{(overcharged ? ", OVERCHARGED" : "")})");
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

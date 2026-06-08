using UnityEngine;

/// <summary>
/// Simplified AI TEAMMATE for the test scene (Stage 3 of promoting TestMovement, "extend Test*"
/// path). Structurally a sibling of <see cref="TestOpponentController"/> — same possess / skate /
/// shoot model on a Y Bot — but on the PLAYER's team: it gets open to support the puck carrier,
/// CATCHES passes aimed at it, attacks the OPPONENT net, and never body-checks the player.
///
/// Possession arbitration mirrors the opponent: the puck (<see cref="TestPuckController"/>) only
/// auto-possesses to the PLAYER, so this manages its own <c>hasPuck</c>. It grabs a slow loose puck
/// nearby OR actively catches a pass moving toward it (receiveRadius / aligned / under maxReceiveSpeed
/// so it doesn't vacuum hard shots). If the player skates in and the puck auto-possesses to them by
/// proximity, it hands off cleanly (give-and-go). To avoid fighting the player for the puck, it does
/// NOT chase loose pucks in open play — it positions and receives.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class TestTeammateController : MonoBehaviour
{
    [Header("Movement")]
    [Range(1f, 20f)] public float moveSpeed = 6.5f;
    [Range(1f, 20f)] public float rotationSpeed = 8f;

    [Header("Puck Possession")]
    [Range(0.5f, 5f)] public float possessionRadius = 1.5f;
    [Range(0.3f, 5f)] public float stickOffset = 0.7f;
    [Range(10f, 80f)] public float puckFollowSpeed = 40f;

    [Header("Pass Reception")]
    [Tooltip("A loose puck moving TOWARD this teammate within this radius is caught (a received pass), " +
             "even while still moving — so passes don't have to arrive at a dead crawl.")]
    [Range(1f, 6f)] public float receiveRadius = 3f;

    [Tooltip("Min dot(puck velocity dir, dir to me) for an incoming puck to count as a pass aimed at me.")]
    [Range(0f, 1f)] public float receiveAlignment = 0.3f;

    [Tooltip("Don't auto-catch pucks faster than this — keeps the teammate from snagging hard shots " +
             "that happen to fly near it. Set above your pass power, below shot speed.")]
    [Range(4f, 30f)] public float maxReceiveSpeed = 16f;

    [Header("Support Positioning")]
    [Tooltip("How far ahead of the player (toward the attack goal) the teammate tries to get open.")]
    [Range(0f, 15f)] public float supportLead = 6f;

    [Tooltip("Sideways offset from the player's lane so the teammate is a passing option, not stacked on them.")]
    [Range(0f, 12f)] public float supportSide = 4f;

    [Header("Shooting")]
    [Range(5f, 40f)] public float shotPower = 15f;
    [Range(5f, 20f)] public float shootDistance = 12f;

    [Header("Give-and-Go")]
    [Tooltip("After receiving, if the player has broken forward into space the teammate RETURNS the " +
             "pass instead of carrying — the give-and-go. Max XZ distance to attempt a return pass.")]
    [Range(5f, 35f)] public float returnPassRange = 22f;

    [Tooltip("Beat (s) the teammate holds the puck before it'll consider a return pass, so the " +
             "give-and-go reads as an exchange and not an instant ping-pong.")]
    [Range(0f, 1.5f)] public float returnPassDelay = 0.35f;

    [Tooltip("How 'forward' the player must be to count as having made a run worth returning to: " +
             "dot(dir to player, dir to attack goal). 0 = level with the teammate, higher = the player " +
             "must be ahead toward the net.")]
    [Range(-1f, 1f)] public float returnPassForwardDot = 0.15f;

    [Tooltip("Impulse of the return pass (led to the player's skating path). Brisk but catchable — " +
             "the player actively receives it.")]
    [Range(5f, 40f)] public float returnPassPower = 15f;

    [Header("Behavior")]
    [Tooltip("The OPPONENT net this teammate attacks (auto-finds the GoalTrigger with IsPlayerGoal=false).")]
    public Transform attackGoal;

    [Tooltip("Hard limit on |pz| so the teammate doesn't sail past the back boards. Goals at z=±23.73.")]
    [Range(20f, 30f)] public float zBoundary = 25.5f;

    [Header("Appearance")]
    [Tooltip("Color to apply to the model — blue-ish to read as the player's team vs the red opponent.")]
    public Color teamColor = new Color(0.25f, 0.55f, 1f);

    [Header("Debug")]
    [Tooltip("Pin in place with AI disabled.")]
    public bool debugFreezePosition = false;

    // State
    private Rigidbody rb;
    private Animator animator;
    private Transform puckTransform;
    private Rigidbody puckRb;
    private TestPuckController puckCtrl;
    private Transform playerTransform;
    private Rigidbody playerRb;
    private StickAttacher teammateStick;
    private bool hasPuck = false;
    private float possessionCooldown = 0f;
    private float puckHeldTime = 0f;
    private Vector3 lastMoveDir = Vector3.forward;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int HasPuckHash = Animator.StringToHash("HasPuck");
    private static readonly int ShootHash = Animator.StringToHash("Shoot");

    public bool HasPuck => hasPuck;

    /// <summary>Flat (XZ) velocity — lets the passer lead this teammate's skating path.</summary>
    public Vector3 PlanarVelocity => PhysicsHelper.FlattenY(rb.linearVelocity);

    /// <summary>
    /// The skater the human currently controls (whom this teammate supports / passes back to). Reads
    /// the live active skater from <see cref="TestTeamController"/>; falls back to the Start-found player
    /// if there's no manager in the scene. When THIS skater is the active one its controller is disabled,
    /// so this never resolves to self during AI updates.
    /// </summary>
    private Transform Player => (TestTeamController.Instance != null && TestTeamController.Instance.ActiveSkater != null)
        ? TestTeamController.Instance.ActiveSkater
        : playerTransform;

    private Rigidbody PlayerRb
    {
        get
        {
            Transform p = Player;
            if (p == null) return null;
            return (p == playerTransform && playerRb != null) ? playerRb : p.GetComponent<Rigidbody>();
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.linearDamping = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null && capsule.sharedMaterial == null)
        {
            PhysicsMaterial mat = new PhysicsMaterial("TeammateIceMat");
            mat.dynamicFriction = 0.02f;
            mat.staticFriction = 0.02f;
            mat.bounciness = 0f;
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
            capsule.sharedMaterial = mat;
        }
    }

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.GetComponent<FootIKController>() == null)
                animator.gameObject.AddComponent<FootIKController>();
        }

        teammateStick = GetComponentInChildren<StickAttacher>();

        ApplyTeamColor();

        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
            puckRb = puck.GetComponent<Rigidbody>();
            puckCtrl = puck.GetComponent<TestPuckController>();
        }

        TestPlayerController player = FindFirstObjectByType<TestPlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
            playerRb = player.GetComponent<Rigidbody>();
        }

        // Attack the OPPONENT net (the goal the player attacks): its GoalTrigger has IsPlayerGoal=false.
        if (attackGoal == null)
        {
            foreach (GoalTrigger gt in FindObjectsByType<GoalTrigger>(FindObjectsSortMode.None))
            {
                if (!gt.IsPlayerGoal) { attackGoal = gt.transform; break; }
            }
            if (attackGoal != null)
                Debug.Log($"TestTeammate: attacking goal at {attackGoal.position}");
        }

        Debug.Log("TestTeammateController ready");
    }

    private void ApplyTeamColor()
    {
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            Material[] mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = new Material(mats[i]);
                mats[i].color = teamColor;
            }
            rend.materials = mats;
        }
    }

    private void Update()
    {
        if (puckTransform == null) return;

        if (possessionCooldown > 0f) possessionCooldown -= Time.deltaTime;

        if (debugFreezePosition)
        {
            rb.linearVelocity = Vector3.zero;
            UpdateAnimation();
            return;
        }

        UpdatePossession();

        if (hasPuck)
        {
            puckHeldTime += Time.deltaTime;
            if (ShouldReturnPass())
                ReturnPassToPlayer();   // give-and-go: pop it back to the player's run
            else
                SkateWithPuck();
        }
        else
        {
            MoveToSupport();
        }

        UpdateAnimation();

        // Keep on ice + soft containment behind the boards (mirrors the opponent).
        if (Mathf.Abs(transform.position.y) > 0.5f)
        {
            Vector3 pos = transform.position; pos.y = 0f; transform.position = pos;
        }
        if (Mathf.Abs(transform.position.z) > zBoundary)
        {
            Vector3 pos = transform.position;
            float sign = Mathf.Sign(pos.z);
            pos.z = sign * zBoundary; transform.position = pos;
            Vector3 v = rb.linearVelocity;
            if (Mathf.Sign(v.z) == sign) v.z = 0f;
            rb.linearVelocity = v;
        }
    }

    /// <summary>Catch passes / grab a slow loose puck; hand off if the player takes it or it gets far.</summary>
    private void UpdatePossession()
    {
        // Hand off: the puck auto-possessed to the player (they skated in), or it drifted away.
        if (hasPuck)
        {
            float d = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);
            if ((puckCtrl != null && puckCtrl.IsPossessed) || d > possessionRadius * 3f)
                LosePuck();
            return;
        }

        if (possessionCooldown > 0f) return;
        if (puckCtrl != null && puckCtrl.IsPossessed) return; // player has it — don't yank it away

        float dist = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);
        float puckSpeed = PhysicsHelper.SpeedXZ(puckRb.linearVelocity);

        bool slowNear = dist <= possessionRadius && puckSpeed < 5f;

        bool incomingPass = false;
        if (dist <= receiveRadius && puckSpeed > 1f && puckSpeed <= maxReceiveSpeed)
        {
            Vector3 puckDir = PhysicsHelper.FlattenY(puckRb.linearVelocity).normalized;
            Vector3 toMe = PhysicsHelper.DirectionXZ(puckTransform.position, transform.position);
            incomingPass = Vector3.Dot(puckDir, toMe) >= receiveAlignment;
        }

        if (slowNear || incomingPass)
            Possess();
    }

    private void Possess()
    {
        hasPuck = true;
        puckHeldTime = 0f;
        puckRb.linearVelocity = Vector3.zero;

        Collider myCol = GetComponent<Collider>();
        Collider puckCol = puckTransform.GetComponent<Collider>();
        if (myCol != null && puckCol != null)
            Physics.IgnoreCollision(myCol, puckCol, true);

        Debug.Log("Teammate received/possessed the puck!");
    }

    private void LosePuck()
    {
        hasPuck = false;
        possessionCooldown = 0.5f;

        Collider myCol = GetComponent<Collider>();
        Collider puckCol = puckTransform.GetComponent<Collider>();
        if (myCol != null && puckCol != null)
            Physics.IgnoreCollision(myCol, puckCol, false);
    }

    private void SkateWithPuck()
    {
        // Carry toward the opponent net (or straight up-ice if no goal resolved).
        Vector3 dir = attackGoal != null
            ? PhysicsHelper.DirectionXZ(transform.position, attackGoal.position)
            : Vector3.forward;

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, dir * moveSpeed, 5f * Time.deltaTime);
        lastMoveDir = dir;

        Vector3 leadVel = PhysicsHelper.FlattenY(rb.linearVelocity);
        Vector3 puckTarget;
        if (teammateStick != null)
            puckTarget = teammateStick.GetBladeContactPoint() + leadVel / puckFollowSpeed;
        else
            puckTarget = transform.position + dir * stickOffset + leadVel / puckFollowSpeed;
        puckTarget.y = 0.05f;
        puckRb.MovePosition(Vector3.Lerp(puckRb.position, puckTarget, puckFollowSpeed * Time.deltaTime));
        puckRb.linearVelocity = Vector3.zero;

        RotateToward(dir);

        if (attackGoal != null &&
            PhysicsHelper.DistanceXZ(transform.position, attackGoal.position) < shootDistance)
        {
            ShootPuck(dir);
        }
    }

    private void ShootPuck(Vector3 direction)
    {
        hasPuck = false;
        possessionCooldown = 1.5f;

        Collider myCol = GetComponent<Collider>();
        Collider puckCol = puckTransform.GetComponent<Collider>();
        if (myCol != null && puckCol != null)
            Physics.IgnoreCollision(myCol, puckCol, false);

        puckRb.linearVelocity = Vector3.zero;
        puckRb.AddForce(direction * shotPower, ForceMode.Impulse);

        if (animator != null) animator.SetTrigger(ShootHash);
        Debug.Log("Teammate SHOT!");
    }

    /// <summary>
    /// Give-and-go: true once the teammate has held the puck a beat AND the player has broken forward
    /// into space (ahead toward the attack goal, within return range, and doesn't already have a puck).
    /// </summary>
    private bool ShouldReturnPass()
    {
        Transform player = Player;
        if (player == null || puckCtrl == null) return false;
        if (puckHeldTime < returnPassDelay) return false;
        if (puckCtrl.IsPossessed) return false; // player already carrying — nothing to return to

        float dist = PhysicsHelper.DistanceXZ(transform.position, player.position);
        if (dist > returnPassRange) return false;

        // The player must have made a run toward the net (be forward of the teammate, goal-ward).
        Vector3 toPlayer = PhysicsHelper.DirectionXZ(transform.position, player.position);
        Vector3 toGoal = attackGoal != null
            ? PhysicsHelper.DirectionXZ(transform.position, attackGoal.position)
            : Vector3.forward;
        return Vector3.Dot(toPlayer, toGoal) >= returnPassForwardDot;
    }

    /// <summary>Return the puck to the player, led to their skating path so it arrives in stride.</summary>
    private void ReturnPassToPlayer()
    {
        hasPuck = false;
        possessionCooldown = 0.6f; // don't immediately re-grab the pass we just made
        puckHeldTime = 0f;

        Collider myCol = GetComponent<Collider>();
        Collider puckCol = puckTransform.GetComponent<Collider>();
        if (myCol != null && puckCol != null)
            Physics.IgnoreCollision(myCol, puckCol, false);

        Transform player = Player;
        Rigidbody pRb = PlayerRb;
        Vector3 playerVel = pRb != null ? PhysicsHelper.FlattenY(pRb.linearVelocity) : Vector3.zero;
        Vector3 leadPoint = PhysicsHelper.LeadPointXZ(
            transform.position, player.position, playerVel, returnPassPower);
        Vector3 dir = PhysicsHelper.DirectionXZ(transform.position, leadPoint);

        puckRb.linearVelocity = Vector3.zero;
        puckRb.AddForce(dir * returnPassPower, ForceMode.Impulse);

        RotateToward(dir);
        Debug.Log("Teammate RETURN PASS (give-and-go)!");
    }

    /// <summary>
    /// Get open: skate to a support spot ahead of the player toward the attack goal, offset to the
    /// side opposite the player's half so the teammate is a clear passing option.
    /// </summary>
    private void MoveToSupport()
    {
        Transform player = Player;
        if (player == null) return;

        Vector3 goalDir = attackGoal != null
            ? PhysicsHelper.DirectionXZ(player.position, attackGoal.position)
            : Vector3.forward;

        // Support on the opposite side of center from the player so the lane is open.
        float sideSign = player.position.x <= 0f ? 1f : -1f;
        Vector3 side = Vector3.Cross(Vector3.up, goalDir).normalized * supportSide * sideSign;

        Vector3 supportPos = player.position + goalDir * supportLead + side;
        supportPos.y = 0f;

        Vector3 toSupport = PhysicsHelper.FlattenY(supportPos - transform.position);
        Vector3 moveDir = toSupport.sqrMagnitude > 0.25f ? toSupport.normalized : Vector3.zero; // settle within ~0.5m
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, moveDir * moveSpeed, 5f * Time.deltaTime);

        // Face up-ice (toward the play) while supporting so a received pass starts pointed the right way.
        RotateToward(moveDir.sqrMagnitude > 0.04f ? moveDir : goalDir);
        lastMoveDir = goalDir;
    }

    private void RotateToward(Vector3 direction)
    {
        if (direction.magnitude < 0.1f) return;
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        Quaternion target = Quaternion.Euler(0f, angle, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
    }

    private void UpdateAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float speed = flatVel.magnitude;
        animator.SetFloat(SpeedHash, speed);

        float strafe = (speed > 0.05f) ? Vector3.Dot(flatVel, transform.right) / Mathf.Max(0.01f, moveSpeed) : 0f;
        animator.SetFloat(MoveXHash, Mathf.Clamp(strafe, -1f, 1f), 0.1f, Time.deltaTime);

        animator.SetBool(HasPuckHash, hasPuck);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 0.55f, 1f);
        Gizmos.DrawWireSphere(transform.position, possessionRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, receiveRadius);

        if (attackGoal != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, attackGoal.position);
        }
    }
}

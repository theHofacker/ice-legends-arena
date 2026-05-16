using UnityEngine;

/// <summary>
/// Simplified AI opponent for testing checking, puck stealing, and collisions.
/// Chases the puck when free, skates with it when possessed.
/// No formations, no team systems - just basic chase/possess/shoot behavior.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class TestOpponentController : MonoBehaviour
{
    [Header("Movement")]
    [Range(1f, 20f)]
    public float moveSpeed = 6.5f;

    [Range(1f, 20f)]
    public float rotationSpeed = 8f;

    [Header("Puck Possession")]
    [Range(0.5f, 5f)]
    public float possessionRadius = 1.5f;

    [Range(0.3f, 5f)]
    public float stickOffset = 0.7f;

    [Tooltip("How tightly the puck snaps to the stick target while skating")]
    [Range(10f, 80f)]
    public float puckFollowSpeed = 40f;

    [Header("Shooting")]
    [Range(5f, 40f)]
    public float shotPower = 15f;

    [Tooltip("Shoots when this close to opponent's goal")]
    [Range(5f, 20f)]
    public float shootDistance = 12f;

    [Header("Behavior")]
    [Tooltip("Which goal this opponent attacks (auto-finds if empty)")]
    public Transform attackGoal;

    [Tooltip("How close before opponent tries to body check player")]
    [Range(0.5f, 5f)]
    public float checkRange = 1.4f;

    [Tooltip("Knockback force on body check")]
    [Range(5f, 30f)]
    public float checkForce = 15f;

    [Tooltip("Speed the puck flies off at when body check knocks it loose")]
    [Range(2f, 20f)]
    public float puckKnockLooseSpeed = 8f;

    [Tooltip("How long opponent must wait before re-possessing after a body check (gives puck time to separate)")]
    [Range(0f, 2f)]
    public float postCheckRepossessDelay = 0.7f;

    [Header("Containment")]
    [Tooltip("Hard limit on |pz| — keeps opponent from sailing past the back boards. Goals are at z=±23.73.")]
    [Range(20f, 30f)]
    public float zBoundary = 25.5f;

    [Tooltip("Seconds between body check attempts")]
    [Range(1f, 5f)]
    public float checkCooldown = 2f;

    [Header("Stun")]
    [Tooltip("How long opponent is stunned after being checked. Must be >= fall animation length (IH@Hit_Fall_knockback_1 is ~1.7s) or the opponent starts chasing while still mid-fall, which looks like the body is being dragged across the ice.")]
    [Range(0.5f, 3f)]
    public float stunDuration = 2.0f;

    [Header("Appearance")]
    [Tooltip("Color to apply to the model")]
    public Color teamColor = Color.red;

    // State
    private Rigidbody rb;
    private Animator animator;
    private Transform puckTransform;
    private Rigidbody puckRb;
    private Transform playerTransform;
    private bool hasPuck = false;
    private bool isStunned = false;
    private float stunTimer = 0f;
    private float checkTimer = 0f;
    private float possessionCooldown = 0f;
    private Vector3 lastMoveDir = Vector3.forward;

    // Animator hashes
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int HasPuckHash = Animator.StringToHash("HasPuck");
    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int BodyCheckHash = Animator.StringToHash("BodyCheck");
    private static readonly int GotHitHash = Animator.StringToHash("GotHit");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Inspector values win — print them so we know what's actually active.
        Debug.Log($"[TestOpponent] Tuning: possR={possessionRadius}, stick={stickOffset}, puckFollow={puckFollowSpeed}, checkR={checkRange}, knockSpd={puckKnockLooseSpeed}, repossessDelay={postCheckRepossessDelay}");

        // Ice physics - add some damping so opponent doesn't look frantic
        rb.useGravity = false;
        rb.linearDamping = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        // Slippery material
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null && capsule.sharedMaterial == null)
        {
            PhysicsMaterial mat = new PhysicsMaterial("OpponentIceMat");
            mat.dynamicFriction = 0.02f;
            mat.staticFriction = 0.02f;
            mat.bounciness = 0f;
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
            capsule.sharedMaterial = mat;
        }
    }

    private void Start()
    {
        // Find animator and set color
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // Apply team color to all mesh renderers in children
        ApplyTeamColor();

        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
            puckRb = puck.GetComponent<Rigidbody>();
        }

        // Find player
        TestPlayerController player = FindFirstObjectByType<TestPlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // Auto-find attack goal - opponent attacks the player's goal (IsPlayerGoal = true)
        if (attackGoal == null)
        {
            GameObject[] goals = GameObject.FindGameObjectsWithTag("Goal");
            foreach (GameObject goal in goals)
            {
                GoalTrigger trigger = goal.GetComponent<GoalTrigger>();
                if (trigger != null && trigger.IsPlayerGoal)
                {
                    attackGoal = goal.transform;
                    break;
                }
            }
            // Fallback: pick the -Z goal
            if (attackGoal == null)
            {
                float bestZ = float.MaxValue;
                foreach (GameObject goal in goals)
                {
                    if (goal.transform.position.z < bestZ)
                    {
                        bestZ = goal.transform.position.z;
                        attackGoal = goal.transform;
                    }
                }
            }
            if (attackGoal != null)
                Debug.Log($"TestOpponent: Attacking goal at {attackGoal.position}");
        }

        Debug.Log("TestOpponentController ready");
    }

    private void ApplyTeamColor()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            // Create material instances so we don't modify shared materials
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

        // Tick timers
        if (checkTimer > 0f) checkTimer -= Time.deltaTime;
        if (possessionCooldown > 0f) possessionCooldown -= Time.deltaTime;

        // Handle stun
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                SetPlayerCollisionIgnored(false);
                SetPuckCollisionIgnored(false);
                Debug.Log("Opponent recovered from stun");
            }
            // Two-phase decay: a brief recoil window so the hit still has a visible
            // launch, then a hard brake so the body comes to rest under the fall
            // animation. The original single-rate Lerp let the 20 m/s knockback
            // glide ~3m across the ice while the puck was traveling in the same
            // direction with the player, which read visually as a tether.
            float timeStunned = stunDuration - stunTimer;
            float brakeRate = timeStunned < 0.12f ? 5f : 30f;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, brakeRate * Time.deltaTime);
            if (rb.linearVelocity.sqrMagnitude < 0.25f) rb.linearVelocity = Vector3.zero;
            UpdateAnimation();
            return;
        }

        // Check puck possession
        CheckPossession();

        if (hasPuck)
        {
            SkateWithPuck();
        }
        else
        {
            ChasePuckOrCheckPlayer();
        }

        UpdateAnimation();

        // Keep on ice
        if (Mathf.Abs(transform.position.y) > 0.5f)
        {
            Vector3 pos = transform.position;
            pos.y = 0f;
            transform.position = pos;
        }

        // Soft containment behind the back boards (not the goal line). Goals
        // are at z=±23.73, so allow a bit past so the opponent can skate around
        // and behind the net. Without this clamp the AI sails to z=±27 and
        // traces wide arcs along the wall trying to recover.
        if (Mathf.Abs(transform.position.z) > zBoundary)
        {
            Vector3 pos = transform.position;
            float sign = Mathf.Sign(pos.z);
            pos.z = sign * zBoundary;
            transform.position = pos;

            Vector3 v = rb.linearVelocity;
            if (Mathf.Sign(v.z) == sign) v.z = 0f;
            rb.linearVelocity = v;
        }
    }

    private void CheckPossession()
    {
        float distToPuck = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);

        // Try to possess - only if puck is slow AND player doesn't have it
        if (!hasPuck && possessionCooldown <= 0f && distToPuck <= possessionRadius)
        {
            // Don't steal if puck is moving fast (just been shot)
            if (PhysicsHelper.SpeedXZ(puckRb.linearVelocity) > 5f)
                return;

            // Don't steal if player has possession
            TestPuckController puckCtrl = puckTransform.GetComponent<TestPuckController>();
            if (puckCtrl != null && puckCtrl.IsPossessed)
                return;

            hasPuck = true;
            puckRb.linearVelocity = Vector3.zero;
            Debug.Log("Opponent possessed puck!");

            Collider myCol = GetComponent<Collider>();
            Collider puckCol = puckTransform.GetComponent<Collider>();
            if (myCol != null && puckCol != null)
                Physics.IgnoreCollision(myCol, puckCol, true);
        }

        // Lose possession if puck is far
        if (hasPuck && distToPuck > possessionRadius * 3f)
        {
            LosePuck();
        }
    }

    private void SkateWithPuck()
    {
        if (attackGoal == null) return;

        // Move toward goal
        Vector3 dirToGoal = PhysicsHelper.DirectionXZ(transform.position, attackGoal.position);
        Vector3 targetVel = dirToGoal * moveSpeed;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, 5f * Time.deltaTime);
        lastMoveDir = dirToGoal;

        // Make puck follow
        Vector3 puckTarget = transform.position + dirToGoal * stickOffset;
        puckTarget.y = 0.05f;
        puckRb.MovePosition(Vector3.Lerp(puckRb.position, puckTarget, puckFollowSpeed * Time.deltaTime));
        puckRb.linearVelocity = Vector3.zero;

        RotateToward(dirToGoal);

        // Shoot when close to goal
        float distToGoal = PhysicsHelper.DistanceXZ(transform.position, attackGoal.position);
        if (distToGoal < shootDistance)
        {
            ShootPuck(dirToGoal);
        }
    }

    private void ChasePuckOrCheckPlayer()
    {
        if (playerTransform == null) return;

        float distToPlayer = PhysicsHelper.DistanceXZ(transform.position, playerTransform.position);

        // If player has the puck and we're close, try body check
        TestPuckController puckCtrl = puckTransform.GetComponent<TestPuckController>();
        bool playerHasPuck = puckCtrl != null && puckCtrl.IsPossessed;

        if (playerHasPuck && distToPlayer < checkRange && checkTimer <= 0f)
        {
            BodyCheckPlayer();
            return;
        }

        // Chase the puck with smooth acceleration
        Vector3 dirToPuck = PhysicsHelper.DirectionXZ(transform.position, puckTransform.position);

        // Brake-before-turn: if the puck is behind our current heading, kill
        // velocity first instead of arcing around. Eliminates the "wide circle"
        // behavior when the puck ricochets behind us after a shot.
        Vector3 currentVel = rb.linearVelocity;
        currentVel.y = 0f;
        if (currentVel.magnitude > 1.5f &&
            Vector3.Dot(currentVel.normalized, dirToPuck) < -0.2f)
        {
            rb.linearVelocity = Vector3.Lerp(currentVel, Vector3.zero, 8f * Time.deltaTime);
            RotateToward(dirToPuck);
            return;
        }

        Vector3 targetVel = dirToPuck * moveSpeed;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, 5f * Time.deltaTime);
        lastMoveDir = dirToPuck;
        RotateToward(dirToPuck);
    }

    private void BodyCheckPlayer()
    {
        checkTimer = checkCooldown;

        if (animator != null)
            animator.SetTrigger(BodyCheckHash);

        Vector3 checkDir = PhysicsHelper.DirectionXZ(transform.position, playerTransform.position);
        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = checkDir * checkForce;
        }

        TestPuckController puckCtrl = puckTransform.GetComponent<TestPuckController>();
        if (puckCtrl != null && puckCtrl.IsPossessed)
        {
            puckCtrl.ForceLosePuck();
            puckRb.linearVelocity = PhysicsHelper.RandomDirectionXZ() * puckKnockLooseSpeed;
            // Force a re-possess delay so the puck visibly separates from the
            // opponent before they can grab it again. Without this the opponent
            // re-grabs on the next physics frame and the "steal" looks magnetic.
            possessionCooldown = postCheckRepossessDelay;
        }

        Debug.Log("Opponent BODY CHECK!");
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

        if (animator != null)
            animator.SetTrigger(ShootHash);

        Debug.Log("Opponent SHOT!");
    }

    private void LosePuck()
    {
        hasPuck = false;
        possessionCooldown = 0.5f;

        Collider myCol = GetComponent<Collider>();
        Collider puckCol = puckTransform.GetComponent<Collider>();
        if (myCol != null && puckCol != null)
            Physics.IgnoreCollision(myCol, puckCol, false);

        Debug.Log("Opponent lost puck");
    }

    /// <summary>
    /// Called by player when they body check this opponent.
    /// </summary>
    public void GetBodyChecked(Vector3 knockbackDir, float force)
    {
        isStunned = true;
        stunTimer = stunDuration;
        rb.linearVelocity = knockbackDir * force;

        if (hasPuck)
        {
            LosePuck();
            puckRb.linearVelocity = PhysicsHelper.RandomDirectionXZ() * puckKnockLooseSpeed;
        }

        if (animator != null)
            animator.SetTrigger(GotHitHash);

        // While we're down, let the player pass through us — otherwise the player's
        // capsule shoves our collapsed body around and it looks like an invisible
        // tether dragging us behind them. Same goes for the puck: it's glued to
        // the player via MovePosition each FixedUpdate, and its swept path will
        // shove our prone capsule across the ice if collision stays enabled.
        SetPlayerCollisionIgnored(true);
        SetPuckCollisionIgnored(true);

        Debug.Log($"Opponent got BODY CHECKED! Stunned for {stunDuration}s");
    }

    /// <summary>
    /// Ignore (or restore) collision between this opponent and the puck. Used so the
    /// player's possessed puck doesn't drag the stunned body around on its sweep path.
    /// </summary>
    private void SetPuckCollisionIgnored(bool ignore)
    {
        if (puckTransform == null) return;
        Collider myCol = GetComponent<Collider>();
        Collider puckCol = puckTransform.GetComponent<Collider>();
        if (myCol != null && puckCol != null)
            Physics.IgnoreCollision(myCol, puckCol, ignore);
    }

    /// <summary>
    /// Ignore (or restore) collision between this opponent and the player. Used to
    /// keep the stunned ragdoll body from being pushed by the player's capsule.
    /// Iterates ALL colliders in the player's hierarchy (capsule, hockey stick,
    /// any future equipment) because Physics.IgnoreCollision is per-pair, not per
    /// Rigidbody — and equipment attached to bone GameObjects sweeps through space
    /// via the skating animation, pushing the prone body if not excluded.
    /// </summary>
    private void SetPlayerCollisionIgnored(bool ignore)
    {
        if (playerTransform == null) return;
        Collider myCol = GetComponent<Collider>();
        if (myCol == null) return;
        Collider[] playerCols = playerTransform.GetComponentsInChildren<Collider>(includeInactive: false);
        foreach (Collider pc in playerCols)
        {
            if (pc != null) Physics.IgnoreCollision(myCol, pc, ignore);
        }
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

        // MoveX = signed strafe relative to facing, [-1, +1]. Drives SkatingWithPuck blend.
        float strafe = (speed > 0.05f) ? Vector3.Dot(flatVel, transform.right) / Mathf.Max(0.01f, moveSpeed) : 0f;
        animator.SetFloat(MoveXHash, Mathf.Clamp(strafe, -1f, 1f), 0.1f, Time.deltaTime);

        animator.SetBool(HasPuckHash, hasPuck);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, checkRange);

        if (attackGoal != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, attackGoal.position);
        }
    }
}

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
    public float moveSpeed = 8f;

    [Range(1f, 20f)]
    public float rotationSpeed = 8f;

    [Header("Puck Possession")]
    [Range(0.5f, 5f)]
    public float possessionRadius = 2.5f;

    [Range(0.5f, 5f)]
    public float stickOffset = 1.2f;

    [Header("Shooting")]
    [Range(5f, 40f)]
    public float shotPower = 15f;

    [Tooltip("Shoots when this close to opponent's goal")]
    [Range(5f, 20f)]
    public float shootDistance = 12f;

    [Header("Behavior")]
    [Tooltip("Which goal this opponent attacks (drag the goal GameObject here)")]
    public Transform attackGoal;

    [Tooltip("How close before opponent tries to body check player")]
    [Range(1f, 5f)]
    public float checkRange = 2.5f;

    [Tooltip("Knockback force on body check")]
    [Range(5f, 30f)]
    public float checkForce = 15f;

    [Tooltip("Seconds between body check attempts")]
    [Range(1f, 5f)]
    public float checkCooldown = 2f;

    [Header("Stun")]
    [Tooltip("How long opponent is stunned after being checked")]
    [Range(0.5f, 3f)]
    public float stunDuration = 1.5f;

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
    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int BodyCheckHash = Animator.StringToHash("BodyCheck");
    private static readonly int GotHitHash = Animator.StringToHash("GotHit");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Ice physics
        rb.useGravity = false;
        rb.linearDamping = 0f;
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
        // Find animator
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

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

        // Auto-find attack goal if not assigned (pick the +Z goal)
        if (attackGoal == null)
        {
            GameObject[] goals = GameObject.FindGameObjectsWithTag("Goal");
            float bestZ = float.MinValue;
            foreach (GameObject goal in goals)
            {
                if (goal.transform.position.z > bestZ)
                {
                    bestZ = goal.transform.position.z;
                    attackGoal = goal.transform;
                }
            }
            if (attackGoal != null)
                Debug.Log($"TestOpponent: Auto-targeting goal at {attackGoal.position}");
        }

        Debug.Log("TestOpponentController ready");
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
                Debug.Log("Opponent recovered from stun");
            }
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 5f * Time.deltaTime);
            UpdateAnimation();
            return;
        }

        // Check puck possession
        CheckPossession();

        if (hasPuck)
        {
            // Skate toward goal and shoot when close
            SkateWithPuck();
        }
        else
        {
            // Chase puck or check player
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
    }

    private void CheckPossession()
    {
        float distToPuck = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);

        // Try to possess
        if (!hasPuck && possessionCooldown <= 0f && distToPuck <= possessionRadius &&
            PhysicsHelper.SpeedXZ(puckRb.linearVelocity) < 8f)
        {
            // Check if player's TestPuckController already has it
            TestPuckController puckCtrl = puckTransform.GetComponent<TestPuckController>();
            if (puckCtrl != null && puckCtrl.IsPossessed)
            {
                return; // Player has it, can't steal (need to body check first)
            }

            hasPuck = true;
            puckRb.linearVelocity = Vector3.zero;
            Debug.Log("Opponent possessed puck!");

            // Ignore collision
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
        rb.linearVelocity = dirToGoal * moveSpeed;
        lastMoveDir = dirToGoal;

        // Make puck follow
        Vector3 puckTarget = transform.position + dirToGoal * stickOffset;
        puckTarget.y = 0.05f;
        puckRb.MovePosition(Vector3.Lerp(puckRb.position, puckTarget, 25f * Time.deltaTime));
        puckRb.linearVelocity = Vector3.zero;

        // Rotate to face goal
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
        float distToPuck = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);

        // If player has the puck and we're close, try body check
        TestPuckController puckCtrl = puckTransform.GetComponent<TestPuckController>();
        bool playerHasPuck = puckCtrl != null && puckCtrl.IsPossessed;

        if (playerHasPuck && distToPlayer < checkRange && checkTimer <= 0f)
        {
            BodyCheckPlayer();
            return;
        }

        // Chase the puck
        Vector3 dirToPuck = PhysicsHelper.DirectionXZ(transform.position, puckTransform.position);
        rb.linearVelocity = dirToPuck * moveSpeed;
        lastMoveDir = dirToPuck;
        RotateToward(dirToPuck);
    }

    private void BodyCheckPlayer()
    {
        checkTimer = checkCooldown;

        // Trigger animation
        if (animator != null)
            animator.SetTrigger(BodyCheckHash);

        // Apply force to player
        Vector3 checkDir = PhysicsHelper.DirectionXZ(transform.position, playerTransform.position);
        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = checkDir * checkForce;
        }

        // Force player to lose puck
        TestPuckController puckCtrl = puckTransform.GetComponent<TestPuckController>();
        if (puckCtrl != null && puckCtrl.IsPossessed)
        {
            puckCtrl.ForceLosePuck();

            // Pop puck in random direction
            Vector3 randomDir = PhysicsHelper.RandomDirectionXZ();
            puckRb.linearVelocity = randomDir * 10f;
        }

        Debug.Log("Opponent BODY CHECK!");
    }

    private void ShootPuck(Vector3 direction)
    {
        hasPuck = false;
        possessionCooldown = 1f;

        // Re-enable collision
        Collider myCol = GetComponent<Collider>();
        Collider puckCol = puckTransform.GetComponent<Collider>();
        if (myCol != null && puckCol != null)
            Physics.IgnoreCollision(myCol, puckCol, false);

        // Shoot
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
    /// Called by player when they body check this opponent
    /// </summary>
    public void GetBodyChecked(Vector3 knockbackDir, float force)
    {
        isStunned = true;
        stunTimer = stunDuration;
        rb.linearVelocity = knockbackDir * force;

        if (hasPuck)
        {
            LosePuck();
            puckRb.linearVelocity = PhysicsHelper.RandomDirectionXZ() * 8f;
        }

        if (animator != null)
            animator.SetTrigger(GotHitHash);

        Debug.Log($"Opponent got BODY CHECKED! Stunned for {stunDuration}s");
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
        if (animator == null) return;
        float speed = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
        animator.SetFloat(SpeedHash, speed);
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

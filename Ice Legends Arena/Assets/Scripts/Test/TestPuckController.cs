using UnityEngine;

/// <summary>
/// Simplified puck controller for testing possession, following, and shooting.
/// No dependencies on teams, opponents, formations, or context buttons.
/// Uses keyboard shortcuts: Space = shoot, F = pass forward.
/// Rink: X = width, Z = length (goal-to-goal), Y = height.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class TestPuckController : MonoBehaviour
{
    [Header("Possession")]
    [Tooltip("Distance to auto-possess puck")]
    [Range(0.5f, 8f)]
    public float possessionRadius = 3f;

    [Tooltip("How far in front of player the puck sits")]
    [Range(0.5f, 5f)]
    public float stickOffset = 1.5f;

    [Tooltip("How smoothly puck follows player")]
    [Range(5f, 50f)]
    public float followSpeed = 25f;

    [Header("Shooting")]
    [Tooltip("Shot power (impulse force)")]
    [Range(5f, 50f)]
    public float shotPower = 20f;

    [Tooltip("Seconds before puck can be re-possessed after shot")]
    [Range(0.3f, 3f)]
    public float shotCooldown = 1f;

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
    private bool isPossessed = false;
    private float cooldownTimer = 0f;
    private Vector3 lastPlayerDir = Vector3.forward;

    public bool IsPossessed => isPossessed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        puckCollider = GetComponent<SphereCollider>();

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
            Debug.Log($"TestPuckController: Tracking player {playerTransform.name}");
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

        // Shoot: Space key
        if (isPossessed && Input.GetKeyDown(KeyCode.Space))
        {
            Shoot();
        }

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
        if (isPossessed && playerTransform != null)
        {
            // Follow player - puck sits in front of player in facing direction
            Vector3 dir = lastPlayerDir.magnitude > 0.1f ? lastPlayerDir : Vector3.forward;
            dir = PhysicsHelper.FlattenY(dir).normalized;

            Vector3 target = playerTransform.position + dir * stickOffset;
            target.y = 0.05f; // Just above ice

            Vector3 newPos = Vector3.Lerp(rb.position, target, followSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            // Zero velocity while possessed
            rb.linearVelocity = Vector3.zero;
        }
    }

    private void Shoot()
    {
        if (!isPossessed) return;

        // Release possession
        isPossessed = false;
        cooldownTimer = shotCooldown;

        // Re-enable collision
        Collider playerCol = playerTransform.GetComponent<Collider>();
        if (playerCol != null)
            Physics.IgnoreCollision(puckCollider, playerCol, false);

        // Apply shot force in player's facing direction
        Vector3 shotDir = lastPlayerDir.magnitude > 0.1f ? lastPlayerDir : Vector3.forward;
        shotDir = PhysicsHelper.FlattenY(shotDir).normalized;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(shotDir * shotPower, ForceMode.Impulse);

        Debug.Log($"SHOT! dir={shotDir:F2}, power={shotPower}");
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

using UnityEngine;

/// <summary>
/// Simplified player controller for testing basic 3D movement.
/// No dependencies on formations, teams, abilities, or other systems.
/// Rink coordinate system: X = width (left/right), Z = length (goal-to-goal), Y = height.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class TestPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Range(1f, 30f)]
    public float moveSpeed = 12f;

    [Range(1f, 30f)]
    public float acceleration = 15f;

    [Range(1f, 30f)]
    public float deceleration = 20f;

    [Header("Rotation")]
    [Range(1f, 20f)]
    public float rotationSpeed = 10f;

    [Header("Debug")]
    public bool logInput = false;

    private Rigidbody rb;
    private InputManager inputManager;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Physics setup for ice surface
        rb.useGravity = false;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
    }

    private void Start()
    {
        inputManager = InputManager.Instance;
        if (inputManager == null)
        {
            Debug.LogError("TestPlayerController: No InputManager found!");
        }
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

        // Calculate target velocity
        Vector3 targetVelocity = worldDir * moveSpeed;

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

        // Safety: keep on ice
        if (Mathf.Abs(transform.position.y) > 0.5f)
        {
            Vector3 pos = transform.position;
            pos.y = 0f;
            transform.position = pos;
        }
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

using UnityEngine;

/// <summary>
/// Drives animations for AI-controlled players (teammates and opponents).
/// Similar to PlayerAnimator but reads velocity directly from Rigidbody
/// without needing input or shooting/checking controller references.
/// Attach to any AI player GameObject that has a 3D model child with an Animator.
/// </summary>
public class AIAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Animator on the 3D model child. Auto-finds if empty.")]
    [SerializeField] private Animator animator;

    [Tooltip("The transform to rotate (3D model child). Auto-finds if empty.")]
    [SerializeField] private Transform modelTransform;

    [Header("Model Rotation")]
    [Tooltip("How quickly the model rotates to face movement direction")]
    [Range(1f, 20f)]
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Speed Thresholds")]
    [Tooltip("Minimum velocity to trigger skating animation")]
    [Range(0.01f, 1f)]
    [SerializeField] private float skatingThreshold = 0.1f;

    // Cached references
    private Rigidbody rb;

    // Animator parameter hashes
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // State
    private Vector3 lastFacingDirection = Vector3.forward;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning($"AIAnimator: No Animator found on {gameObject.name} or its children.", this);
            enabled = false;
            return;
        }

        if (modelTransform == null)
        {
            modelTransform = animator.transform;
        }
    }

    private void Update()
    {
        if (animator == null || rb == null) return;

        // Update speed parameter using XZ speed only
        float speed = PhysicsHelper.SpeedXZ(rb.linearVelocity);
        animator.SetFloat(SpeedHash, speed);

        RotateModelToFaceDirection();
    }

    /// <summary>
    /// Rotates the 3D model around the Y axis to face movement direction on XZ plane.
    /// </summary>
    private void RotateModelToFaceDirection()
    {
        if (modelTransform == null) return;

        Vector3 velocity = PhysicsHelper.FlattenY(rb.linearVelocity);

        if (velocity.magnitude > skatingThreshold)
        {
            lastFacingDirection = velocity.normalized;
        }

        float targetAngle = Mathf.Atan2(lastFacingDirection.x, lastFacingDirection.z) * Mathf.Rad2Deg;

        Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
        modelTransform.localRotation = Quaternion.Slerp(
            modelTransform.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}

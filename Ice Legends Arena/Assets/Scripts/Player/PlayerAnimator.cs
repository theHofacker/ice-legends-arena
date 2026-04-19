using UnityEngine;

/// <summary>
/// Bridges player gameplay state to the Animator for 3D character animation.
/// Reads movement, shooting, and checking state to set animator parameters.
/// Rotates the 3D model to face movement direction on the XZ plane.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The 3D model child object (e.g., Y Bot). If empty, searches children for an Animator.")]
    [SerializeField] private Animator animator;

    [Tooltip("The transform to rotate (3D model child). If empty, uses the animator's transform.")]
    [SerializeField] private Transform modelTransform;

    [Header("Model Rotation")]
    [Tooltip("How quickly the model rotates to face movement direction")]
    [Range(1f, 20f)]
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Speed Thresholds")]
    [Tooltip("Minimum velocity to trigger skating animation")]
    [Range(0.01f, 1f)]
    [SerializeField] private float skatingThreshold = 0.1f;

    // Cached component references
    private Rigidbody rb;
    private ShootingController shootingController;
    private CheckingController checkingController;
    private PassingController passingController;

    // Animator parameter hashes (cached for performance)
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int PassHash = Animator.StringToHash("Pass");
    private static readonly int BodyCheckHash = Animator.StringToHash("BodyCheck");
    private static readonly int GotHitHash = Animator.StringToHash("GotHit");
    private static readonly int CelebrateHash = Animator.StringToHash("Celebrate");
    private static readonly int BlockHash = Animator.StringToHash("Block");

    // State tracking
    private bool wasChargingShot = false;
    private bool wasChargingSaucer = false;
    private Vector3 lastFacingDirection = Vector3.forward;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        shootingController = GetComponent<ShootingController>();
        checkingController = GetComponent<CheckingController>();
        passingController = GetComponent<PassingController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning("PlayerAnimator: No Animator found on this object or its children. Assign a 3D model with an Animator.", this);
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
        if (animator == null) return;

        UpdateSpeedParameter();
        UpdateShootingState();
        UpdatePassingState();
        UpdateCheckingState();
        RotateModelToFaceDirection();
    }

    private void UpdateSpeedParameter()
    {
        if (rb == null) return;

        // Use XZ speed only (ignore any vertical velocity)
        float speed = PhysicsHelper.SpeedXZ(rb.linearVelocity);
        animator.SetFloat(SpeedHash, speed);
    }

    private void UpdateShootingState()
    {
        if (shootingController == null) return;

        bool isCharging = shootingController.IsChargingShot;
        animator.SetBool(IsChargingHash, isCharging);

        if (wasChargingShot && !isCharging)
        {
            animator.SetTrigger(ShootHash);
        }

        wasChargingShot = isCharging;
    }

    private void UpdatePassingState()
    {
        if (passingController == null) return;

        bool isChargingSaucer = passingController.IsChargingSaucerPass;

        if (wasChargingSaucer && !isChargingSaucer)
        {
            animator.SetTrigger(PassHash);
        }

        wasChargingSaucer = isChargingSaucer;
    }

    private void UpdateCheckingState()
    {
        if (checkingController == null) return;
    }

    /// <summary>
    /// Rotates the 3D model around the Y axis to face movement direction on XZ plane.
    /// </summary>
    private void RotateModelToFaceDirection()
    {
        if (modelTransform == null || rb == null) return;

        Vector3 velocity = PhysicsHelper.FlattenY(rb.linearVelocity);

        if (velocity.magnitude > skatingThreshold)
        {
            lastFacingDirection = velocity.normalized;
        }

        // Convert XZ direction to Y-axis rotation
        // Atan2(x, z) gives the correct angle for Unity's Y rotation
        float targetAngle = Mathf.Atan2(lastFacingDirection.x, lastFacingDirection.z) * Mathf.Rad2Deg;

        Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
        modelTransform.localRotation = Quaternion.Slerp(
            modelTransform.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    // --- Public methods for other scripts to trigger animations ---

    public void TriggerPass()
    {
        if (animator != null)
            animator.SetTrigger(PassHash);
    }

    public void TriggerBodyCheck()
    {
        if (animator != null)
            animator.SetTrigger(BodyCheckHash);
    }

    public void TriggerGotHit()
    {
        if (animator != null)
            animator.SetTrigger(GotHitHash);
    }

    public void TriggerCelebration()
    {
        if (animator != null)
            animator.SetTrigger(CelebrateHash);
    }

    public void TriggerBlock()
    {
        if (animator != null)
            animator.SetTrigger(BlockHash);
    }
}

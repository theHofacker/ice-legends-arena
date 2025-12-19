using UnityEngine;

/// <summary>
/// Handles player shooting mechanics including wrist shots, slapshots, and auto-aim.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ShootingController : MonoBehaviour
{
    [Header("Shot Power Settings")]
    [Tooltip("Base power for wrist shot (tap)")]
    [Range(5f, 50f)]
    [SerializeField] private float wristShotPower = 20f;

    [Tooltip("Power multiplier for slapshot (hold)")]
    [Range(1.5f, 3f)]
    [SerializeField] private float slapshotMultiplier = 2f;

    [Header("Auto-Aim Settings")]
    [Tooltip("Enable auto-aim toward goal")]
    [SerializeField] private bool autoAimEnabled = true;

    [Tooltip("Auto-aim strength (0 = no aim assist, 1 = direct to goal)")]
    [Range(0f, 1f)]
    [SerializeField] private float autoAimStrength = 0.6f;

    [Tooltip("Maximum angle for auto-aim (degrees)")]
    [Range(0f, 90f)]
    [SerializeField] private float maxAutoAimAngle = 45f;

    [Header("Possession Settings")]
    [Tooltip("Distance to consider player 'has' the puck")]
    [Range(0.5f, 3f)]
    [SerializeField] private float possessionRadius = 1.5f;

    [Header("Visual Feedback")]
    [SerializeField] private bool showAimDebug = true;

    // Component references
    private Rigidbody2D playerRb;
    private Transform puckTransform;
    private Rigidbody2D puckRb;
    private Transform nearestGoal;

    // State
    private Vector2 lastMoveDirection = Vector2.right;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
            puckRb = puck.GetComponent<Rigidbody2D>();
        }
        else
        {
            Debug.LogError("ShootingController: No Puck found! Tag your puck with 'Puck' tag.");
        }

        // Find nearest goal (for now, just find any goal)
        // TODO: Determine which goal to shoot at based on team
        GameObject[] goals = GameObject.FindGameObjectsWithTag("Goal");
        if (goals.Length > 0)
        {
            nearestGoal = goals[0].transform;
            Debug.Log($"ShootingController: Aiming at goal: {nearestGoal.name}");
        }
        else
        {
            Debug.LogWarning("ShootingController: No goals found. Auto-aim disabled.");
        }

        // Subscribe to button manager
        ContextButtonManager.Instance.OnShootRequested += HandleShootRequested;
    }

    private void Update()
    {
        // Track movement direction for shooting
        if (InputManager.Instance != null)
        {
            Vector2 moveInput = InputManager.Instance.MoveInput;
            if (moveInput.magnitude > 0.1f)
            {
                lastMoveDirection = moveInput.normalized;
            }
        }
    }

    private void HandleShootRequested(bool isCharged)
    {
        // Check if we have possession
        if (!HasPossession())
        {
            Debug.Log("Cannot shoot - no possession of puck");
            return;
        }

        // Execute shot
        if (isCharged)
        {
            ExecuteSlapshot();
        }
        else
        {
            ExecuteWristShot();
        }
    }

    private void ExecuteWristShot()
    {
        Vector2 shotDirection = CalculateShotDirection();
        ApplyShotForce(shotDirection, wristShotPower);

        Debug.Log($"Wrist shot! Power: {wristShotPower}, Direction: {shotDirection}");
    }

    private void ExecuteSlapshot()
    {
        Vector2 shotDirection = CalculateShotDirection();
        float slapshotPower = wristShotPower * slapshotMultiplier;
        ApplyShotForce(shotDirection, slapshotPower);

        Debug.Log($"Slapshot! Power: {slapshotPower}, Direction: {shotDirection}");

        // TODO: Add screen shake effect
    }

    private Vector2 CalculateShotDirection()
    {
        // Start with player's facing direction
        Vector2 baseDirection = lastMoveDirection;

        // Apply auto-aim if enabled and goal exists
        if (autoAimEnabled && nearestGoal != null)
        {
            // Calculate direction to goal
            Vector2 toGoal = (nearestGoal.position - transform.position).normalized;

            // Check if goal is within auto-aim cone
            float angleToGoal = Vector2.Angle(baseDirection, toGoal);

            if (angleToGoal <= maxAutoAimAngle)
            {
                // Blend between player direction and goal direction
                baseDirection = Vector2.Lerp(baseDirection, toGoal, autoAimStrength).normalized;
            }
        }

        return baseDirection;
    }

    private void ApplyShotForce(Vector2 direction, float power)
    {
        if (puckRb == null) return;

        // Apply impulse force to puck
        puckRb.linearVelocity = Vector2.zero; // Reset current velocity
        puckRb.AddForce(direction * power, ForceMode2D.Impulse);
    }

    private bool HasPossession()
    {
        if (puckTransform == null) return false;

        float distance = Vector2.Distance(transform.position, puckTransform.position);
        return distance <= possessionRadius;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showAimDebug) return;

        // Draw possession radius
        Gizmos.color = HasPossession() ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        // Draw shot direction
        if (Application.isPlaying)
        {
            Vector2 shotDir = CalculateShotDirection();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, shotDir * 5f);

            // Draw auto-aim cone
            if (autoAimEnabled && nearestGoal != null)
            {
                Gizmos.color = Color.cyan;
                Vector2 toGoal = (nearestGoal.position - transform.position).normalized;
                Gizmos.DrawRay(transform.position, toGoal * 5f);
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnShootRequested -= HandleShootRequested;
        }
    }
}

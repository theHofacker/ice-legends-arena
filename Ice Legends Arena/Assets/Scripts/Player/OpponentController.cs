using UnityEngine;

/// <summary>
/// Simple AI opponent for testing defensive mechanics (poke checks, body checks).
/// 3D physics: movement on XZ plane, Y = height.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class OpponentController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("How fast opponent moves")]
    [Range(1f, 10f)]
    public float moveSpeed = 3f;

    [Tooltip("Movement pattern")]
    public MovementPattern pattern = MovementPattern.Circle;

    [Header("Puck Settings")]
    [Tooltip("Distance to auto-possess puck")]
    [Range(0.5f, 3f)]
    public float possessionRadius = 1.5f;

    [Header("Visual Settings")]
    [Tooltip("Color to distinguish from player")]
    public Color opponentColor = Color.red;

    [Header("Starting Position")]
    public Vector3 centerPoint = Vector3.zero;
    public float circleRadius = 5f;

    // Component references
    private Rigidbody rb;
    private SpriteRenderer spriteRenderer;
    private Transform puckTransform;
    private Rigidbody puckRb;

    // State
    private bool hasPuck = false;
    private float angle = 0f;
    private float possessionCooldown = 0f;

    public enum MovementPattern
    {
        Circle,
        BackAndForth,
        Figure8,
        Stationary
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Set up 3D physics
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        rb.linearDamping = 2f;
    }

    private void Start()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = opponentColor;
        }

        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
            puckRb = puck.GetComponent<Rigidbody>();
        }

        // Set starting position
        if (centerPoint == Vector3.zero)
        {
            centerPoint = transform.position;
        }
        else
        {
            transform.position = centerPoint;
        }
    }

    private void Update()
    {
        if (puckTransform == null) return;

        if (possessionCooldown > 0f)
        {
            possessionCooldown -= Time.deltaTime;
        }

        CheckPuckPossession();

        if (!hasPuck)
        {
            MoveTowardPuck();
        }
        else
        {
            MoveWithPattern();
        }
    }

    private void CheckPuckPossession()
    {
        float distance = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);

        if (!hasPuck && distance <= possessionRadius && puckRb != null && possessionCooldown <= 0f)
        {
            if (PhysicsHelper.SpeedXZ(puckRb.linearVelocity) < 8f)
            {
                hasPuck = true;
                puckRb.linearVelocity *= 0.5f;

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.yellow;
                }

                Debug.Log($"{gameObject.name} possessed puck");
            }
        }
        else if (hasPuck && distance > possessionRadius * 3)
        {
            hasPuck = false;
            possessionCooldown = 1f;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = opponentColor;
            }

            Debug.Log($"{gameObject.name} lost possession (cooldown: 1s)");
        }
    }

    public void ApplyPokeCheckStun()
    {
        hasPuck = false;
        possessionCooldown = 1f;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = opponentColor;
        }

        Debug.Log($"{gameObject.name} got poke checked! Stunned for 1s");
    }

    public void ApplyBodyCheckStun(float stunDuration)
    {
        hasPuck = false;
        possessionCooldown = stunDuration;

        Animator modelAnimator = GetComponentInChildren<Animator>();
        if (modelAnimator != null)
        {
            modelAnimator.SetTrigger("GotHit");
        }

        if (spriteRenderer != null)
        {
            StartCoroutine(FlashColorCoroutine(Color.red, stunDuration));
        }

        Debug.Log($"{gameObject.name} got BODY CHECKED! Stunned for {stunDuration}s");
    }

    private System.Collections.IEnumerator FlashColorCoroutine(Color flashColor, float duration)
    {
        if (spriteRenderer == null) yield break;

        float elapsed = 0f;
        float flashSpeed = 5f;

        while (elapsed < duration)
        {
            float t = Mathf.PingPong(elapsed * flashSpeed, 1f);
            spriteRenderer.color = Color.Lerp(opponentColor, flashColor, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        spriteRenderer.color = opponentColor;
    }

    private void MoveTowardPuck()
    {
        Vector3 direction = PhysicsHelper.DirectionXZ(transform.position, puckTransform.position);
        rb.linearVelocity = direction * moveSpeed;
    }

    private void MoveWithPattern()
    {
        switch (pattern)
        {
            case MovementPattern.Circle:
                MoveInCircle();
                break;

            case MovementPattern.BackAndForth:
                MoveBackAndForth();
                break;

            case MovementPattern.Figure8:
                MoveFigure8();
                break;

            case MovementPattern.Stationary:
                rb.linearVelocity = Vector3.zero;
                break;
        }

        // Make puck follow opponent
        if (puckTransform != null && hasPuck)
        {
            Vector3 puckTargetPos = transform.position + PhysicsHelper.FlattenY(rb.linearVelocity).normalized * 0.8f;
            Vector3 puckDirection = puckTargetPos - puckTransform.position;
            puckDirection.y = 0f;

            if (puckRb != null)
            {
                puckRb.linearVelocity = puckDirection * 10f;
            }
        }
    }

    private void MoveInCircle()
    {
        angle += Time.deltaTime * (moveSpeed / circleRadius);

        // Circle on XZ plane
        Vector3 targetPos = centerPoint + new Vector3(
            Mathf.Cos(angle) * circleRadius,
            0f,
            Mathf.Sin(angle) * circleRadius
        );

        Vector3 direction = PhysicsHelper.DirectionXZ(transform.position, targetPos);
        rb.linearVelocity = direction * moveSpeed;
    }

    private void MoveBackAndForth()
    {
        float x = centerPoint.x + Mathf.Sin(Time.time * moveSpeed * 0.5f) * circleRadius;
        Vector3 targetPos = new Vector3(x, 0f, centerPoint.z);

        Vector3 direction = PhysicsHelper.DirectionXZ(transform.position, targetPos);
        rb.linearVelocity = direction * moveSpeed;
    }

    private void MoveFigure8()
    {
        angle += Time.deltaTime * (moveSpeed / circleRadius);

        // Figure-8 on XZ plane
        Vector3 targetPos = centerPoint + new Vector3(
            Mathf.Sin(angle) * circleRadius,
            0f,
            Mathf.Sin(angle * 2) * circleRadius * 0.5f
        );

        Vector3 direction = PhysicsHelper.DirectionXZ(transform.position, targetPos);
        rb.linearVelocity = direction * moveSpeed;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        Gizmos.color = Color.yellow;

        switch (pattern)
        {
            case MovementPattern.Circle:
                DrawCircle(centerPoint, circleRadius, 32);
                break;

            case MovementPattern.BackAndForth:
                Gizmos.DrawLine(
                    centerPoint - new Vector3(circleRadius, 0, 0),
                    centerPoint + new Vector3(circleRadius, 0, 0)
                );
                break;
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float rad = Mathf.Deg2Rad * angleStep * i;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(rad) * radius,
                0f,
                Mathf.Sin(rad) * radius
            );

            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}

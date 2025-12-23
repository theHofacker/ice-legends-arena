using UnityEngine;

/// <summary>
/// Simple AI opponent for testing defensive mechanics (poke checks, body checks).
/// Moves slowly with the puck, perfect target for defense practice.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
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
    public Vector2 centerPoint = Vector2.zero;
    public float circleRadius = 5f;

    // Component references
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Transform puckTransform;
    private Rigidbody2D puckRb;

    // State
    private bool hasPuck = false;
    private float angle = 0f;
    private float possessionCooldown = 0f; // Prevent re-possessing after poke check

    public enum MovementPattern
    {
        Circle,
        BackAndForth,
        Figure8,
        Stationary
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Set up physics
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearDamping = 2f; // Some drag for realistic movement
    }

    private void Start()
    {
        // Set opponent color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = opponentColor;
        }

        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
            puckRb = puck.GetComponent<Rigidbody2D>();
        }

        // Set starting position
        if (centerPoint == Vector2.zero)
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

        // Decrement cooldown
        if (possessionCooldown > 0f)
        {
            possessionCooldown -= Time.deltaTime;
        }

        // Check for puck possession
        CheckPuckPossession();

        // Move according to pattern
        if (!hasPuck)
        {
            // Move to puck if don't have it
            MoveTowardPuck();
        }
        else
        {
            // Move with puck
            MoveWithPattern();
        }
    }

    private void CheckPuckPossession()
    {
        float distance = Vector2.Distance(transform.position, puckTransform.position);

        // Auto-possess when close (only if cooldown expired)
        if (!hasPuck && distance <= possessionRadius && puckRb != null && possessionCooldown <= 0f)
        {
            if (puckRb.linearVelocity.magnitude < 8f) // Only possess slow-moving pucks
            {
                hasPuck = true;

                // Slow down puck
                puckRb.linearVelocity *= 0.5f;

                // Visual feedback
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.yellow; // Change color when gets puck
                }

                Debug.Log($"{gameObject.name} possessed puck");
            }
        }
        else if (hasPuck && distance > possessionRadius * 3)
        {
            // Lost possession - set cooldown to prevent immediate re-possession
            hasPuck = false;
            possessionCooldown = 1f; // 1 second cooldown

            if (spriteRenderer != null)
            {
                spriteRenderer.color = opponentColor;
            }

            Debug.Log($"{gameObject.name} lost possession (cooldown: 1s)");
        }
    }

    /// <summary>
    /// Public method to stun opponent (called by poke check)
    /// </summary>
    public void ApplyPokeCheckStun()
    {
        hasPuck = false;
        possessionCooldown = 1f; // 1 second cooldown

        if (spriteRenderer != null)
        {
            spriteRenderer.color = opponentColor;
        }

        Debug.Log($"{gameObject.name} got poke checked! Stunned for 1s");
    }

    /// <summary>
    /// Public method to stun opponent with body check (knocked down)
    /// </summary>
    public void ApplyBodyCheckStun(float stunDuration)
    {
        hasPuck = false;
        possessionCooldown = stunDuration;

        // Visual feedback - flash red when body checked
        if (spriteRenderer != null)
        {
            StartCoroutine(FlashColorCoroutine(Color.red, stunDuration));
        }

        Debug.Log($"{gameObject.name} got BODY CHECKED! Stunned for {stunDuration}s");
    }

    /// <summary>
    /// Flash opponent color when stunned
    /// </summary>
    private System.Collections.IEnumerator FlashColorCoroutine(Color flashColor, float duration)
    {
        if (spriteRenderer == null) yield break;

        float elapsed = 0f;
        float flashSpeed = 5f; // How fast to flash

        while (elapsed < duration)
        {
            // Alternate between flash color and normal color
            float t = Mathf.PingPong(elapsed * flashSpeed, 1f);
            spriteRenderer.color = Color.Lerp(opponentColor, flashColor, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Restore original color
        spriteRenderer.color = opponentColor;
    }

    private void MoveTowardPuck()
    {
        Vector2 direction = (puckTransform.position - transform.position).normalized;
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
                rb.linearVelocity = Vector2.zero;
                break;
        }

        // Make puck follow opponent
        if (puckTransform != null && hasPuck)
        {
            Vector2 puckTargetPos = (Vector2)transform.position + rb.linearVelocity.normalized * 0.8f;
            Vector2 puckDirection = (puckTargetPos - (Vector2)puckTransform.position);

            if (puckRb != null)
            {
                puckRb.linearVelocity = puckDirection * 10f;
            }
        }
    }

    private void MoveInCircle()
    {
        angle += Time.deltaTime * (moveSpeed / circleRadius);

        Vector2 targetPos = centerPoint + new Vector2(
            Mathf.Cos(angle) * circleRadius,
            Mathf.Sin(angle) * circleRadius
        );

        Vector2 direction = (targetPos - (Vector2)transform.position).normalized;
        rb.linearVelocity = direction * moveSpeed;
    }

    private void MoveBackAndForth()
    {
        float x = centerPoint.x + Mathf.Sin(Time.time * moveSpeed * 0.5f) * circleRadius;
        Vector2 targetPos = new Vector2(x, centerPoint.y);

        Vector2 direction = (targetPos - (Vector2)transform.position).normalized;
        rb.linearVelocity = direction * moveSpeed;
    }

    private void MoveFigure8()
    {
        angle += Time.deltaTime * (moveSpeed / circleRadius);

        Vector2 targetPos = centerPoint + new Vector2(
            Mathf.Sin(angle) * circleRadius,
            Mathf.Sin(angle * 2) * circleRadius * 0.5f
        );

        Vector2 direction = (targetPos - (Vector2)transform.position).normalized;
        rb.linearVelocity = direction * moveSpeed;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw possession radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, possessionRadius);

        // Draw movement path
        Gizmos.color = Color.yellow;

        switch (pattern)
        {
            case MovementPattern.Circle:
                DrawCircle(centerPoint, circleRadius, 32);
                break;

            case MovementPattern.BackAndForth:
                Gizmos.DrawLine(
                    centerPoint - new Vector2(circleRadius, 0),
                    centerPoint + new Vector2(circleRadius, 0)
                );
                break;
        }
    }

    private void DrawCircle(Vector2 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector2 prevPoint = center + new Vector2(radius, 0);

        for (int i = 1; i <= segments; i++)
        {
            float rad = Mathf.Deg2Rad * angleStep * i;
            Vector2 newPoint = center + new Vector2(
                Mathf.Cos(rad) * radius,
                Mathf.Sin(rad) * radius
            );

            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}

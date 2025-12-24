using UnityEngine;

/// <summary>
/// Simple teammate AI for testing passing mechanics.
/// Stays in position and can receive passes.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class TeammateController : MonoBehaviour
{
    [Header("Teammate Settings")]
    [Tooltip("Is this teammate AI-controlled or just a dummy?")]
    public bool isAI = false;

    [Tooltip("Home position for this teammate")]
    public Vector2 homePosition = Vector2.zero;

    [Tooltip("Distance to auto-receive puck")]
    [Range(0.5f, 3f)]
    public float receiveRadius = 1.5f;

    [Header("Visual Settings")]
    [Tooltip("Color to distinguish from player")]
    public Color teammateColor = Color.green;

    [Header("One-Timer Settings")]
    [Tooltip("Base shot power for one-timers")]
    [Range(10f, 50f)]
    public float oneTimerBasePower = 25f;

    // Component references
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Transform puckTransform;
    private Rigidbody2D puckRb;
    private Transform nearestGoal;

    // State
    private bool hasPuck = false;
    private bool isArmedForOneTimer = false;
    private float oneTimerPowerMultiplier = 1.0f;
    private float oneTimerCooldown = 0f; // Prevent catching puck after one-timer

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Set up physics for static teammate
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Start()
    {
        // Set teammate color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = teammateColor;
        }

        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
            puckRb = puck.GetComponent<Rigidbody2D>();
        }

        // Find nearest goal (for one-timers)
        // TODO: Determine which goal to shoot at based on team
        GameObject[] goals = GameObject.FindGameObjectsWithTag("Goal");
        if (goals.Length > 0)
        {
            nearestGoal = goals[0].transform;
        }

        // Set home position
        if (homePosition == Vector2.zero)
        {
            homePosition = transform.position;
        }
        else
        {
            transform.position = homePosition;
        }
    }

    private void Update()
    {
        if (puckTransform == null) return;

        // Decrement one-timer cooldown
        if (oneTimerCooldown > 0f)
        {
            oneTimerCooldown -= Time.deltaTime;
        }

        // Check for puck reception
        CheckPuckReception();

        // Simple AI: stay at home position for now
        if (isAI && !hasPuck)
        {
            // Could add movement logic here later
        }
    }

    private void CheckPuckReception()
    {
        float distance = Vector2.Distance(transform.position, puckTransform.position);

        // Receive puck when it gets close
        if (distance <= receiveRadius)
        {
            if (puckRb != null && puckRb.linearVelocity.magnitude > 0.5f)
            {
                // Check if armed for one-timer
                if (isArmedForOneTimer)
                {
                    // Execute one-timer immediately!
                    ExecuteOneTimer();
                    isArmedForOneTimer = false;
                }
                else if (oneTimerCooldown <= 0f) // Only catch if not on cooldown
                {
                    // Stop the puck (teammate catches it normally)
                    puckRb.linearVelocity *= 0.3f; // Slow it down significantly
                    hasPuck = true;

                    Debug.Log($"{gameObject.name} received the pass!");

                    // Visual feedback
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = Color.cyan; // Change color when has puck
                    }
                }
                // else: on cooldown after one-timer, don't catch the puck
            }
        }
        else if (hasPuck && distance > receiveRadius * 2)
        {
            // Lost possession
            hasPuck = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = teammateColor;
            }
        }
    }

    /// <summary>
    /// Arms this teammate for a one-timer shot
    /// </summary>
    public void ArmOneTimer(float powerMultiplier)
    {
        isArmedForOneTimer = true;
        oneTimerPowerMultiplier = powerMultiplier;

        // Visual feedback - change color to indicate armed status
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.yellow;
        }

        Debug.Log($"{gameObject.name} is ARMED for one-timer! (multiplier: {powerMultiplier}x)");
    }

    /// <summary>
    /// Execute one-timer shot toward goal
    /// </summary>
    private void ExecuteOneTimer()
    {
        Debug.Log($"ExecuteOneTimer called! puckRb: {puckRb != null}, nearestGoal: {nearestGoal != null}");

        if (puckRb == null)
        {
            Debug.LogError("ONE-TIMER FAILED: puckRb is null!");
            return;
        }

        if (nearestGoal == null)
        {
            Debug.LogError("ONE-TIMER FAILED: No goal found! Make sure you have a goal tagged with 'Goal' in the scene.");
            return;
        }

        // Calculate direction to goal
        Vector2 shotDirection = (nearestGoal.position - transform.position).normalized;

        // Calculate shot power with multiplier
        float shotPower = oneTimerBasePower * oneTimerPowerMultiplier;

        // Apply shot force to puck
        puckRb.linearVelocity = Vector2.zero; // Reset velocity
        puckRb.AddForce(shotDirection * shotPower, ForceMode2D.Impulse);

        // Set cooldown to prevent catching the puck immediately after one-timer
        oneTimerCooldown = 0.5f; // 0.5 second cooldown

        // Visual feedback - prominent console message
        Debug.Log($"═══════════════════════════════════");
        Debug.Log($"    🎯 ONE-TIMER! 🎯");
        Debug.Log($"    Power: {shotPower:F1} ({oneTimerPowerMultiplier}x multiplier)");
        Debug.Log($"    Shooter: {gameObject.name}");
        Debug.Log($"═══════════════════════════════════");

        // Flash effect
        StartCoroutine(OneTimerFlashEffect());

        // TODO: Add on-screen "ONE-TIMER!" text overlay
        // TODO: Add particle effects
        // TODO: Add screen shake for powerful one-timers
    }

    /// <summary>
    /// Flash the teammate sprite to show one-timer execution
    /// </summary>
    private System.Collections.IEnumerator OneTimerFlashEffect()
    {
        if (spriteRenderer == null) yield break;

        // Flash white 3 times
        for (int i = 0; i < 3; i++)
        {
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = Color.cyan; // Bright color
            yield return new WaitForSeconds(0.05f);
        }

        // Reset to normal color
        spriteRenderer.color = teammateColor;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw receive radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, receiveRadius);

        // Draw home position
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(homePosition, 0.5f);

        // Draw one-timer shot direction (if armed and in play mode)
        if (Application.isPlaying && isArmedForOneTimer && nearestGoal != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, nearestGoal.position);
        }
    }
}

using UnityEngine;

/// <summary>
/// Simulates hockey net physics - catches pucks and dramatically slows them down.
/// Makes the puck behave like it's hitting netting instead of bouncing off solid walls.
/// </summary>
public class NetPhysics : MonoBehaviour
{
    [Header("Net Catch Settings")]
    [Tooltip("How much to reduce velocity on entering net (0-1, where 1 = full stop)")]
    [Range(0f, 1f)]
    [SerializeField] private float velocityReduction = 0.9f;

    [Tooltip("Slowdown per second while in net")]
    [Range(0f, 50f)]
    [SerializeField] private float slowdownRate = 15f;

    [Tooltip("Maximum speed allowed in net")]
    [Range(0f, 3f)]
    [SerializeField] private float maxSpeedInNet = 0.5f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Puck"))
        {
            Rigidbody2D puckRb = other.GetComponent<Rigidbody2D>();
            if (puckRb != null)
            {
                // Immediately reduce velocity when entering net
                puckRb.linearVelocity *= (1f - velocityReduction);
                Debug.Log($"Puck caught in net! Speed reduced to {puckRb.linearVelocity.magnitude:F2}");
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Puck"))
        {
            Rigidbody2D puckRb = other.GetComponent<Rigidbody2D>();
            if (puckRb != null)
            {
                // Continuously slow down the puck
                float currentSpeed = puckRb.linearVelocity.magnitude;
                if (currentSpeed > maxSpeedInNet)
                {
                    // Reduce velocity toward max speed
                    float newSpeed = Mathf.MoveTowards(currentSpeed, maxSpeedInNet, slowdownRate * Time.fixedDeltaTime);
                    puckRb.linearVelocity = puckRb.linearVelocity.normalized * newSpeed;
                }
                else if (currentSpeed > 0.1f)
                {
                    // Slow down to stop
                    float newSpeed = Mathf.MoveTowards(currentSpeed, 0f, slowdownRate * Time.fixedDeltaTime);
                    puckRb.linearVelocity = puckRb.linearVelocity.normalized * newSpeed;
                }
                else
                {
                    // Stop completely
                    puckRb.linearVelocity = Vector2.zero;
                }

                // Also reduce angular velocity (spinning)
                puckRb.angularVelocity *= 0.95f;
            }
        }
    }
}

using UnityEngine;

/// <summary>
/// Trigger-based barrier that blocks players from entering through the goal front
/// but allows pucks to pass through freely.
/// </summary>
public class FrontBarrierTrigger : MonoBehaviour
{
    private void OnTriggerStay2D(Collider2D other)
    {
        // Only block players, not pucks
        if (!other.CompareTag("Puck"))
        {
            // Push player back if they try to enter
            Rigidbody2D playerRb = other.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                // Calculate direction away from goal
                Vector2 pushDirection = (other.transform.position - transform.position).normalized;

                // Apply a force to push the player back
                playerRb.AddForce(pushDirection * 100f, ForceMode2D.Force);
            }
        }
    }
}

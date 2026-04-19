using UnityEngine;

/// <summary>
/// Trigger-based barrier that blocks players from entering through the goal front
/// but allows pucks to pass through freely.
/// Converted to 3D physics (XZ plane, Y = height).
/// </summary>
public class FrontBarrierTrigger : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
        // Only block players, not pucks
        if (!other.CompareTag("Puck"))
        {
            // Push player back if they try to enter
            Rigidbody playerRb = other.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                // Calculate direction away from goal on XZ plane
                Vector3 pushDirection = PhysicsHelper.DirectionXZ(transform.position, other.transform.position);

                // Apply a force to push the player back
                playerRb.AddForce(pushDirection * 100f, ForceMode.Force);
            }
        }
    }
}

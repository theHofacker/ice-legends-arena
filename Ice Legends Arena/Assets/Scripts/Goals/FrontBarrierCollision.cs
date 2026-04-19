using UnityEngine;

/// <summary>
/// Attached to goal front barrier to allow pucks through while blocking players.
/// Uses layer-based collision or finds and ignores puck collisions at start.
/// Converted to 3D physics (XZ plane, Y = height).
/// </summary>
public class FrontBarrierCollision : MonoBehaviour
{
    private BoxCollider barrierCollider;

    private void Awake()
    {
        barrierCollider = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        // Find all pucks in the scene and ignore collision with them
        GameObject[] pucks = GameObject.FindGameObjectsWithTag("Puck");
        foreach (GameObject puck in pucks)
        {
            Collider puckCollider = puck.GetComponent<Collider>();
            if (puckCollider != null)
            {
                Physics.IgnoreCollision(barrierCollider, puckCollider, true);
                Debug.Log($"Front barrier ignoring collision with puck: {puck.name}");
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Double-check: if a puck somehow hits this barrier, ignore it
        if (collision.gameObject.CompareTag("Puck"))
        {
            Collider puckCollider = collision.collider;
            Physics.IgnoreCollision(barrierCollider, puckCollider, true);
            Debug.Log($"Front barrier dynamically ignoring puck collision");
        }
        // Players and other objects will collide normally
    }
}

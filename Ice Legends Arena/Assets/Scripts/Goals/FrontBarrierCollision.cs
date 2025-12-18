using UnityEngine;

/// <summary>
/// Attached to goal front barrier to allow pucks through while blocking players.
/// Uses layer-based collision or finds and ignores puck collisions at start.
/// </summary>
public class FrontBarrierCollision : MonoBehaviour
{
    private BoxCollider2D barrierCollider;

    private void Awake()
    {
        barrierCollider = GetComponent<BoxCollider2D>();
    }

    private void Start()
    {
        // Find all pucks in the scene and ignore collision with them
        GameObject[] pucks = GameObject.FindGameObjectsWithTag("Puck");
        foreach (GameObject puck in pucks)
        {
            Collider2D puckCollider = puck.GetComponent<Collider2D>();
            if (puckCollider != null)
            {
                Physics2D.IgnoreCollision(barrierCollider, puckCollider, true);
                Debug.Log($"Front barrier ignoring collision with puck: {puck.name}");
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Double-check: if a puck somehow hits this barrier, ignore it
        if (collision.gameObject.CompareTag("Puck"))
        {
            Collider2D puckCollider = collision.collider;
            Physics2D.IgnoreCollision(barrierCollider, puckCollider, true);
            Debug.Log($"Front barrier dynamically ignoring puck collision");
        }
        // Players and other objects will collide normally
    }
}

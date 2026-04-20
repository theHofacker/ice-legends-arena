using UnityEngine;

/// <summary>
/// Trigger component attached to goals. Detects when puck enters and notifies GameManager.
/// Attach this to your goal GameObjects with a trigger collider.
/// Converted to 3D physics (XZ plane, Y = height).
/// </summary>
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    [Header("Goal Settings")]
    [Tooltip("Is this the player's goal (they defend it) or opponent's goal (they attack it)?")]
    [SerializeField] private bool isPlayerGoal = false;

    [Header("Visual Feedback")]
    [Tooltip("Particle effect to spawn when goal is scored")]
    [SerializeField] private GameObject goalParticlePrefab;

    [Tooltip("Sound effect to play when goal is scored")]
    [SerializeField] private AudioClip goalSound;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    private Collider goalCollider;
    private AudioSource audioSource;

    private void Awake()
    {
        // Find a trigger collider - prefer BoxCollider since MeshColliders can't be concave triggers
        goalCollider = null;
        foreach (Collider col in GetComponents<Collider>())
        {
            if (col.isTrigger)
            {
                goalCollider = col;
                break;
            }
            // Prefer BoxCollider over MeshCollider for trigger use
            if (col is BoxCollider && goalCollider == null)
            {
                goalCollider = col;
            }
        }

        // Fallback to first collider if none found
        if (goalCollider == null)
        {
            goalCollider = GetComponent<Collider>();
        }

        // Set as trigger if not already (only if it's not a concave MeshCollider)
        if (goalCollider != null && !goalCollider.isTrigger)
        {
            if (goalCollider is MeshCollider meshCol && !meshCol.convex)
            {
                Debug.LogWarning($"{gameObject.name}: MeshCollider is concave and can't be a trigger. Add a BoxCollider with Is Trigger = true instead.");
            }
            else
            {
                goalCollider.isTrigger = true;
                Debug.LogWarning($"{gameObject.name}: Collider was not set as trigger. Fixed automatically.");
            }
        }

        // Get or add AudioSource for goal horn
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && goalSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if puck entered the goal
        if (other.CompareTag("Puck"))
        {
            OnGoalScored(other.gameObject);
        }
    }

    /// <summary>
    /// Handle goal scored logic
    /// </summary>
    private void OnGoalScored(GameObject puck)
    {
        // Only count goals during active play
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GoalTrigger: No GameManager found!");
            return;
        }

        if (GameManager.Instance.CurrentState != GameManager.MatchState.Playing)
        {
            if (showDebugMessages)
            {
                Debug.Log($"Goal not counted - match state is {GameManager.Instance.CurrentState}");
            }
            return;
        }

        // Determine who scored
        // If puck enters player's goal -> opponent scored
        // If puck enters opponent's goal -> player scored
        bool scoredByPlayer = !isPlayerGoal;

        if (showDebugMessages)
        {
            string scorer = scoredByPlayer ? "PLAYER" : "OPPONENT";
            Debug.Log($"GOAL! {scorer} scored in {gameObject.name}");
        }

        // Notify GameManager
        GameManager.Instance.GoalScored(scoredByPlayer);

        // Visual/audio feedback
        PlayGoalEffects();
    }

    /// <summary>
    /// Play visual and audio effects for goal
    /// </summary>
    private void PlayGoalEffects()
    {
        // Spawn particle effect
        if (goalParticlePrefab != null)
        {
            GameObject particles = Instantiate(goalParticlePrefab, transform.position, Quaternion.identity);
            Destroy(particles, 3f); // Clean up after 3 seconds
        }

        // Play goal horn
        if (audioSource != null && goalSound != null)
        {
            audioSource.PlayOneShot(goalSound);
        }

        // TODO: Add screen shake, celebration animation, etc.
    }

    /// <summary>
    /// Visualize goal trigger in editor
    /// </summary>
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // Draw goal area in editor
            Gizmos.color = isPlayerGoal ? new Color(1, 0, 0, 0.3f) : new Color(0, 1, 0, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider boxCol)
            {
                Gizmos.DrawCube(boxCol.center, boxCol.size);
            }
            else if (col is SphereCollider sphereCol)
            {
                Gizmos.DrawSphere(sphereCol.center, sphereCol.radius);
            }
        }
    }
}

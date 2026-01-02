using UnityEngine;

/// <summary>
/// Trigger component attached to goals. Detects when puck enters and notifies GameManager.
/// Attach this to your goal GameObjects with a trigger collider.
/// 100% transferable to 3D!
/// </summary>
[RequireComponent(typeof(Collider2D))]
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

    private Collider2D goalCollider;
    private AudioSource audioSource;

    private void Awake()
    {
        goalCollider = GetComponent<Collider2D>();

        // Ensure collider is a trigger
        if (!goalCollider.isTrigger)
        {
            goalCollider.isTrigger = true;
            Debug.LogWarning($"{gameObject.name}: Collider was not set as trigger. Fixed automatically.");
        }

        // Get or add AudioSource for goal horn
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && goalSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
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
        // If puck enters player's goal → opponent scored
        // If puck enters opponent's goal → player scored
        bool scoredByPlayer = !isPlayerGoal;

        if (showDebugMessages)
        {
            string scorer = scoredByPlayer ? "PLAYER" : "OPPONENT";
            Debug.Log($"🚨 GOAL! {scorer} scored in {gameObject.name}");
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
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            // Draw goal area in editor
            Gizmos.color = isPlayerGoal ? new Color(1, 0, 0, 0.3f) : new Color(0, 1, 0, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider2D boxCol)
            {
                Gizmos.DrawCube(boxCol.offset, boxCol.size);
            }
            else if (col is CircleCollider2D circleCol)
            {
                Gizmos.DrawSphere(circleCol.offset, circleCol.radius);
            }
        }
    }
}

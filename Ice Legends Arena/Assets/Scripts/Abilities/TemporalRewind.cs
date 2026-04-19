using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Chronomancer ability: Temporal Rewind
/// Rewinds the puck back in time to its previous position
/// Great for preventing opponent goals or resetting failed plays
/// </summary>
public class TemporalRewind : Ability
{
    [Header("Temporal Rewind Settings")]
    [Tooltip("How far back to rewind (seconds)")]
    [Range(1f, 5f)]
    [SerializeField] private float rewindTime = 2f;

    [Tooltip("How often to record puck position (seconds)")]
    [Range(0.05f, 0.2f)]
    [SerializeField] private float recordInterval = 0.1f;

    [Tooltip("Duration of the rewind animation")]
    [Range(0.2f, 1f)]
    [SerializeField] private float rewindAnimationDuration = 0.5f;

    [Header("Visual Settings")]
    [Tooltip("Color for rewind ghost trail")]
    [SerializeField] private Color rewindColor = new Color(0.6f, 0.4f, 1f, 0.7f); // Purple

    // References
    private GameObject puck;
    private Rigidbody puckRb;
    private PuckHistoryTracker historyTracker;

    protected override void Awake()
    {
        base.Awake();

        // Find puck and set up history tracking
        puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckRb = puck.GetComponent<Rigidbody>();

            // Add or get history tracker
            historyTracker = puck.GetComponent<PuckHistoryTracker>();
            if (historyTracker == null)
            {
                historyTracker = puck.AddComponent<PuckHistoryTracker>();
            }
            historyTracker.Initialize(rewindTime, recordInterval);

            Debug.Log($"TemporalRewind: Puck history tracker initialized ({rewindTime}s history)");
        }
        else
        {
            Debug.LogWarning("TemporalRewind: No puck found!");
        }
    }

    protected override void ActivateAbility()
    {
        if (puck == null || historyTracker == null)
        {
            Debug.LogWarning("TemporalRewind: Cannot rewind - no puck or history tracker!");
            return;
        }

        // Get position from rewindTime seconds ago
        PuckHistoryTracker.PuckState pastState = historyTracker.GetStateFromPast(rewindTime);

        if (pastState == null)
        {
            Debug.Log("TemporalRewind: Not enough history recorded yet!");
            return;
        }

        Debug.Log($"TEMPORAL REWIND! Moving puck from {puck.transform.position} to {pastState.position}");

        // Start rewind animation
        StartCoroutine(RewindAnimation(pastState));
    }

    /// <summary>
    /// Animate the puck rewinding through time
    /// </summary>
    private IEnumerator RewindAnimation(PuckHistoryTracker.PuckState targetState)
    {
        // Get all positions between now and target time for the trail
        List<PuckHistoryTracker.PuckState> rewindPath = historyTracker.GetRewindPath(rewindTime);

        // Freeze puck physics during rewind
        Vector3 originalVelocity = puckRb.linearVelocity;
        puckRb.linearVelocity = Vector3.zero;
        puckRb.isKinematic = true;

        // Create ghost trail showing rewind path
        List<GameObject> ghostTrail = CreateGhostTrail(rewindPath);

        // Slow motion effect
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0.3f;

        // Animate puck moving backwards through positions
        Vector3 startPos = puck.transform.position;
        Vector3 endPos = targetState.position;

        float elapsed = 0f;
        float realDuration = rewindAnimationDuration * Time.timeScale; // Adjust for slow-mo

        // Flash effect on puck
        SpriteRenderer puckSr = puck.GetComponent<SpriteRenderer>();
        Color originalPuckColor = puckSr != null ? puckSr.color : Color.black;

        while (elapsed < rewindAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / rewindAnimationDuration;

            // Ease-in-out movement
            float smoothT = t * t * (3f - 2f * t);

            // Move puck backwards
            puck.transform.position = Vector3.Lerp(startPos, endPos, smoothT);

            // Pulsing purple effect
            if (puckSr != null)
            {
                float pulse = Mathf.Sin(elapsed * 20f) * 0.5f + 0.5f;
                puckSr.color = Color.Lerp(originalPuckColor, rewindColor, pulse);
            }

            // Fade out ghost trail
            for (int i = 0; i < ghostTrail.Count; i++)
            {
                if (ghostTrail[i] != null)
                {
                    SpriteRenderer sr = ghostTrail[i].GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Color c = sr.color;
                        c.a = Mathf.Lerp(0.5f, 0f, t);
                        sr.color = c;
                    }
                }
            }

            yield return null;
        }

        // Snap to final position
        puck.transform.position = endPos;

        // Restore time and physics
        Time.timeScale = originalTimeScale;
        puckRb.isKinematic = false;
        puckRb.linearVelocity = targetState.velocity * 0.5f; // Reduced velocity after rewind

        // Restore puck color
        if (puckSr != null)
        {
            puckSr.color = originalPuckColor;
        }

        // Clean up ghost trail
        foreach (GameObject ghost in ghostTrail)
        {
            if (ghost != null) Destroy(ghost);
        }

        // Clear history after rewind (prevent double-rewind exploits)
        historyTracker.ClearHistory();

        Debug.Log("Temporal Rewind complete!");
    }

    /// <summary>
    /// Create ghost pucks showing the rewind path
    /// </summary>
    private List<GameObject> CreateGhostTrail(List<PuckHistoryTracker.PuckState> path)
    {
        List<GameObject> ghosts = new List<GameObject>();

        SpriteRenderer puckSr = puck.GetComponent<SpriteRenderer>();
        if (puckSr == null) return ghosts;

        // Create ghost at every few positions
        int step = Mathf.Max(1, path.Count / 10); // Max 10 ghosts

        for (int i = 0; i < path.Count; i += step)
        {
            GameObject ghost = new GameObject($"RewindGhost_{i}");
            ghost.transform.position = path[i].position;
            ghost.transform.localScale = puck.transform.localScale * 0.8f;

            SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
            sr.sprite = puckSr.sprite;
            sr.color = new Color(rewindColor.r, rewindColor.g, rewindColor.b, 0.3f);
            sr.sortingOrder = puckSr.sortingOrder - 1;

            ghosts.Add(ghost);
        }

        return ghosts;
    }
}

/// <summary>
/// Tracks puck position history for temporal rewind ability
/// Automatically attached to puck when TemporalRewind is used
/// </summary>
public class PuckHistoryTracker : MonoBehaviour
{
    [System.Serializable]
    public class PuckState
    {
        public Vector3 position;
        public Vector3 velocity;
        public float timestamp;

        public PuckState(Vector3 pos, Vector3 vel, float time)
        {
            position = pos;
            velocity = vel;
            timestamp = time;
        }
    }

    private Queue<PuckState> history = new Queue<PuckState>();
    private float maxHistoryTime = 2f;
    private float recordInterval = 0.1f;
    private float lastRecordTime = 0f;
    private Rigidbody rb;
    private bool isInitialized = false;

    public void Initialize(float historyDuration, float interval)
    {
        maxHistoryTime = historyDuration;
        recordInterval = interval;
        rb = GetComponent<Rigidbody>();
        isInitialized = true;
        Debug.Log($"PuckHistoryTracker: Tracking {maxHistoryTime}s of history at {1f/recordInterval} samples/sec");
    }

    private void Update()
    {
        if (!isInitialized) return;

        // Record position at intervals
        if (Time.time - lastRecordTime >= recordInterval)
        {
            RecordState();
            lastRecordTime = Time.time;

            // Remove old states
            while (history.Count > 0)
            {
                PuckState oldest = history.Peek();
                if (Time.time - oldest.timestamp > maxHistoryTime + 0.5f)
                {
                    history.Dequeue();
                }
                else
                {
                    break;
                }
            }
        }
    }

    private void RecordState()
    {
        Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;
        PuckState state = new PuckState(transform.position, velocity, Time.time);
        history.Enqueue(state);
    }

    /// <summary>
    /// Get puck state from X seconds ago
    /// </summary>
    public PuckState GetStateFromPast(float secondsAgo)
    {
        float targetTime = Time.time - secondsAgo;
        PuckState closest = null;
        float closestDiff = float.MaxValue;

        foreach (PuckState state in history)
        {
            float diff = Mathf.Abs(state.timestamp - targetTime);
            if (diff < closestDiff)
            {
                closestDiff = diff;
                closest = state;
            }
        }

        return closest;
    }

    /// <summary>
    /// Get all states from now back to X seconds ago (for animation path)
    /// </summary>
    public List<PuckState> GetRewindPath(float secondsAgo)
    {
        List<PuckState> path = new List<PuckState>();
        float targetTime = Time.time - secondsAgo;

        foreach (PuckState state in history)
        {
            if (state.timestamp >= targetTime)
            {
                path.Add(state);
            }
        }

        // Reverse so it goes from now to past
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Clear history (called after rewind to prevent exploits)
    /// </summary>
    public void ClearHistory()
    {
        history.Clear();
    }
}

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Detects when a puck/ball enters the goal and triggers scoring events.
/// Handles team-based scoring logic.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class GoalTrigger : MonoBehaviour
{
    public enum TeamSide
    {
        West,  // Left end of rink
        East   // Right end of rink
    }

    [Header("Goal Settings")]
    [Tooltip("Which end of the rink this goal is at")]
    [SerializeField] private TeamSide goalSide = TeamSide.West;

    [Tooltip("Display name for this goal")]
    [SerializeField] private string goalName = "Goal";

    [Header("Events")]
    public UnityEvent<TeamSide> onGoalScored;

    private BoxCollider2D triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if puck entered
        if (other.CompareTag("Puck"))
        {
            OnPuckEntered(other.gameObject);
        }
    }

    private void OnPuckEntered(GameObject puck)
    {
        // The team that scores is the one shooting INTO this goal
        // So if puck enters West goal, East team scored
        TeamSide scoringTeam = goalSide == TeamSide.West ? TeamSide.East : TeamSide.West;

        Debug.Log($"GOAL! {scoringTeam} team scored on {goalName}!");

        // Invoke scoring event
        onGoalScored?.Invoke(scoringTeam);

        // TODO: Add goal celebration effects, sound, etc.
        // TODO: Reset puck position after goal
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(goalName))
        {
            goalName = $"{goalSide} Goal";
        }
    }

    private void OnDrawGizmos()
    {
        // Visualize goal trigger in editor
        Gizmos.color = goalSide == TeamSide.West ? new Color(1f, 0f, 0f, 0.3f) : new Color(0f, 0f, 1f, 0.3f);

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Vector3 center = transform.position + (Vector3)box.offset;
            Vector3 size = box.size;
            Gizmos.DrawCube(center, size);
        }
    }
}

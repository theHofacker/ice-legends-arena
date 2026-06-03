using System.Collections;
using UnityEngine;

/// <summary>
/// Lightweight goal trigger for the 1v1 test scene (TestMovement.unity). The real GoalTrigger
/// depends on GameManager; the test scene has none, so this is fully self-contained: count a goal
/// when the puck enters, show a tiny on-screen score, and snap the puck back to center ice so
/// testing keeps flowing.
///
/// The trigger box is meant to span from the ice UP past the crossbar height, so a lofted roof
/// shot / top-of-net goal still registers now that the puck flies in 3D.
///
/// Add via Tools → Ice Legends → Setup Test Goals, or drop on a GameObject with a trigger collider.
/// This is a throwaway test harness — the real net (crossbar/post colliders, GameManager scoring)
/// comes when we build out the proper goal.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TestGoalTrigger : MonoBehaviour
{
    [Tooltip("Which net this is. The player's OWN net → a puck here means the opponent scored; the " +
             "opponent's net → the player scored. Drives the score label only.")]
    public bool isPlayerNet = false;

    [Tooltip("How much of the puck's speed survives when it crosses into the goal — the net " +
             "'catching' it. 0.1 = keep 10% so it deadens and drops into the net instead of " +
             "rebounding hard off the net collider. Vertical speed is zeroed regardless so it " +
             "settles fast. 1 = no deaden (old hard bounce).")]
    [Range(0f, 1f)]
    public float netDeadenFactor = 0.1f;

    [Tooltip("After a goal, snap the puck back to center ice (like a faceoff) so testing flows.")]
    public bool resetPuckOnGoal = true;

    [Tooltip("Seconds the puck stays in the net (celebration beat) before it resets to center ice.")]
    [Range(0f, 6f)]
    public float goalCelebrationDelay = 2.5f;

    [Tooltip("Where the puck respawns after a goal.")]
    public Vector3 faceoffPoint = new Vector3(0f, 0.05f, 0f);

    [Tooltip("Ignore further puck entries for this long after a goal — stops a puck sitting in the " +
             "net from double-counting.")]
    [Range(0.1f, 3f)]
    public float scoreCooldown = 1f;

    // Combined match score (static so one label reflects both nets).
    public static int PlayerGoals;
    public static int OpponentGoals;

    private float cooldownTimer;

    // Only one instance draws the shared on-screen score.
    private static TestGoalTrigger guiOwner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PlayerGoals = 0;
        OpponentGoals = 0;
        guiOwner = null;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnEnable()  { if (guiOwner == null) guiOwner = this; }
    private void OnDisable() { if (guiOwner == this) guiOwner = null; }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (cooldownTimer > 0f) return;
        if (!other.CompareTag("Puck")) return;

        // Hold off re-counting until the puck is back in play (cover the celebration beat too,
        // in case it gets nudged out and back in while it's sitting in the net).
        cooldownTimer = Mathf.Max(scoreCooldown, goalCelebrationDelay);

        if (isPlayerNet) OpponentGoals++;   // puck in YOUR net = they scored
        else             PlayerGoals++;     // puck in their net = you scored

        Debug.Log($"GOAL! {(isPlayerNet ? "OPPONENT" : "PLAYER")} scores.  PLAYER {PlayerGoals} – {OpponentGoals} OPPONENT");

        Rigidbody puckRb = other.attachedRigidbody;
        DeadenPuck(puckRb);
        if (resetPuckOnGoal) StartCoroutine(ResetPuckAfterDelay(puckRb));
    }

    /// <summary>The net 'catches' the puck: dump most of its speed (and all upward speed) so it
    /// drops into the net and settles instead of rebounding off the net collider.</summary>
    private void DeadenPuck(Rigidbody puckRb)
    {
        if (puckRb == null) return;
        Vector3 v = puckRb.linearVelocity;
        v.x *= netDeadenFactor;
        v.z *= netDeadenFactor;
        v.y = 0f;
        puckRb.linearVelocity = v;
        puckRb.angularVelocity = Vector3.zero;
    }

    private IEnumerator ResetPuckAfterDelay(Rigidbody puckRb)
    {
        if (puckRb == null) yield break;
        if (goalCelebrationDelay > 0f) yield return new WaitForSeconds(goalCelebrationDelay);
        puckRb.linearVelocity = Vector3.zero;
        puckRb.angularVelocity = Vector3.zero;
        puckRb.position = faceoffPoint;
    }

    private void OnGUI()
    {
        if (guiOwner != this) return;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            alignment = TextAnchor.UpperCenter,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(0f, 10f, Screen.width, 40f), $"PLAYER  {PlayerGoals} – {OpponentGoals}  OPPONENT", style);
    }

    private void OnDrawGizmos()
    {
        if (GetComponent<Collider>() is BoxCollider box)
        {
            Gizmos.color = isPlayerNet ? new Color(1f, 0.3f, 0.3f, 0.25f) : new Color(0.3f, 1f, 0.3f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}

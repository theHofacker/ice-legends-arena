using UnityEngine;

/// <summary>
/// Adds life to the standing-still pose by periodically triggering a one-shot
/// "idle break" (weight shift, look-around, stick adjust) and then settling back
/// to the base idle. Lives on the base Idle state of the HockeyPlayerAnimator
/// controller; HockeyAnimatorBuilder attaches it and wires the transitions.
///
/// Self-contained — drives the animator entirely through the IdleBreak int
/// parameter, so TestPlayerController needs no changes:
///   - On entering Idle, reset IdleBreak to 0 (so we don't immediately re-fire a
///     break we set on a previous visit) and roll the next dwell.
///   - After the dwell elapses, set IdleBreak to a random 1..breakCount; the
///     Idle -> IdleBreakN transition (IdleBreak == N) fires, the break clip plays
///     once, then exit-time returns to Idle and this resets the cycle.
///
/// Because the player only sits in Idle while Speed < 0.1 (the Idle -> Skating
/// transitions own the moving case), the dwell never fires mid-skate; and the
/// break states carry their own Speed-gated exits so starting to move always
/// interrupts a break cleanly.
/// </summary>
public class IdleVariationBehaviour : StateMachineBehaviour
{
    [Tooltip("Number of idle-break variants (IdleBreakN states) the builder created. " +
             "The behaviour picks a random index in 1..breakCount.")]
    public int breakCount = 3;

    [Tooltip("Shortest time (seconds) to hold the base idle before a break can fire.")]
    public float minDwell = 5f;

    [Tooltip("Longest time (seconds) to hold the base idle before a break fires.")]
    public float maxDwell = 11f;

    private static readonly int IdleBreakHash = Animator.StringToHash("IdleBreak");

    // Counts up in real state-time; when it passes the rolled dwell we fire a break.
    private float elapsed;
    private float dwell;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Clear any break index left over from a prior visit so the Idle ->
        // IdleBreakN transitions don't re-trigger the instant we land back here.
        animator.SetInteger(IdleBreakHash, 0);
        elapsed = 0f;
        dwell = RollDwell();
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (breakCount <= 0) return;

        elapsed += Time.deltaTime;
        if (elapsed < dwell) return;

        // Pick a break variant (1..breakCount) and let the transition take us there.
        int variant = Random.Range(1, breakCount + 1);
        animator.SetInteger(IdleBreakHash, variant);

        // Re-arm. We'll usually leave Idle this frame, but if the transition is
        // momentarily blocked (e.g. mid-crossfade), this avoids spamming.
        elapsed = 0f;
        dwell = RollDwell();
    }

    private float RollDwell()
    {
        return maxDwell > minDwell ? Random.Range(minDwell, maxDwell) : minDwell;
    }
}

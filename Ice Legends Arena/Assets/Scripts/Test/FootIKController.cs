using UnityEngine;

/// <summary>
/// Pins the character's feet to ice level (Y >= 0) during configured Animator
/// states. Lives on the same GameObject as the Animator (the Y Bot child), so
/// OnAnimatorIK gets called for it. Without IK, the fall clip's authored hip
/// curve dips below the ice surface for a beat during impact — feet planted at
/// Y=0 force the IK solver to keep the legs above the ice, which lifts the
/// rest of the body proportionally.
///
/// Requires the Animator Controller's layer to have IK Pass enabled AND the
/// applicable states to have Foot IK enabled. HockeyAnimatorBuilder sets both.
/// </summary>
[RequireComponent(typeof(Animator))]
public class FootIKController : MonoBehaviour
{
    [Tooltip("Animator state names where feet should be clamped to ice level. " +
             "States not listed get no IK adjustment (weight ramps to 0). " +
             "Only upright, feet-on-ice recovery poses belong here — NOT HeavyHit. " +
             "The fall clip's stomach→back ground roll needs feet and hips free to " +
             "swing through 3D space; pinning them makes the body pivot around the " +
             "planted feet instead of rolling (the 'circle on the back' bug).")]
    public string[] pinFeetStates = { "GetUp", "BoardGetUp" };

    [Tooltip("Minimum world Y for feet. Should match ice surface (0).")]
    public float iceY = 0f;

    [Tooltip("Minimum world Y for the hip (pelvis) bone. The GetUp clip authors a " +
             "pose where the hip sits below the heels at NT≈0.13; with feet pinned " +
             "at iceY, the hip ends up below ice. Lifting the body via " +
             "Animator.bodyPosition keeps the torso visible while the IK solver " +
             "stretches the legs back to the pinned feet.")]
    public float minHipY = 0.05f;

    [Tooltip("How fast IK weight ramps in/out when entering/exiting an IK state. " +
             "0 = instant, higher = smoother transition.")]
    [Range(0f, 30f)]
    public float weightLerpSpeed = 12f;

    private Animator animator;
    private float currentWeight = 0f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
        AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(layerIndex);

        // Active if current OR transitioning-into a pin state. Without checking
        // the "next" state, IK weight would snap to 0 at the start of a transition
        // out (e.g., HeavyHit → GetUp) and the feet would pop down mid-blend.
        bool active = MatchesAny(info, pinFeetStates) || MatchesAny(nextInfo, pinFeetStates);
        float targetWeight = active ? 1f : 0f;

        // Smooth ramp avoids the feet snapping to/from the ice on state edges.
        currentWeight = weightLerpSpeed > 0f
            ? Mathf.Lerp(currentWeight, targetWeight, weightLerpSpeed * Time.deltaTime)
            : targetWeight;

        ApplyFootPin(AvatarIKGoal.LeftFoot, currentWeight);
        ApplyFootPin(AvatarIKGoal.RightFoot, currentWeight);
        ApplyHipLift(currentWeight);
    }

    /// <summary>
    /// Lifts the body (hip) when the clip would author it below ice. Uses the
    /// IK pass's bodyPosition so the rest of the rig follows naturally — feet
    /// stay pinned via the foot IK, legs stretch to bridge the gap.
    ///
    /// Reads animator.bodyPosition (the hip world position the IK solver is
    /// about to use), clamps Y to minHipY if needed, scaled by the IK weight
    /// so the lift fades in/out with the same ramp as the feet.
    /// </summary>
    private void ApplyHipLift(float weight)
    {
        if (weight <= 0.001f) return;
        Vector3 bp = animator.bodyPosition;
        float floor = iceY + minHipY;
        if (bp.y < floor)
        {
            // Scale the correction by weight so we don't snap the body up on
            // state edges; matches how feet ramp in.
            float deficit = (floor - bp.y) * weight;
            bp.y += deficit;
            animator.bodyPosition = bp;
        }
    }

    private void ApplyFootPin(AvatarIKGoal foot, float weight)
    {
        animator.SetIKPositionWeight(foot, weight);
        animator.SetIKRotationWeight(foot, weight);
        if (weight <= 0.001f) return;

        // Take the foot position the clip would have produced, clamp Y, write back.
        // Leaves X/Z alone — only stops the foot from sinking below the ice.
        Vector3 pos = animator.GetIKPosition(foot);
        if (pos.y < iceY) pos.y = iceY;
        animator.SetIKPosition(foot, pos);
        animator.SetIKRotation(foot, animator.GetIKRotation(foot));
    }

    private static bool MatchesAny(AnimatorStateInfo info, string[] names)
    {
        foreach (string n in names)
        {
            if (info.IsName(n)) return true;
        }
        return false;
    }
}

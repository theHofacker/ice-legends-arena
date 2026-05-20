using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Linq;

/// <summary>
/// Editor tool that auto-generates an Animator Controller for hockey gameplay.
/// Creates states, transitions, and parameters from the ICE_HOCKEY animation pack.
/// Menu: Tools > Ice Legends > Build Hockey Animator
/// </summary>
public class HockeyAnimatorBuilder : MonoBehaviour
{
    private const string AnimationsFolder = "Assets/ICE_HOCKEY/Animations";
    private const string OutputPath = "Assets/Animation/HockeyPlayerAnimator.controller";

    // Animation clip names to search for (prefix patterns from the ICE_HOCKEY pack)
    // GA = with stick (attacker), G = without stick
    private static readonly string[] IdleClips = { "IH@GA_idle", "IH@GA_idle2" };
    // Skating without the puck — IH@G_L_03 is the matching no-puck variant for the
    // G_L_03 set we use for puck-carry skating. Fallbacks for safety.
    private static readonly string[] SkatingClips = { "IH@G_L_03", "IHA@power", "IH@GA_running" };
    // Slap-shot animation with a clear pull-back peak — set up to be paused mid-clip
    // while the player holds Space, then resumed on release. Right_GoalStraight ships
    // as Generic; FindOrPrepareHumanoidClip auto-reimports it as Humanoid and applies
    // bake settings on first build. Fallback is the previous Forward_GoalStraight clip.
    private static readonly string[] ShootClips = { "IH@Shoot_01FBH_Right_GoalStraight", "IH@Shoot_01FBH_Forward_GoalStraight" };
    // Single delivery clip shared by all three check tiers — varied via state.speed
    // (Light=1.3x snappy poke, Medium=1.0x standard, Heavy=0.85x wound-up slam).
    // The pack ships no straight-on "light" check clip, only directional Hit_01 variants.
    private static readonly string[] BodyCheckClips = { "IH@GA_MoveStraightHeavyHit_01" };
    // Three-tier receiver reactions. Of the Humanoid Hit_impact_* clips, only
    // forward_1 has explicit clipAnimations with Y bake + heightFromFeet=Feet —
    // the others (backward_1, forward_2) lack clipAnimations entries and fall
    // back to un-baked defaults, which drops the model into the ice. To avoid
    // hand-editing more .meta files we reuse forward_1 for both Light and Medium
    // and differentiate via state.speed (Light=1.5x snappy flinch, Medium=1.0x
    // standard impact). Heavy uses the full fall clip and gates on stunDuration.
    private static readonly string[] LightHitClips = { "IH@Hit_impact_forward_1" };
    private static readonly string[] MediumHitClips = { "IH@Hit_impact_forward_1" };
    private static readonly string[] HeavyHitClips = { "IH@Hit_Fall_knockback_1", "IH@Hit_impact_01" };
    private static readonly string[] GetUpClips = { "IH@Hit_getup_back" };
    private static readonly string[] CelebrationClips = { "IH@Celebration_02" };
    private static readonly string[] BlockClips = { "IH@GA_BLockStraight" };
    // Wind-up pose held while charging a shot OR check. The clip is an aim/stick-back
    // stance — reads naturally as "about to swing." First-pass uses one clip for
    // both intents; can split into ChargingShot vs ChargingCheck states later.
    private static readonly string[] ChargingClips = { "IH@Shoot_01FBH_Forward_GoalIdle" };

    // Puck-carry skating: G_L_03 FBH variants used in the SkatingWithPuck blend tree.
    // The stationary "with puck" idle reuses IH@GA_idle (already a with-stick stance).
    private static readonly string[] SkateWithPuckForwardClips = { "IH@G_L_03FBH_Forward" };
    private static readonly string[] SkateWithPuckLeftClips = { "IH@G_L_03FBH_Left" };
    private static readonly string[] SkateWithPuckRightClips = { "IH@G_L_03FBH_Right" };

    [MenuItem("Tools/Ice Legends/Build Hockey Animator")]
    public static void BuildAnimator()
    {
        // Ensure output directory exists
        if (!AssetDatabase.IsValidFolder("Assets/Animation"))
        {
            AssetDatabase.CreateFolder("Assets", "Animation");
            AssetDatabase.Refresh();
        }

        // Create the Animator Controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);

        // Add parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float); // -1 strafe-left ... +1 strafe-right
        controller.AddParameter("HasPuck", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsCharging", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("BodyCheck", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("GotHit", AnimatorControllerParameterType.Trigger);
        // Tier selector for both check delivery and hit reaction: 0=Light, 1=Medium, 2=Heavy.
        // Caller MUST set HitTier BEFORE firing BodyCheck or GotHit, or the wrong state plays.
        controller.AddParameter("HitTier", AnimatorControllerParameterType.Int);
        // Per-state speed multiplier on the Shoot state. Player sets to 0 to freeze
        // the wind-up at the peak frame while Space is held, then back to 1 on release
        // so the slap-shot completes. Defaults to 1 below — float parameters default to
        // 0 in Unity, which would freeze Shoot immediately on any trigger.
        controller.AddParameter(new AnimatorControllerParameter {
            name = "ShotSpeed",
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f
        });
        // Charging intent: 0=None, 1=Shot, 2=Check. Drives which charge-pose state to
        // play. Shots use the Shoot state (with freeze-at-peak); Checks use the Charging
        // state (held wind-up pose).
        controller.AddParameter("ChargeIntent", AnimatorControllerParameterType.Int);
        controller.AddParameter("Celebrate", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Block", AnimatorControllerParameterType.Trigger);

        // Get the base layer state machine
        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // Enable IK Pass on the base layer so OnAnimatorIK fires on the Y Bot's
        // FootIKController, which pins feet to ice level during fall/getup states.
        // Layers is a value-type array — must reassign after mutation.
        var layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;

        // Create states
        AnimatorState idleState = CreateState(rootStateMachine, "Idle", IdleClips, true);
        AnimatorState skatingState = CreateState(rootStateMachine, "Skating", SkatingClips, true);
        AnimatorState skatingWithPuckState = CreatePuckSkateBlendTree(controller, rootStateMachine);
        // Shoot state uses FindOrPrepareHumanoidClip so a Generic shot clip (like
        // Right_GoalStraight) gets auto-reimported as Humanoid with Y/XZ bake on
        // first build. State's speed is driven by the ShotSpeed parameter so the
        // player can freeze the wind-up at the peak frame.
        AnimatorState shootState = CreateShootState(rootStateMachine, "Shoot", ShootClips);
        shootState.speedParameter = "ShotSpeed";
        shootState.speedParameterActive = true;
        // Three check-delivery states, same clip, different playback speeds so the
        // visible wind-up matches the tier (snappy poke vs. wound-up slam).
        AnimatorState lightCheckState = CreateCheckState(rootStateMachine, "LightCheck", BodyCheckClips, 1.3f);
        AnimatorState mediumCheckState = CreateCheckState(rootStateMachine, "MediumCheck", BodyCheckClips, 1.0f);
        AnimatorState heavyCheckState = CreateCheckState(rootStateMachine, "HeavyCheck", BodyCheckClips, 0.85f);
        // Three receiver reactions. Light and Medium share the same clip (forward_1)
        // since it's the only Humanoid impact with proper Y bake; Light plays at
        // 1.5x to read as a snappier flinch. Heavy has its own fall clip.
        AnimatorState lightHitState = CreateCheckState(rootStateMachine, "LightHit", LightHitClips, 1.5f);
        AnimatorState mediumHitState = CreateState(rootStateMachine, "MediumHit", MediumHitClips, false);
        AnimatorState heavyHitState = CreateHeavyHitState(rootStateMachine, "HeavyHit", HeavyHitClips);
        // NO Foot IK on HeavyHit. The knockback fall ends with a stomach→back
        // ground roll; pinning the feet to ice (and lifting the hip) makes the
        // body pivot around the planted feet instead of rolling — the "circle on
        // the back" bug. Foot IK belongs only on upright recovery poses (GetUp).
        heavyHitState.iKOnFeet = false;
        AnimatorState getUpState = CreateState(rootStateMachine, "GetUp", GetUpClips, false);
        // Foot IK on GetUp too — the rising motion goes through a low-crouch
        // pose that can clip below ice without IK.
        getUpState.iKOnFeet = true;
        AnimatorState celebrationState = CreateState(rootStateMachine, "Celebration", CelebrationClips, false);
        AnimatorState blockState = CreateState(rootStateMachine, "Block", BlockClips, false);
        // Wind-up state — looping aim pose held while IsCharging is true.
        // Uses CreateChargingState which forces Loop + Y/XZ bake + feet-based on
        // the clip's import settings, since IH@Shoot_01FBH_Forward_GoalIdle ships
        // with empty clipAnimations and otherwise defaults to un-baked Y (which
        // would sink the body into the ice while charging).
        AnimatorState chargingState = CreateChargingState(rootStateMachine, "Charging", ChargingClips);

        // Set default state
        rootStateMachine.defaultState = idleState;

        // --- Transitions ---
        // Locomotion: Idle <-> Skating (no puck) and Idle <-> SkatingWithPuck (has puck),
        // gated by Speed and HasPuck. Idle is shared because IH@GA_idle is already a
        // with-stick stance and reads fine whether or not we're carrying the puck.

        // Idle -> Skating (no puck, moving)
        AddLocoTransition(idleState, skatingState, speedGreater: true, hasPuck: false);
        // Skating -> Idle (stopped)
        AnimatorStateTransition skatingToIdle = skatingState.AddTransition(idleState);
        skatingToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        skatingToIdle.hasExitTime = false;
        skatingToIdle.duration = 0.15f;

        // Idle -> SkatingWithPuck (has puck, moving)
        AddLocoTransition(idleState, skatingWithPuckState, speedGreater: true, hasPuck: true);
        // SkatingWithPuck -> Idle (stopped)
        AnimatorStateTransition swpToIdle = skatingWithPuckState.AddTransition(idleState);
        swpToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        swpToIdle.hasExitTime = false;
        swpToIdle.duration = 0.15f;

        // Cross-edges when HasPuck flips while skating
        AddPuckFlipTransition(skatingState, skatingWithPuckState, hasPuck: true);
        AddPuckFlipTransition(skatingWithPuckState, skatingState, hasPuck: false);

        // Any State -> Shoot (trigger)
        AnimatorStateTransition anyToShoot = rootStateMachine.AddAnyStateTransition(shootState);
        anyToShoot.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
        anyToShoot.hasExitTime = false;
        anyToShoot.duration = 0.1f;
        anyToShoot.canTransitionToSelf = false;

        // Shoot -> Idle (after animation finishes)
        AnimatorStateTransition shootToIdle = shootState.AddTransition(idleState);
        shootToIdle.hasExitTime = true;
        shootToIdle.exitTime = 0.9f;
        shootToIdle.duration = 0.15f;

        // Any State -> tier-specific check (BodyCheck trigger + HitTier int).
        // Caller must set HitTier BEFORE setting the trigger or the wrong state plays.
        AddAnyToTierTransition(rootStateMachine, lightCheckState, "BodyCheck", 0);
        AddAnyToTierTransition(rootStateMachine, mediumCheckState, "BodyCheck", 1);
        AddAnyToTierTransition(rootStateMachine, heavyCheckState, "BodyCheck", 2);

        // Each check state -> Idle after animation finishes.
        AddStateExitToIdle(lightCheckState, idleState, exitTime: 0.9f);
        AddStateExitToIdle(mediumCheckState, idleState, exitTime: 0.9f);
        AddStateExitToIdle(heavyCheckState, idleState, exitTime: 0.9f);

        // Any State -> tier-specific hit reaction (GotHit trigger + HitTier int).
        AddAnyToTierTransition(rootStateMachine, lightHitState, "GotHit", 0);
        AddAnyToTierTransition(rootStateMachine, mediumHitState, "GotHit", 1);
        AddAnyToTierTransition(rootStateMachine, heavyHitState, "GotHit", 2);

        // Light and Medium don't fall, so they go straight back to Idle.
        // Heavy is the full fall — it goes through GetUp before returning to Idle.
        AddStateExitToIdle(lightHitState, idleState, exitTime: 0.9f);
        AddStateExitToIdle(mediumHitState, idleState, exitTime: 0.9f);
        // Start the crossfade earlier (0.88) and stretch it (0.25s) so the tail of
        // the roll blends into the start of getup_back instead of snapping. The roll
        // end-pose and the getup start-pose don't line up exactly, so a longer blend
        // hides the seam.
        AnimatorStateTransition heavyToGetUp = heavyHitState.AddTransition(getUpState);
        heavyToGetUp.hasExitTime = true;
        heavyToGetUp.exitTime = 0.88f;
        heavyToGetUp.duration = 0.25f;

        AnimatorStateTransition getUpToIdle = getUpState.AddTransition(idleState);
        getUpToIdle.hasExitTime = true;
        getUpToIdle.exitTime = 0.9f;
        getUpToIdle.duration = 0.15f;

        // Any State -> Celebration (trigger)
        AnimatorStateTransition anyToCelebrate = rootStateMachine.AddAnyStateTransition(celebrationState);
        anyToCelebrate.AddCondition(AnimatorConditionMode.If, 0, "Celebrate");
        anyToCelebrate.hasExitTime = false;
        anyToCelebrate.duration = 0.1f;
        anyToCelebrate.canTransitionToSelf = false;

        // Celebration -> Idle
        AnimatorStateTransition celebToIdle = celebrationState.AddTransition(idleState);
        celebToIdle.hasExitTime = true;
        celebToIdle.exitTime = 0.95f;
        celebToIdle.duration = 0.15f;

        // Any State -> Block (trigger)
        AnimatorStateTransition anyToBlock = rootStateMachine.AddAnyStateTransition(blockState);
        anyToBlock.AddCondition(AnimatorConditionMode.If, 0, "Block");
        anyToBlock.hasExitTime = false;
        anyToBlock.duration = 0.1f;
        anyToBlock.canTransitionToSelf = false;

        // Block -> Idle
        AnimatorStateTransition blockToIdle = blockState.AddTransition(idleState);
        blockToIdle.hasExitTime = true;
        blockToIdle.exitTime = 0.9f;
        blockToIdle.duration = 0.15f;

        // Wind-up: Any State -> Charging when IsCharging is true AND ChargeIntent == 2
        // (Check). Shots use the Shoot state's freeze-at-peak instead of the held
        // Charging pose. Must be added AFTER the trigger-based AnyState transitions
        // so those preempt on release. canTransitionToSelf=false avoids re-entry
        // while the bool is held.
        AnimatorStateTransition anyToCharging = rootStateMachine.AddAnyStateTransition(chargingState);
        anyToCharging.AddCondition(AnimatorConditionMode.If, 0, "IsCharging");
        anyToCharging.AddCondition(AnimatorConditionMode.Equals, 2, "ChargeIntent");
        anyToCharging.hasExitTime = false;
        anyToCharging.duration = 0.1f;
        anyToCharging.canTransitionToSelf = false;

        // Charging -> Idle when IsCharging drops AND player has come to rest. The
        // Speed condition matters because TestPlayerController slows the player to
        // chargeMoveSpeedFactor while charging — on release they may still be drifting.
        AnimatorStateTransition chargingToIdle = chargingState.AddTransition(idleState);
        chargingToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCharging");
        chargingToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        chargingToIdle.hasExitTime = false;
        chargingToIdle.duration = 0.15f;

        // Charging -> Skating(WithPuck) when IsCharging drops AND player is moving.
        // Fork on HasPuck so we go back to the right locomotion state.
        AnimatorStateTransition chargingToSkating = chargingState.AddTransition(skatingState);
        chargingToSkating.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCharging");
        chargingToSkating.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        chargingToSkating.AddCondition(AnimatorConditionMode.IfNot, 0, "HasPuck");
        chargingToSkating.hasExitTime = false;
        chargingToSkating.duration = 0.15f;

        AnimatorStateTransition chargingToSkatingWithPuck = chargingState.AddTransition(skatingWithPuckState);
        chargingToSkatingWithPuck.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCharging");
        chargingToSkatingWithPuck.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        chargingToSkatingWithPuck.AddCondition(AnimatorConditionMode.If, 0, "HasPuck");
        chargingToSkatingWithPuck.hasExitTime = false;
        chargingToSkatingWithPuck.duration = 0.15f;

        // Save
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Hockey Animator Controller created at: {OutputPath}");
        Debug.Log("States: Idle, Skating, SkatingWithPuck (BlendTree), Shoot, {Light,Medium,Heavy}Check, {Light,Medium,Heavy}Hit, GetUp, Celebration, Block, Charging");
        Debug.Log("Parameters: Speed (float), MoveX (float), HasPuck (bool), IsCharging (bool), HitTier (int), Shoot/BodyCheck/GotHit/Celebrate/Block (triggers)");
        Debug.Log("Tier contract: set HitTier (0=Light, 1=Medium, 2=Heavy) BEFORE setting BodyCheck or GotHit trigger.");
        Debug.Log("\nIMPORTANT: You may need to manually assign animation clips to each state if they weren't auto-found.");
        Debug.Log("Open the Animator window (Window > Animation > Animator) to verify the setup.");

        // Select the created asset
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
    }

    /// <summary>
    /// Creates the Shoot state with auto-Humanoid-reimport for the chosen shot clip.
    /// Some pack shot clips (e.g., Right_GoalStraight) ship as Generic and would
    /// T-pose the Y Bot until reimported. FindOrPrepareHumanoidClip handles that.
    /// </summary>
    private static AnimatorState CreateShootState(AnimatorStateMachine sm, string stateName, string[] clipSearchNames)
    {
        AnimationClip clip = FindOrPrepareHumanoidClip(clipSearchNames, requireLoop: false);
        AnimatorState state = sm.AddState(stateName);
        if (clip != null)
        {
            state.motion = clip;
            Debug.Log($"  State '{stateName}': Assigned clip '{clip.name}' (Humanoid + bake verified)");
            WarnIfNotHumanoid(stateName, clip);
        }
        else
        {
            Debug.LogWarning($"  State '{stateName}': No matching clip found for [{string.Join(", ", clipSearchNames)}]. Assign manually.");
        }
        return state;
    }

    /// <summary>
    /// Finds a clip and ensures it's set up for use on the Y Bot Humanoid avatar:
    ///   1. If Generic, reimports as Humanoid with avatar copied from IH@GA_idle.
    ///   2. Applies Y/XZ Bake Into Pose + Based Upon Feet on the clip animations.
    ///   3. Optionally forces Loop Time if the clip needs to loop in its state.
    /// Saves and reimports the FBX only if something actually changed, so repeat
    /// runs of the builder are cheap.
    /// </summary>
    private static AnimationClip FindOrPrepareHumanoidClip(string[] searchNames, bool requireLoop)
    {
        AnimationClip clip = FindAnimationClip(searchNames);
        if (clip == null) return null;

        string path = AssetDatabase.GetAssetPath(clip);
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return clip;

        bool changed = false;

        // 1) Promote Generic → Humanoid using IH@GA_idle's avatar.
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            Avatar sourceAvatar = LoadAvatarFromClipNamed("IH@GA_idle");
            if (sourceAvatar != null)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = sourceAvatar;
                changed = true;
                Debug.Log($"  Reimporting '{clip.name}' as Humanoid (avatar from IH@GA_idle).");
            }
            else
            {
                Debug.LogWarning($"  Cannot find source avatar (IH@GA_idle) — '{clip.name}' stays Generic and will T-pose.");
            }
        }

        // 2/3) Apply bake settings + loop. Use defaultClipAnimations as starting
        // template when the FBX ships with empty clipAnimations.
        ModelImporterClipAnimation[] clipAnimations = importer.clipAnimations;
        if (clipAnimations.Length == 0) clipAnimations = importer.defaultClipAnimations;

        foreach (var clipAnim in clipAnimations)
        {
            if (requireLoop && !clipAnim.loopTime) { clipAnim.loopTime = true; changed = true; }
            if (!clipAnim.lockRootHeightY) { clipAnim.lockRootHeightY = true; changed = true; }
            if (!clipAnim.lockRootPositionXZ) { clipAnim.lockRootPositionXZ = true; changed = true; }
            if (!clipAnim.heightFromFeet) { clipAnim.heightFromFeet = true; changed = true; }
        }

        if (changed)
        {
            importer.clipAnimations = clipAnimations;
            importer.SaveAndReimport();
        }
        return clip;
    }

    /// <summary>
    /// Loads the Avatar sub-asset from a Humanoid FBX by clip name. Used as the
    /// "copy from" source when promoting Generic clips to Humanoid.
    /// </summary>
    private static Avatar LoadAvatarFromClipNamed(string clipName)
    {
        string[] guids = AssetDatabase.FindAssets(clipName, new[] { AnimationsFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != clipName) continue;
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (UnityEngine.Object asset in subAssets)
            {
                if (asset is Avatar avatar) return avatar;
            }
        }
        return null;
    }

    /// <summary>
    /// Creates a wind-up state that loops an aim/charge pose. Forces the clip's
    /// import settings to Loop=true, Y-bake=true, XZ-bake=true, feet-based=true
    /// the first time the builder runs — the ICE_HOCKEY pack ships several
    /// Humanoid clips with empty clipAnimations that default to un-baked Y,
    /// which would sink the model into the ice during the wind-up.
    /// </summary>
    private static AnimatorState CreateChargingState(AnimatorStateMachine sm, string stateName, string[] clipSearchNames)
    {
        AnimationClip clip = FindAnimationClipWithFullBake(clipSearchNames);
        AnimatorState state = sm.AddState(stateName);
        if (clip != null)
        {
            state.motion = clip;
            Debug.Log($"  State '{stateName}': Assigned clip '{clip.name}' (full-bake applied)");
            WarnIfNotHumanoid(stateName, clip);
        }
        else
        {
            Debug.LogWarning($"  State '{stateName}': No matching clip found for [{string.Join(", ", clipSearchNames)}]. Assign manually.");
        }
        return state;
    }

    /// <summary>
    /// Finds a clip and forces full ground-locked import settings (Loop, Y/XZ
    /// Bake Into Pose, Based Upon = Feet) so it can be used as a charge/hold
    /// pose without drifting through the ice. Works around clips that ship with
    /// empty clipAnimations and would otherwise default to un-baked Y.
    /// </summary>
    private static AnimationClip FindAnimationClipWithFullBake(string[] searchNames)
    {
        AnimationClip clip = FindAnimationClip(searchNames);
        if (clip == null) return null;

        string path = AssetDatabase.GetAssetPath(clip);
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return clip;

        ModelImporterClipAnimation[] clipAnimations = importer.clipAnimations;
        if (clipAnimations.Length == 0) clipAnimations = importer.defaultClipAnimations;

        bool changed = false;
        foreach (var clipAnim in clipAnimations)
        {
            if (!clipAnim.loopTime)             { clipAnim.loopTime = true; changed = true; }
            if (!clipAnim.lockRootHeightY)      { clipAnim.lockRootHeightY = true; changed = true; }
            if (!clipAnim.lockRootPositionXZ)   { clipAnim.lockRootPositionXZ = true; changed = true; }
            if (clipAnim.heightFromFeet == false) { clipAnim.heightFromFeet = true; changed = true; }
        }

        if (changed)
        {
            importer.clipAnimations = clipAnimations;
            importer.SaveAndReimport();
        }
        return clip;
    }

    /// <summary>
    /// Creates the HeavyHit fall state, baking ALL root motion (rotation + Y + XZ)
    /// into the body pose. The knockback fall ends with a stomach→back ground roll
    /// whose rotation, by default, is extracted to root motion — which is discarded
    /// at runtime because the controllers set applyRootMotion=false. The result is
    /// the body writhing in place instead of rolling over (the "circle on the back"
    /// bug). Baking rotation into pose keeps the roll in the bones so it plays
    /// without root motion. NOT looped — it's a one-shot fall.
    /// </summary>
    private static AnimatorState CreateHeavyHitState(AnimatorStateMachine sm, string stateName, string[] clipSearchNames)
    {
        AnimationClip clip = FindAnimationClipFullyGrounded(clipSearchNames);
        AnimatorState state = sm.AddState(stateName);
        if (clip != null)
        {
            state.motion = clip;
            Debug.Log($"  State '{stateName}': Assigned clip '{clip.name}' (root rotation + position baked into pose)");
            WarnIfNotHumanoid(stateName, clip);
        }
        else
        {
            Debug.LogWarning($"  State '{stateName}': No matching clip found for [{string.Join(", ", clipSearchNames)}]. Assign manually.");
        }
        return state;
    }

    /// <summary>
    /// Finds a clip and bakes its full root motion (rotation, Y, XZ) into the pose
    /// without forcing Loop. Use for one-shot reactions whose root motion would
    /// otherwise be discarded when the character is physics-driven (applyRootMotion
    /// off). lockRootRotation = "Root Transform Rotation → Bake Into Pose".
    /// </summary>
    private static AnimationClip FindAnimationClipFullyGrounded(string[] searchNames)
    {
        AnimationClip clip = FindAnimationClip(searchNames);
        if (clip == null) return null;

        string path = AssetDatabase.GetAssetPath(clip);
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return clip;

        ModelImporterClipAnimation[] clipAnimations = importer.clipAnimations;
        if (clipAnimations.Length == 0) clipAnimations = importer.defaultClipAnimations;

        bool changed = false;
        foreach (var clipAnim in clipAnimations)
        {
            if (!clipAnim.lockRootRotation)   { clipAnim.lockRootRotation = true; changed = true; }
            if (!clipAnim.lockRootHeightY)    { clipAnim.lockRootHeightY = true; changed = true; }
            if (!clipAnim.lockRootPositionXZ) { clipAnim.lockRootPositionXZ = true; changed = true; }
        }

        if (changed)
        {
            importer.clipAnimations = clipAnimations;
            importer.SaveAndReimport();
        }
        return clip;
    }

    /// <summary>
    /// Creates a check-delivery state using the standard BodyCheck clip with a custom
    /// state-level speed multiplier. Lets one clip cover all three tiers visually
    /// (snappy poke at 1.3x vs. wound-up slam at 0.85x) without needing a separate
    /// light-straight clip the pack doesn't ship.
    /// </summary>
    private static AnimatorState CreateCheckState(AnimatorStateMachine sm, string stateName, string[] clipSearchNames, float playbackSpeed)
    {
        AnimatorState state = CreateState(sm, stateName, clipSearchNames, false);
        state.speed = playbackSpeed;
        return state;
    }

    /// <summary>
    /// Any State → tier state, conditioned on trigger + HitTier int equals tier.
    /// Caller must set HitTier BEFORE firing the trigger (within the same frame is fine).
    /// </summary>
    private static void AddAnyToTierTransition(AnimatorStateMachine sm, AnimatorState target, string triggerName, int tier)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(target);
        t.AddCondition(AnimatorConditionMode.If, 0, triggerName);
        t.AddCondition(AnimatorConditionMode.Equals, tier, "HitTier");
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.canTransitionToSelf = false;
    }

    /// <summary>
    /// Standard "play to ~end, then crossfade back to Idle" exit used by every
    /// one-shot state (shoot, checks, light/medium hits, celebrations, block).
    /// </summary>
    private static void AddStateExitToIdle(AnimatorState from, AnimatorState idle, float exitTime)
    {
        AnimatorStateTransition t = from.AddTransition(idle);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.duration = 0.15f;
    }

    /// <summary>
    /// Builds the SkatingWithPuck 1D blend tree on MoveX:
    ///   -1 → FBH_Left, 0 → FBH_Forward, +1 → FBH_Right.
    /// </summary>
    private static AnimatorState CreatePuckSkateBlendTree(AnimatorController controller, AnimatorStateMachine sm)
    {
        AnimatorState state = sm.AddState("SkatingWithPuck");

        BlendTree tree = new BlendTree();
        tree.name = "SkatingWithPuckBlend";
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "MoveX";
        tree.useAutomaticThresholds = false;
        tree.hideFlags = HideFlags.HideInHierarchy;

        AnimationClip leftClip = FindAnimationClipLooped(SkateWithPuckLeftClips);
        AnimationClip fwdClip = FindAnimationClipLooped(SkateWithPuckForwardClips);
        AnimationClip rightClip = FindAnimationClipLooped(SkateWithPuckRightClips);

        if (leftClip != null) tree.AddChild(leftClip, -1f);
        if (fwdClip != null) tree.AddChild(fwdClip, 0f);
        if (rightClip != null) tree.AddChild(rightClip, 1f);

        AssetDatabase.AddObjectToAsset(tree, controller);
        state.motion = tree;

        Debug.Log($"  State 'SkatingWithPuck': BlendTree on MoveX [Left={leftClip?.name}, Forward={fwdClip?.name}, Right={rightClip?.name}]");
        return state;
    }

    /// <summary>
    /// Adds a Speed-gated locomotion transition that also requires HasPuck to match.
    /// speedGreater=true → Speed > 0.1, false → Speed < 0.1.
    /// </summary>
    private static void AddLocoTransition(AnimatorState from, AnimatorState to, bool speedGreater, bool hasPuck)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(speedGreater ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0.1f, "Speed");
        t.AddCondition(hasPuck ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "HasPuck");
        t.hasExitTime = false;
        t.duration = 0.15f;
    }

    /// <summary>
    /// Adds a transition that fires the moment HasPuck flips, ignoring Speed
    /// (so a stationary player picking up the puck instantly switches to IdleWithPuck).
    /// </summary>
    private static void AddPuckFlipTransition(AnimatorState from, AnimatorState to, bool hasPuck)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(hasPuck ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "HasPuck");
        t.hasExitTime = false;
        t.duration = 0.15f;
    }

    /// <summary>
    /// Creates an animator state and tries to find a matching animation clip from the ICE_HOCKEY pack.
    /// </summary>
    private static AnimatorState CreateState(AnimatorStateMachine stateMachine, string stateName, string[] clipSearchNames, bool loop)
    {
        AnimatorState state = stateMachine.AddState(stateName);

        AnimationClip clip = loop ? FindAnimationClipLooped(clipSearchNames) : FindAnimationClip(clipSearchNames);

        if (clip != null)
        {
            state.motion = clip;
            Debug.Log($"  State '{stateName}': Assigned clip '{clip.name}'");
            WarnIfNotHumanoid(stateName, clip);
        }
        else
        {
            Debug.LogWarning($"  State '{stateName}': No matching clip found for [{string.Join(", ", clipSearchNames)}]. Assign manually.");
        }

        return state;
    }

    /// <summary>
    /// Hard warning if a clip used by the controller is Generic rather than Humanoid.
    /// Generic clips silently retarget to T-pose on the Humanoid Y Bot avatar, so
    /// catching this at build time is much better than catching it as a visible
    /// gameplay bug. Fix by reimporting the FBX with Rig → Animation Type = Humanoid,
    /// Avatar Definition = Copy From Other Avatar (source: any working Humanoid clip).
    /// </summary>
    private static void WarnIfNotHumanoid(string stateName, AnimationClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
        {
            Debug.LogError($"  State '{stateName}': Clip '{clip.name}' is {importer.animationType} (not Humanoid). Will T-POSE on Y Bot. Reimport the FBX as Humanoid.");
        }
    }

    /// <summary>
    /// Finds a clip and forces Loop Time on its import settings before returning.
    /// </summary>
    private static AnimationClip FindAnimationClipLooped(string[] searchNames)
    {
        AnimationClip clip = FindAnimationClip(searchNames);
        if (clip == null) return null;

        string clipPath = AssetDatabase.GetAssetPath(clip);
        ModelImporter importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;
        if (importer != null)
        {
            ModelImporterClipAnimation[] clipAnimations = importer.clipAnimations;
            if (clipAnimations.Length == 0) clipAnimations = importer.defaultClipAnimations;

            bool changed = false;
            foreach (var clipAnim in clipAnimations)
            {
                if (!clipAnim.loopTime) { clipAnim.loopTime = true; changed = true; }
            }

            if (changed)
            {
                importer.clipAnimations = clipAnimations;
                importer.SaveAndReimport();
            }
        }
        return clip;
    }

    /// <summary>
    /// Searches the ICE_HOCKEY animations folder for a clip matching one of the given names.
    /// Prefers exact filename match (so "IH@G_L_03" doesn't accidentally grab
    /// "IH@G_L_03FBH_Forward"), but falls back to the first prefix match if no
    /// exact filename exists (so "IH@Shoot_01FBH_Forward" still resolves to
    /// "IH@Shoot_01FBH_Forward_GoalStraight" etc.).
    /// </summary>
    private static AnimationClip FindAnimationClip(string[] searchNames)
    {
        AnimationClip firstFallback = null;

        foreach (string searchName in searchNames)
        {
            string[] guids = AssetDatabase.FindAssets(searchName, new[] { AnimationsFolder });

            // Pass 1: try exact filename match.
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName != searchName) continue;

                AnimationClip clip = LoadFirstClip(path);
                if (clip != null) return clip;
            }

            // Pass 2: remember the first prefix match as a fallback for this searchName.
            if (firstFallback == null)
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AnimationClip clip = LoadFirstClip(path);
                    if (clip != null) { firstFallback = clip; break; }
                }
            }
        }

        return firstFallback;
    }

    private static AnimationClip LoadFirstClip(string path)
    {
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object subAsset in subAssets)
        {
            if (subAsset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }
}

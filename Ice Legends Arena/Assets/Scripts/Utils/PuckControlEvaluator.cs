using UnityEngine;

/// <summary>
/// Utility for evaluating puck control and determining Force vs Contain defense.
/// Based on Weiss Tech hockey strategy:
/// - FORCE: Attack aggressively if opponent doesn't have clean control OR has back turned
/// - CONTAIN: Play passive if opponent has clean control and is facing you
/// </summary>
public static class PuckControlEvaluator
{
    /// <summary>
    /// Defensive decision type
    /// </summary>
    public enum DefensiveAction
    {
        Force,   // Attack aggressively - opponent vulnerable
        Contain  // Play passive - opponent has control
    }

    /// <summary>
    /// Evaluate whether to Force or Contain against a puck carrier
    /// </summary>
    /// <param name="puckCarrierPosition">Position of opponent with puck</param>
    /// <param name="puckCarrierVelocity">Velocity of opponent (used to determine facing direction)</param>
    /// <param name="puckPosition">Position of the puck</param>
    /// <param name="puckVelocity">Velocity of the puck</param>
    /// <param name="defenderPosition">Position of the defender (you)</param>
    /// <returns>Force (attack) or Contain (be passive)</returns>
    public static DefensiveAction EvaluateDefense(
        Vector2 puckCarrierPosition,
        Vector2 puckCarrierVelocity,
        Vector2 puckPosition,
        Vector2 puckVelocity,
        Vector2 defenderPosition)
    {
        // Check 1: Does opponent have clean control of puck?
        bool hasCleanControl = HasCleanPuckControl(puckCarrierPosition, puckPosition, puckVelocity);

        if (!hasCleanControl)
        {
            // Puck is loose or bouncing - FORCE!
            return DefensiveAction.Force;
        }

        // Check 2: Does opponent have their back to us?
        bool hasBackTurned = HasBackTurned(puckCarrierPosition, puckCarrierVelocity, defenderPosition);

        if (hasBackTurned)
        {
            // Opponent can't see us - FORCE!
            return DefensiveAction.Force;
        }

        // Check 3: Is opponent stationary (easier to force turnover)
        bool isStationary = puckCarrierVelocity.magnitude < 1f;

        if (isStationary)
        {
            // Stationary opponent is vulnerable - FORCE!
            return DefensiveAction.Force;
        }

        // Default: Opponent has clean control and is moving toward us - CONTAIN
        return DefensiveAction.Contain;
    }

    /// <summary>
    /// Check if puck carrier has clean control of the puck
    /// </summary>
    private static bool HasCleanPuckControl(Vector2 puckCarrierPosition, Vector2 puckPosition, Vector2 puckVelocity)
    {
        // Distance check: Is puck close to carrier?
        float distanceToPuck = Vector2.Distance(puckCarrierPosition, puckPosition);
        bool isClose = distanceToPuck <= 1.5f; // Within stick reach

        // Velocity check: Is puck moving slowly (controlled) or fast (loose)?
        float puckSpeed = puckVelocity.magnitude;
        bool isControlled = puckSpeed < 8f; // Slow enough to be controlled

        // Clean control = close AND controlled
        return isClose && isControlled;
    }

    /// <summary>
    /// Check if puck carrier has their back turned to defender
    /// Uses velocity as proxy for facing direction (facing where they're skating)
    /// </summary>
    private static bool HasBackTurned(Vector2 puckCarrierPosition, Vector2 puckCarrierVelocity, Vector2 defenderPosition)
    {
        // If opponent is stationary, they're not showing their back
        if (puckCarrierVelocity.magnitude < 0.5f)
        {
            return false;
        }

        // Get opponent's facing direction (where they're skating)
        Vector2 opponentFacing = puckCarrierVelocity.normalized;

        // Get direction from opponent to defender
        Vector2 toDefender = (defenderPosition - puckCarrierPosition).normalized;

        // Dot product:
        // > 0 means opponent facing toward defender (can see them)
        // < 0 means opponent facing away from defender (back turned)
        float dot = Vector2.Dot(opponentFacing, toDefender);

        // Back is turned if dot product < -0.3 (facing away at angle > ~108°)
        return dot < -0.3f;
    }

    /// <summary>
    /// Get aggression level (0.0 - 1.0) based on Force vs Contain
    /// Force = 1.0 (full aggression)
    /// Contain = 0.3 (passive, maintain position)
    /// </summary>
    public static float GetAggressionLevel(DefensiveAction action)
    {
        return action == DefensiveAction.Force ? 1.0f : 0.3f;
    }

    /// <summary>
    /// Get description of defensive action for debugging
    /// </summary>
    public static string GetActionDescription(DefensiveAction action)
    {
        return action == DefensiveAction.Force
            ? "FORCE (attack aggressively)"
            : "CONTAIN (play passive)";
    }
}

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject defining a team formation.
/// Contains positions for each player role in offensive and defensive zones.
///
/// NOTE: Vector2 fields are KEPT as Vector2 to avoid re-serializing ScriptableObject assets.
/// These represent XZ offsets (x = rink length, y = rink width).
/// Convert to 3D at usage time with PhysicsHelper.ToWorldPosition(offset) -> Vector3(x, 0, y).
/// </summary>
[CreateAssetMenu(fileName = "New Formation", menuName = "Ice Legends/Formation Data")]
public class FormationData : ScriptableObject
{
    [Header("Formation Info")]
    [Tooltip("Display name (e.g., '1-2-2', '2-1-2')")]
    public string formationName;

    [Tooltip("Description of the formation's strengths")]
    [TextArea(2, 3)]
    public string description;

    [Tooltip("Icon representing the formation")]
    public Sprite icon;

    [Header("Formation Type")]
    [Tooltip("Primary use case for this formation")]
    public FormationType formationType = FormationType.Balanced;

    [Header("Position Offsets (Relative to Puck/Zone Center)")]
    [Tooltip("Center position offset (x = rink length, y = rink width; convert with PhysicsHelper.ToWorldPosition)")]
    public Vector2 centerOffset = new Vector2(0, 0);

    [Tooltip("Left Wing position offset")]
    public Vector2 leftWingOffset = new Vector2(-3, 1);

    [Tooltip("Right Wing position offset")]
    public Vector2 rightWingOffset = new Vector2(3, 1);

    [Tooltip("Left Defense position offset")]
    public Vector2 leftDefenseOffset = new Vector2(-2, -3);

    [Tooltip("Right Defense position offset")]
    public Vector2 rightDefenseOffset = new Vector2(2, -3);

    [Header("Defensive Zone Positions (When Opponent Has Puck)")]
    [Tooltip("Use different positions in defensive zone")]
    public bool useDefensivePositions = true;

    [Tooltip("Center defensive position")]
    public Vector2 centerDefensiveOffset = new Vector2(0, -2);

    [Tooltip("Left Wing defensive position")]
    public Vector2 leftWingDefensiveOffset = new Vector2(-2, -1);

    [Tooltip("Right Wing defensive position")]
    public Vector2 rightWingDefensiveOffset = new Vector2(2, -1);

    [Tooltip("Left Defense defensive position")]
    public Vector2 leftDefenseDefensiveOffset = new Vector2(-3, -4);

    [Tooltip("Right Defense defensive position")]
    public Vector2 rightDefenseDefensiveOffset = new Vector2(3, -4);

    [Header("Neutral Zone Positions (Transition)")]
    [Tooltip("Use different positions in neutral zone")]
    public bool useNeutralZonePositions = true;

    [Tooltip("Center neutral position")]
    public Vector2 centerNeutralOffset = new Vector2(0, 0);

    [Tooltip("Left Wing neutral position")]
    public Vector2 leftWingNeutralOffset = new Vector2(-4, 0);

    [Tooltip("Right Wing neutral position")]
    public Vector2 rightWingNeutralOffset = new Vector2(4, 0);

    [Tooltip("Left Defense neutral position")]
    public Vector2 leftDefenseNeutralOffset = new Vector2(-2, -3);

    [Tooltip("Right Defense neutral position")]
    public Vector2 rightDefenseNeutralOffset = new Vector2(2, -3);

    [Header("Formation Modifiers")]
    [Tooltip("How tightly players stick to positions (0-1)")]
    [Range(0f, 1f)]
    public float positionStrictness = 0.7f;

    [Tooltip("How aggressively forwards pressure")]
    [Range(0f, 1f)]
    public float forecheck = 0.5f;

    [Tooltip("How defensively the team plays")]
    [Range(0f, 1f)]
    public float defensiveDepth = 0.5f;

    /// <summary>
    /// Get position offset for a player role in offensive zone.
    /// Returns Vector2 (x = rink length, y = rink width). Convert with PhysicsHelper.ToWorldPosition().
    /// </summary>
    public Vector2 GetOffensivePosition(PlayerPosition position)
    {
        return position switch
        {
            PlayerPosition.Center => centerOffset,
            PlayerPosition.LeftWing => leftWingOffset,
            PlayerPosition.RightWing => rightWingOffset,
            PlayerPosition.LeftDefense => leftDefenseOffset,
            PlayerPosition.RightDefense => rightDefenseOffset,
            _ => Vector2.zero
        };
    }

    /// <summary>
    /// Get position offset for a player role in defensive zone.
    /// Returns Vector2 (x = rink length, y = rink width). Convert with PhysicsHelper.ToWorldPosition().
    /// </summary>
    public Vector2 GetDefensivePosition(PlayerPosition position)
    {
        if (!useDefensivePositions) return GetOffensivePosition(position);

        return position switch
        {
            PlayerPosition.Center => centerDefensiveOffset,
            PlayerPosition.LeftWing => leftWingDefensiveOffset,
            PlayerPosition.RightWing => rightWingDefensiveOffset,
            PlayerPosition.LeftDefense => leftDefenseDefensiveOffset,
            PlayerPosition.RightDefense => rightDefenseDefensiveOffset,
            _ => Vector2.zero
        };
    }

    /// <summary>
    /// Get position offset for a player role in neutral zone.
    /// Returns Vector2 (x = rink length, y = rink width). Convert with PhysicsHelper.ToWorldPosition().
    /// </summary>
    public Vector2 GetNeutralZonePosition(PlayerPosition position)
    {
        if (!useNeutralZonePositions) return GetOffensivePosition(position);

        return position switch
        {
            PlayerPosition.Center => centerNeutralOffset,
            PlayerPosition.LeftWing => leftWingNeutralOffset,
            PlayerPosition.RightWing => rightWingNeutralOffset,
            PlayerPosition.LeftDefense => leftDefenseNeutralOffset,
            PlayerPosition.RightDefense => rightDefenseNeutralOffset,
            _ => Vector2.zero
        };
    }

    /// <summary>
    /// Get position for current game zone.
    /// Returns Vector2 (x = rink length, y = rink width). Convert with PhysicsHelper.ToWorldPosition().
    /// </summary>
    public Vector2 GetPositionForZone(PlayerPosition position, ZoneType zone)
    {
        return zone switch
        {
            ZoneType.Offensive => GetOffensivePosition(position),
            ZoneType.Defensive => GetDefensivePosition(position),
            ZoneType.Neutral => GetNeutralZonePosition(position),
            _ => GetOffensivePosition(position)
        };
    }
}

/// <summary>
/// Player positions on the team
/// </summary>
public enum PlayerPosition
{
    Center,
    LeftWing,
    RightWing,
    LeftDefense,
    RightDefense,
    Goalie,
    Bench // Not in starting lineup (used by SaveManager/Roster)
}

/// <summary>
/// Formation type/strategy
/// </summary>
public enum FormationType
{
    Balanced,       // Even offensive/defensive
    Offensive,      // High pressure, aggressive
    Defensive,      // Conservative, protect lead
    Counterattack,  // Quick transitions
    Trap            // Clog neutral zone
}

/// <summary>
/// Ice zones for positioning
/// </summary>
public enum ZoneType
{
    Offensive,
    Neutral,
    Defensive
}

/// <summary>
/// Predefined formation templates.
/// NOTE: Vector2 values represent XZ offsets (x = rink length, y = rink width).
/// Convert to 3D at usage time with PhysicsHelper.ToWorldPosition().
/// </summary>
public static class FormationTemplates
{
    /// <summary>
    /// Classic 1-2-2 formation (Balanced)
    /// </summary>
    public static FormationData Create_1_2_2()
    {
        FormationData formation = ScriptableObject.CreateInstance<FormationData>();
        formation.formationName = "1-2-2";
        formation.description = "Balanced formation with good offensive and defensive coverage.";
        formation.formationType = FormationType.Balanced;

        // Offensive positions
        formation.centerOffset = new Vector2(0, 2);
        formation.leftWingOffset = new Vector2(-3, 1);
        formation.rightWingOffset = new Vector2(3, 1);
        formation.leftDefenseOffset = new Vector2(-2, -2);
        formation.rightDefenseOffset = new Vector2(2, -2);

        // Defensive positions
        formation.centerDefensiveOffset = new Vector2(0, 0);
        formation.leftWingDefensiveOffset = new Vector2(-2, 1);
        formation.rightWingDefensiveOffset = new Vector2(2, 1);
        formation.leftDefenseDefensiveOffset = new Vector2(-3, -2);
        formation.rightDefenseDefensiveOffset = new Vector2(3, -2);

        formation.forecheck = 0.5f;
        formation.defensiveDepth = 0.5f;

        return formation;
    }

    /// <summary>
    /// Aggressive 2-1-2 formation (Offensive)
    /// </summary>
    public static FormationData Create_2_1_2()
    {
        FormationData formation = ScriptableObject.CreateInstance<FormationData>();
        formation.formationName = "2-1-2";
        formation.description = "Aggressive formation with two forwards pressuring high.";
        formation.formationType = FormationType.Offensive;

        // Offensive positions (two high, one mid)
        formation.centerOffset = new Vector2(0, 0);
        formation.leftWingOffset = new Vector2(-2, 3);
        formation.rightWingOffset = new Vector2(2, 3);
        formation.leftDefenseOffset = new Vector2(-3, -2);
        formation.rightDefenseOffset = new Vector2(3, -2);

        // Defensive positions
        formation.centerDefensiveOffset = new Vector2(0, 1);
        formation.leftWingDefensiveOffset = new Vector2(-1, 2);
        formation.rightWingDefensiveOffset = new Vector2(1, 2);
        formation.leftDefenseDefensiveOffset = new Vector2(-3, -1);
        formation.rightDefenseDefensiveOffset = new Vector2(3, -1);

        formation.forecheck = 0.8f;
        formation.defensiveDepth = 0.3f;

        return formation;
    }

    /// <summary>
    /// Defensive 1-3-1 formation
    /// </summary>
    public static FormationData Create_1_3_1()
    {
        FormationData formation = ScriptableObject.CreateInstance<FormationData>();
        formation.formationName = "1-3-1";
        formation.description = "Defensive formation that clogs the middle of the ice.";
        formation.formationType = FormationType.Defensive;

        // Offensive positions
        formation.centerOffset = new Vector2(0, 3);
        formation.leftWingOffset = new Vector2(-3, 0);
        formation.rightWingOffset = new Vector2(3, 0);
        formation.leftDefenseOffset = new Vector2(0, 0);
        formation.rightDefenseOffset = new Vector2(0, -3);

        // Defensive positions (tight box)
        formation.centerDefensiveOffset = new Vector2(0, 1);
        formation.leftWingDefensiveOffset = new Vector2(-2, 0);
        formation.rightWingDefensiveOffset = new Vector2(2, 0);
        formation.leftDefenseDefensiveOffset = new Vector2(-1, -2);
        formation.rightDefenseDefensiveOffset = new Vector2(1, -2);

        formation.forecheck = 0.2f;
        formation.defensiveDepth = 0.8f;

        return formation;
    }

    /// <summary>
    /// Neutral zone trap formation
    /// </summary>
    public static FormationData Create_Trap()
    {
        FormationData formation = ScriptableObject.CreateInstance<FormationData>();
        formation.formationName = "Trap";
        formation.description = "Clogs the neutral zone to force turnovers.";
        formation.formationType = FormationType.Trap;

        // Offensive positions
        formation.centerOffset = new Vector2(0, 1);
        formation.leftWingOffset = new Vector2(-2, 1);
        formation.rightWingOffset = new Vector2(2, 1);
        formation.leftDefenseOffset = new Vector2(-2, -2);
        formation.rightDefenseOffset = new Vector2(2, -2);

        // Neutral zone (spread across)
        formation.centerNeutralOffset = new Vector2(0, 0);
        formation.leftWingNeutralOffset = new Vector2(-4, 0);
        formation.rightWingNeutralOffset = new Vector2(4, 0);
        formation.leftDefenseNeutralOffset = new Vector2(-2, -3);
        formation.rightDefenseNeutralOffset = new Vector2(2, -3);

        // Defensive (collapse)
        formation.centerDefensiveOffset = new Vector2(0, -1);
        formation.leftWingDefensiveOffset = new Vector2(-1, 0);
        formation.rightWingDefensiveOffset = new Vector2(1, 0);
        formation.leftDefenseDefensiveOffset = new Vector2(-2, -3);
        formation.rightDefenseDefensiveOffset = new Vector2(2, -3);

        formation.forecheck = 0.3f;
        formation.defensiveDepth = 0.7f;

        return formation;
    }
}

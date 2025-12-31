using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages team formations for hockey gameplay.
/// Coordinates positioning for all teammates based on game state (offense/defense/neutral).
/// Inspired by professional sports games like NHL, FIFA, etc.
///
/// TEAM-AWARE: Supports multiple teams (Player and Opponent) with separate formations.
/// Use GetFormationManager(team) to get the appropriate formation manager.
/// </summary>
public class FormationManager : MonoBehaviour
{
    // Team enum
    public enum Team
    {
        Player,   // Player's team (teammates)
        Opponent  // Opponent's team (AI opponents)
    }

    // Multi-team support: store multiple formation managers by team
    private static Dictionary<Team, FormationManager> formationManagers = new Dictionary<Team, FormationManager>();

    // Legacy singleton support (defaults to Player team for backward compatibility)
    public static FormationManager Instance => GetFormationManager(Team.Player);

    [Header("Team Settings")]
    [Tooltip("Which team does this FormationManager control?")]
    [SerializeField] private Team team = Team.Player;

    [Header("Formation State")]
    [Tooltip("Current formation being used")]
    [SerializeField] private FormationType currentFormation = FormationType.Neutral;

    [Header("Formation Offsets")]
    [Tooltip("Offensive formation: positions relative to puck carrier")]
    [SerializeField] private FormationOffsets offensiveFormation;

    [Tooltip("Defensive formation: positions relative to own goal")]
    [SerializeField] private FormationOffsets defensiveFormation;

    [Tooltip("Neutral formation: standard ice positions")]
    [SerializeField] private FormationOffsets neutralFormation;

    [Header("Formation Settings")]
    [Tooltip("Defensive system to use (Box = passive, Sagging Zone = balanced, Arrow = aggressive)")]
    [SerializeField] private DefensiveStyle defensiveStyle = DefensiveStyle.SaggingZone;

    [Tooltip("How smoothly positions update (higher = slower)")]
    [Range(0.1f, 5f)]
    [SerializeField] private float positionSmoothTime = 1f;

    [Tooltip("Distance threshold to switch from defense to offense")]
    [Range(5f, 20f)]
    [SerializeField] private float formationSwitchDistance = 10f;

    [Header("Zone Constraints")]
    [Tooltip("Enable zone/lane constraints for realistic positioning")]
    [SerializeField] private bool useZoneConstraints = true;

    [Tooltip("Max distance defensemen can push forward (from own goal)")]
    [Range(10f, 40f)]
    [SerializeField] private float defenseMaxPushDistance = 25f;

    [Tooltip("Max distance wings can drift from their lane (Y axis - up/down on screen)")]
    [Range(3f, 12f)]
    [SerializeField] private float wingLaneWidth = 8f;

    [Tooltip("Max distance center can drift from center ice (Y axis - up/down on screen)")]
    [Range(2f, 8f)]
    [SerializeField] private float centerLaneWidth = 5f;

    // References
    private Transform puckTransform;
    private Transform playerGoal;  // Opponent's goal (teammates attack this)
    private Transform ownGoal;     // Our goal (teammates defend this)

    // Formation states
    public enum FormationType
    {
        Offensive,  // Our team has puck - attack formation
        Defensive,  // Opponent has puck - defensive formation
        Neutral     // Loose puck - neutral formation
    }

    // Defensive zone system types (from Weiss Tech Playbook)
    public enum DefensiveStyle
    {
        BoxPlusOne,        // PASSIVE: 4-player box, clogs slot (Simple Box from playbook)
        SaggingZone,       // BALANCED: Weak-side forward sags to slot (Wedge Plus One)
        SaggingZoneArrow   // AGGRESSIVE: High pressure, forward attacks at blueline
    }

    // Player positions/roles
    public enum PlayerRole
    {
        Center,
        LeftWing,
        RightWing,
        LeftDefense,
        RightDefense
    }

    /// <summary>
    /// Formation offsets for each player position
    /// Defined for attack toward RIGHT (positive X direction)
    /// X = rink length (depth/forward-back), Y = rink width (wing spread up/down)
    /// </summary>
    [System.Serializable]
    public class FormationOffsets
    {
        [Header("Forward Positions (relative to reference point)")]
        [Tooltip("Center position offset (X = depth, Y = lateral)")]
        public Vector2 centerOffset = new Vector2(-5, 0);      // Behind in X, center in Y

        [Tooltip("Left Wing position offset (X = depth, Y = lateral)")]
        public Vector2 leftWingOffset = new Vector2(0, 10);    // Even depth in X, up in Y

        [Tooltip("Right Wing position offset (X = depth, Y = lateral)")]
        public Vector2 rightWingOffset = new Vector2(0, -10);  // Even depth in X, down in Y

        [Header("Defense Positions (relative to reference point)")]
        [Tooltip("Left Defense position offset (X = depth, Y = lateral)")]
        public Vector2 leftDefenseOffset = new Vector2(-12, 6);  // Far behind in X, up in Y

        [Tooltip("Right Defense position offset (X = depth, Y = lateral)")]
        public Vector2 rightDefenseOffset = new Vector2(-12, -6);  // Far behind in X, down in Y
    }

    private void Awake()
    {
        // Register this formation manager for its team
        if (formationManagers.ContainsKey(team))
        {
            Debug.LogWarning($"FormationManager for team {team} already exists! Replacing...");
            formationManagers[team] = this;
        }
        else
        {
            formationManagers.Add(team, this);
            Debug.Log($"FormationManager registered for team {team}");
        }
    }

    /// <summary>
    /// Get the FormationManager for a specific team
    /// </summary>
    public static FormationManager GetFormationManager(Team requestedTeam)
    {
        if (formationManagers.TryGetValue(requestedTeam, out FormationManager manager))
        {
            return manager;
        }

        Debug.LogError($"No FormationManager found for team {requestedTeam}!");
        return null;
    }

    private void Start()
    {
        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
        }

        // Find goals and assign based on team
        GameObject[] goals = GameObject.FindGameObjectsWithTag("Goal");
        if (goals.Length >= 2)
        {
            // Sort goals by X position (left to right)
            Transform leftGoal = goals[0].transform.position.x < goals[1].transform.position.x ? goals[0].transform : goals[1].transform;
            Transform rightGoal = goals[0].transform.position.x > goals[1].transform.position.x ? goals[0].transform : goals[1].transform;

            // Assign goals based on team
            if (team == Team.Player)
            {
                // Player team: Attack LEFT goal (west), Defend RIGHT goal (east)
                playerGoal = leftGoal;  // Attack left
                ownGoal = rightGoal;    // Defend right
                Debug.Log($"[{team}] FormationManager: Attack LEFT goal (X:{playerGoal.position.x}), Defend RIGHT goal (X:{ownGoal.position.x})");
            }
            else // Team.Opponent
            {
                // Opponent team: Attack RIGHT goal (east), Defend LEFT goal (west)
                playerGoal = rightGoal; // Attack right
                ownGoal = leftGoal;     // Defend left
                Debug.Log($"[{team}] FormationManager: Attack RIGHT goal (X:{playerGoal.position.x}), Defend LEFT goal (X:{ownGoal.position.x})");
            }
        }

        // Initialize formation offsets with default values if not set
        if (offensiveFormation == null)
        {
            offensiveFormation = new FormationOffsets
            {
                // Offensive: Formations defined for attack toward RIGHT (positive X)
                // X = rink length (toward goals) = depth/forward-back
                // Y = rink width (perpendicular to goals) = wing spread up/down on screen
                centerOffset = new Vector2(-5, 0),        // Behind in X (trails puck carrier)
                leftWingOffset = new Vector2(0, 10),      // Up in Y (spread up on screen), even depth
                rightWingOffset = new Vector2(0, -10),    // Down in Y (spread down on screen), even depth
                leftDefenseOffset = new Vector2(-12, 6),  // Far behind in X, up in Y
                rightDefenseOffset = new Vector2(-12, -6) // Far behind in X, down in Y
            };
        }
        if (defensiveFormation == null)
        {
            defensiveFormation = new FormationOffsets
            {
                // Defensive: Collapse, protect goal (relative to own goal)
                // X = rink length (toward goals), Y = rink width (up/down spread)
                centerOffset = new Vector2(6, 0),         // Out from goal to pressure puck
                leftWingOffset = new Vector2(8, 8),       // Out + up, collapse to defensive zone
                rightWingOffset = new Vector2(8, -8),     // Out + down, collapse to defensive zone
                leftDefenseOffset = new Vector2(3, 5),    // Cover slot, up
                rightDefenseOffset = new Vector2(3, -5)   // Cover slot, down
            };
        }
        if (neutralFormation == null)
        {
            neutralFormation = new FormationOffsets
            {
                // Neutral: Balanced positioning (relative to puck)
                // X = rink length (toward goals), Y = rink width (up/down spread)
                centerOffset = new Vector2(-3, 0),        // Trail behind slightly
                leftWingOffset = new Vector2(0, 10),      // Up on screen, even depth
                rightWingOffset = new Vector2(0, -10),    // Down on screen, even depth
                leftDefenseOffset = new Vector2(-8, 6),   // Behind, up
                rightDefenseOffset = new Vector2(-8, -6)  // Behind, down
            };
        }

        Debug.Log("FormationManager initialized");
    }

    private void Update()
    {
        // Update formation based on game state
        UpdateFormationState();
    }

    /// <summary>
    /// Update current formation based on puck possession and position
    /// </summary>
    private void UpdateFormationState()
    {
        if (puckTransform == null || ownGoal == null) return;

        // Check who has possession
        bool playerHasPuck = false;
        bool opponentHasPuck = false;

        // Check if controlled player has puck
        if (ContextButtonManager.Instance != null)
        {
            playerHasPuck = ContextButtonManager.Instance.HasPuck;
        }

        // Check if any teammate has puck
        if (!playerHasPuck)
        {
            TeammateController[] teammates = FindObjectsOfType<TeammateController>();
            foreach (TeammateController teammate in teammates)
            {
                if (teammate.enabled && teammate.isAI)
                {
                    float distanceToPuck = Vector2.Distance(teammate.transform.position, puckTransform.position);
                    if (distanceToPuck < 1.5f)
                    {
                        playerHasPuck = true;
                        break;
                    }
                }
            }
        }

        // Check if opponent has puck
        if (!playerHasPuck)
        {
            AIController[] opponents = FindObjectsOfType<AIController>();
            foreach (AIController ai in opponents)
            {
                if (ai.HasPuck)
                {
                    opponentHasPuck = true;
                    break;
                }
            }
        }

        // Determine formation based on which TEAM has possession
        // IMPORTANT: Logic must be team-aware!
        // - Player team: playerHasPuck = Offensive, opponentHasPuck = Defensive
        // - Opponent team: playerHasPuck = Defensive, opponentHasPuck = Offensive (INVERTED!)
        FormationType targetFormation;

        bool weHavePuck, theyHavePuck;

        if (team == Team.Player)
        {
            // Player team perspective
            weHavePuck = playerHasPuck;
            theyHavePuck = opponentHasPuck;
        }
        else // Team.Opponent
        {
            // Opponent team perspective (INVERTED)
            weHavePuck = opponentHasPuck;
            theyHavePuck = playerHasPuck;
        }

        if (weHavePuck)
        {
            targetFormation = FormationType.Offensive;
        }
        else if (theyHavePuck)
        {
            targetFormation = FormationType.Defensive;
        }
        else
        {
            // Neutral - check puck position relative to our goal
            float puckDistanceToOwnGoal = Vector2.Distance(puckTransform.position, ownGoal.position);
            float puckDistanceToOpponentGoal = Vector2.Distance(puckTransform.position, playerGoal.position);

            if (puckDistanceToOwnGoal < puckDistanceToOpponentGoal - formationSwitchDistance)
            {
                targetFormation = FormationType.Defensive; // Puck in our zone
            }
            else if (puckDistanceToOpponentGoal < puckDistanceToOwnGoal - formationSwitchDistance)
            {
                targetFormation = FormationType.Offensive; // Puck in their zone
            }
            else
            {
                targetFormation = FormationType.Neutral; // Neutral zone
            }
        }

        // Update formation (with state change logging)
        if (targetFormation != currentFormation)
        {
            Debug.Log($"Formation changed: {currentFormation} -> {targetFormation}");
            currentFormation = targetFormation;
        }
    }

    /// <summary>
    /// Get target formation position for a specific player role (with zone constraints)
    /// </summary>
    public Vector2 GetFormationPosition(PlayerRole role)
    {
        if (puckTransform == null) return Vector2.zero;

        FormationOffsets formation = GetCurrentFormation();
        Vector2 referencePoint = GetReferencePoint();

        // Get base offset based on role
        Vector2 offset = role switch
        {
            PlayerRole.Center => formation.centerOffset,
            PlayerRole.LeftWing => formation.leftWingOffset,
            PlayerRole.RightWing => formation.rightWingOffset,
            PlayerRole.LeftDefense => formation.leftDefenseOffset,
            PlayerRole.RightDefense => formation.rightDefenseOffset,
            _ => Vector2.zero
        };

        Vector2 originalOffset = offset;

        // Transform offset based on attack direction (for both offensive AND defensive formations)
        // Offensive: Formation relative to attack direction
        // Defensive: Formation relative to goal being defended
        if (currentFormation == FormationType.Offensive || currentFormation == FormationType.Defensive)
        {
            offset = TransformOffsetForAttackDirection(offset);
        }

        // Apply defensive style (for defensive formation)
        if (currentFormation == FormationType.Defensive)
        {
            offset = ApplyDefensiveStyle(offset, role);
        }

        // Calculate absolute position
        Vector2 targetPosition = referencePoint + offset;
        Vector2 beforeConstraints = targetPosition;

        // Apply zone constraints if enabled
        if (useZoneConstraints)
        {
            targetPosition = ApplyZoneConstraints(targetPosition, role);
        }

        // TEMPORARY DEBUG: Log formation positions to diagnose off-ice bug
        if (Time.frameCount % 120 == 0) // Log twice per second at 60fps
        {
            Debug.Log($"[{team}] {currentFormation} {role}: Ref={referencePoint:F1}, BaseOff={originalOffset}, " +
                     $"TransOff={offset}, BeforeConst={beforeConstraints:F1}, Final={targetPosition:F1}");
        }

        return targetPosition;
    }

    /// <summary>
    /// Apply defensive style modifications to formation offset
    /// Based on Weiss Tech Playbook defensive systems
    /// </summary>
    private Vector2 ApplyDefensiveStyle(Vector2 baseOffset, PlayerRole role)
    {
        if (puckTransform == null || ownGoal == null) return baseOffset;

        // Determine strong-side (side where puck is) vs weak-side
        bool puckOnLeftSide = puckTransform.position.y > ownGoal.position.y;
        bool isStrongSide = (puckOnLeftSide && (role == PlayerRole.LeftWing || role == PlayerRole.LeftDefense)) ||
                            (!puckOnLeftSide && (role == PlayerRole.RightWing || role == PlayerRole.RightDefense));

        switch (defensiveStyle)
        {
            case DefensiveStyle.BoxPlusOne:
                // Simple Box: All 4 players form box in front of net (very passive)
                // Tight box formation, protect slot
                return ApplyBoxPlusOneStyle(baseOffset, role);

            case DefensiveStyle.SaggingZone:
                // Sagging Zone (Wedge Plus One): Weak-side forward sags to slot
                // Strong-side forward pressures, weak-side forward helps defensemen
                return ApplySaggingZoneStyle(baseOffset, role, isStrongSide);

            case DefensiveStyle.SaggingZoneArrow:
                // Sagging Zone Arrow: More aggressive, high pressure
                // Similar to Sagging Zone but forwards push higher
                return ApplySaggingZoneArrowStyle(baseOffset, role, isStrongSide);

            default:
                return baseOffset;
        }
    }

    /// <summary>
    /// Box +1: Passive box formation protecting slot
    /// </summary>
    private Vector2 ApplyBoxPlusOneStyle(Vector2 baseOffset, PlayerRole role)
    {
        // Modify offsets to create tight box in front of net
        switch (role)
        {
            case PlayerRole.LeftWing:
            case PlayerRole.RightWing:
                // Forwards form top corners of box
                return new Vector2(6, baseOffset.y * 0.8f); // Closer together

            case PlayerRole.LeftDefense:
            case PlayerRole.RightDefense:
                // Defensemen form bottom corners of box (tight to net)
                return new Vector2(2, baseOffset.y * 0.7f); // Very tight

            default:
                return baseOffset;
        }
    }

    /// <summary>
    /// Sagging Zone (Wedge Plus One): Weak-side forward sags to slot
    /// </summary>
    private Vector2 ApplySaggingZoneStyle(Vector2 baseOffset, PlayerRole role, bool isStrongSide)
    {
        // Strong-side forward: Pressure puck carrier (seam coverage)
        // Weak-side forward: SAG down to slot to help defensemen
        // Defensemen: Protect front of net

        if (role == PlayerRole.LeftWing || role == PlayerRole.RightWing)
        {
            if (isStrongSide)
            {
                // Strong-side forward: Cover seam, pressure slightly
                return new Vector2(baseOffset.x + 2, baseOffset.y); // Push out a bit
            }
            else
            {
                // Weak-side forward: SAG to slot (move closer to net, toward center)
                return new Vector2(4, baseOffset.y * 0.5f); // Drop down to slot
            }
        }

        // Defensemen: Stay tight to net
        return baseOffset;
    }

    /// <summary>
    /// Sagging Zone Arrow: Aggressive high pressure variant
    /// </summary>
    private Vector2 ApplySaggingZoneArrowStyle(Vector2 baseOffset, PlayerRole role, bool isStrongSide)
    {
        // Similar to Sagging Zone but forwards are more aggressive

        if (role == PlayerRole.LeftWing || role == PlayerRole.RightWing)
        {
            if (isStrongSide)
            {
                // Strong-side forward: Aggressive pressure at blueline
                return new Vector2(baseOffset.x + 5, baseOffset.y); // Push WAY out
            }
            else
            {
                // Weak-side forward: Still sags but a bit higher
                return new Vector2(5, baseOffset.y * 0.6f); // Higher slot coverage
            }
        }

        // Defensemen: Can push up slightly
        if (role == PlayerRole.LeftDefense || role == PlayerRole.RightDefense)
        {
            return new Vector2(baseOffset.x + 1, baseOffset.y); // Slightly more aggressive
        }

        return baseOffset;
    }

    /// <summary>
    /// Transform formation offset based on attack direction
    /// Formations are defined assuming attack toward RIGHT (positive X)
    /// This method flips X component if attacking toward LEFT (negative X)
    /// </summary>
    private Vector2 TransformOffsetForAttackDirection(Vector2 baseOffset)
    {
        if (playerGoal == null || ownGoal == null) return baseOffset;

        // Determine attack direction (from own goal to opponent goal)
        float attackDirectionX = playerGoal.position.x - ownGoal.position.x;

        // TEMPORARY DEBUG
        if (Time.frameCount % 240 == 0) // Log once every 4 seconds
        {
            Debug.Log($"[{team}] Transform: AttackGoal={playerGoal.position.x:F1}, OwnGoal={ownGoal.position.x:F1}, " +
                     $"AttackDir={attackDirectionX:F1}, Flip={attackDirectionX < 0}");
        }

        // If attacking LEFT (negative X), flip the X component of offset
        if (attackDirectionX < 0)
        {
            return new Vector2(-baseOffset.x, baseOffset.y);
        }

        // If attacking RIGHT (positive X), use offset as-is
        return baseOffset;
    }

    /// <summary>
    /// Apply zone/lane constraints to keep players in realistic positions
    /// </summary>
    private Vector2 ApplyZoneConstraints(Vector2 targetPosition, PlayerRole role)
    {
        if (ownGoal == null) return targetPosition;

        Vector2 constrainedPosition = targetPosition;

        switch (role)
        {
            case PlayerRole.LeftDefense:
            case PlayerRole.RightDefense:
                // Defensemen: Can't push past certain distance from own goal (X axis = toward goals)
                float distanceFromOwnGoal = Vector2.Distance(targetPosition, ownGoal.position);
                if (distanceFromOwnGoal > defenseMaxPushDistance)
                {
                    // Clamp to max push distance
                    Vector2 directionFromGoal = (targetPosition - (Vector2)ownGoal.position).normalized;
                    constrainedPosition = (Vector2)ownGoal.position + directionFromGoal * defenseMaxPushDistance;
                }

                // Also stay on their side (Y axis = up/down on screen)
                if (role == PlayerRole.LeftDefense)
                {
                    constrainedPosition.y = Mathf.Max(constrainedPosition.y, 3f); // Stay up (positive Y)
                }
                else // RightDefense
                {
                    constrainedPosition.y = Mathf.Min(constrainedPosition.y, -3f); // Stay down (negative Y)
                }
                break;

            case PlayerRole.LeftWing:
                // Left wing: Stay in upper lane (Y axis = up/down on screen)
                constrainedPosition.y = Mathf.Clamp(constrainedPosition.y, wingLaneWidth, 100f);
                break;

            case PlayerRole.RightWing:
                // Right wing: Stay in lower lane (Y axis = up/down on screen)
                constrainedPosition.y = Mathf.Clamp(constrainedPosition.y, -100f, -wingLaneWidth);
                break;

            case PlayerRole.Center:
                // Center: Stay near center ice (don't drift too far up/down on Y axis)
                constrainedPosition.y = Mathf.Clamp(constrainedPosition.y, -centerLaneWidth, centerLaneWidth);
                break;
        }

        return constrainedPosition;
    }

    /// <summary>
    /// Get current formation offsets based on formation type
    /// </summary>
    private FormationOffsets GetCurrentFormation()
    {
        return currentFormation switch
        {
            FormationType.Offensive => offensiveFormation,
            FormationType.Defensive => defensiveFormation,
            FormationType.Neutral => neutralFormation,
            _ => neutralFormation
        };
    }

    /// <summary>
    /// Get reference point for formation positioning
    /// </summary>
    private Vector2 GetReferencePoint()
    {
        switch (currentFormation)
        {
            case FormationType.Offensive:
                // Offensive: positions relative to puck carrier (or puck if loose)
                if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentPlayer != null)
                {
                    return PlayerManager.Instance.CurrentPlayer.transform.position;
                }
                return puckTransform.position;

            case FormationType.Defensive:
                // Defensive: positions relative to own goal
                if (ownGoal != null)
                {
                    return ownGoal.position;
                }
                return Vector2.zero;

            case FormationType.Neutral:
                // Neutral: positions relative to center ice (puck location)
                return puckTransform.position;

            default:
                return puckTransform.position;
        }
    }

    /// <summary>
    /// Check if a specific role should pressure the puck carrier/loose puck
    /// </summary>
    public bool ShouldPressurePuck(PlayerRole role)
    {
        switch (currentFormation)
        {
            case FormationType.Defensive:
                // In defense, only one forward should pressure
                return role == PlayerRole.Center; // Center forechecks

            case FormationType.Offensive:
                // In offense, no one pressures (they're supporting puck carrier)
                return false;

            case FormationType.Neutral:
                // In neutral, nearest player chases (handled by caller)
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Get current formation type (public accessor)
    /// </summary>
    public FormationType CurrentFormation => currentFormation;

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        if (puckTransform == null) return;

        // Draw formation positions for all roles
        DrawFormationPosition(PlayerRole.Center, Color.yellow);
        DrawFormationPosition(PlayerRole.LeftWing, Color.cyan);
        DrawFormationPosition(PlayerRole.RightWing, Color.cyan);
        DrawFormationPosition(PlayerRole.LeftDefense, Color.blue);
        DrawFormationPosition(PlayerRole.RightDefense, Color.blue);
    }

    private void DrawFormationPosition(PlayerRole role, Color color)
    {
        Vector2 position = GetFormationPosition(role);
        Gizmos.color = color;
        Gizmos.DrawWireSphere(position, 0.5f);
        Gizmos.DrawLine(GetReferencePoint(), position);
    }
}

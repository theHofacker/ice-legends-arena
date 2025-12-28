using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages team formations for hockey gameplay.
/// Coordinates positioning for all teammates based on game state (offense/defense/neutral).
/// Inspired by professional sports games like NHL, FIFA, etc.
/// </summary>
public class FormationManager : MonoBehaviour
{
    public static FormationManager Instance { get; private set; }

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

    [Tooltip("Max distance wings can drift from their lane (left/right)")]
    [Range(3f, 12f)]
    [SerializeField] private float wingLaneWidth = 8f;

    [Tooltip("Max distance center can drift from center ice (left/right)")]
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
    /// </summary>
    [System.Serializable]
    public class FormationOffsets
    {
        [Header("Forward Positions (relative to reference point)")]
        [Tooltip("Center position offset")]
        public Vector2 centerOffset = new Vector2(0, -4);      // Trail behind puck carrier

        [Tooltip("Left Wing position offset")]
        public Vector2 leftWingOffset = new Vector2(-10, 3);   // Left lane, ahead

        [Tooltip("Right Wing position offset")]
        public Vector2 rightWingOffset = new Vector2(10, 3);   // Right lane, ahead

        [Header("Defense Positions (relative to reference point)")]
        [Tooltip("Left Defense position offset")]
        public Vector2 leftDefenseOffset = new Vector2(-6, -10);  // Left defensive zone

        [Tooltip("Right Defense position offset")]
        public Vector2 rightDefenseOffset = new Vector2(6, -10);  // Right defensive zone
    }

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
        }

        // Find goals
        GameObject[] goals = GameObject.FindGameObjectsWithTag("Goal");
        if (goals.Length >= 2)
        {
            // TODO: Assign goals based on team
            // For now: bottom goal = our goal, top goal = opponent goal
            ownGoal = goals[0].transform;
            playerGoal = goals[1].transform;
        }

        // Initialize formation offsets with default values if not set
        if (offensiveFormation == null)
        {
            offensiveFormation = new FormationOffsets
            {
                // Offensive: Spread out, push forward
                centerOffset = new Vector2(0, -4),        // Trail for drop passes
                leftWingOffset = new Vector2(-10, 3),     // Wide left, ahead
                rightWingOffset = new Vector2(10, 3),     // Wide right, ahead
                leftDefenseOffset = new Vector2(-6, -12), // Stay back (blue line)
                rightDefenseOffset = new Vector2(6, -12)  // Stay back (blue line)
            };
        }
        if (defensiveFormation == null)
        {
            defensiveFormation = new FormationOffsets
            {
                // Defensive: Collapse, protect goal
                centerOffset = new Vector2(0, -6),        // Pressure puck carrier
                leftWingOffset = new Vector2(-8, -8),     // Collapse to defensive zone
                rightWingOffset = new Vector2(8, -8),     // Collapse to defensive zone
                leftDefenseOffset = new Vector2(-5, -3),  // Cover slot
                rightDefenseOffset = new Vector2(5, -3)   // Cover slot
            };
        }
        if (neutralFormation == null)
        {
            neutralFormation = new FormationOffsets
            {
                // Neutral: Balanced positioning
                centerOffset = new Vector2(0, 0),
                leftWingOffset = new Vector2(-10, 2),
                rightWingOffset = new Vector2(10, 2),
                leftDefenseOffset = new Vector2(-6, -8),
                rightDefenseOffset = new Vector2(6, -8)
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

        // Determine formation
        FormationType targetFormation;

        if (playerHasPuck)
        {
            targetFormation = FormationType.Offensive;
        }
        else if (opponentHasPuck)
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

        // Get offset based on role
        Vector2 offset = role switch
        {
            PlayerRole.Center => formation.centerOffset,
            PlayerRole.LeftWing => formation.leftWingOffset,
            PlayerRole.RightWing => formation.rightWingOffset,
            PlayerRole.LeftDefense => formation.leftDefenseOffset,
            PlayerRole.RightDefense => formation.rightDefenseOffset,
            _ => Vector2.zero
        };

        // Calculate absolute position
        Vector2 targetPosition = referencePoint + offset;

        // Apply zone constraints if enabled
        if (useZoneConstraints)
        {
            targetPosition = ApplyZoneConstraints(targetPosition, role);
        }

        return targetPosition;
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
                // Defensemen: Can't push past certain distance from own goal
                float distanceFromOwnGoal = Vector2.Distance(targetPosition, ownGoal.position);
                if (distanceFromOwnGoal > defenseMaxPushDistance)
                {
                    // Clamp to max push distance
                    Vector2 directionFromGoal = (targetPosition - (Vector2)ownGoal.position).normalized;
                    constrainedPosition = (Vector2)ownGoal.position + directionFromGoal * defenseMaxPushDistance;
                }

                // Also stay on their side (left/right)
                if (role == PlayerRole.LeftDefense)
                {
                    constrainedPosition.x = Mathf.Min(constrainedPosition.x, -3f); // Stay on left
                }
                else // RightDefense
                {
                    constrainedPosition.x = Mathf.Max(constrainedPosition.x, 3f); // Stay on right
                }
                break;

            case PlayerRole.LeftWing:
                // Left wing: Stay in left lane
                constrainedPosition.x = Mathf.Clamp(constrainedPosition.x, -100f, -wingLaneWidth);
                break;

            case PlayerRole.RightWing:
                // Right wing: Stay in right lane
                constrainedPosition.x = Mathf.Clamp(constrainedPosition.x, wingLaneWidth, 100f);
                break;

            case PlayerRole.Center:
                // Center: Stay near center ice (don't drift too far left/right)
                constrainedPosition.x = Mathf.Clamp(constrainedPosition.x, -centerLaneWidth, centerLaneWidth);
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

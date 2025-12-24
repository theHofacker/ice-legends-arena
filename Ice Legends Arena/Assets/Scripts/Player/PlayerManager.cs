using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the team of 5 players and handles switching between them.
/// Singleton pattern for global access.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Team Setup")]
    [Tooltip("All 5 players on your team (assign in Inspector)")]
    [SerializeField] private List<GameObject> teamPlayers = new List<GameObject>();

    [Header("Visual Indicator")]
    [Tooltip("Prefab for visual indicator (colored ring/glow)")]
    [SerializeField] private GameObject controlIndicatorPrefab;

    [Tooltip("Color for controlled player indicator")]
    [SerializeField] private Color controlledPlayerColor = Color.cyan;

    [Header("Switching Settings")]
    [Tooltip("Minimum time between switches to prevent spam")]
    [Range(0.1f, 1f)]
    [SerializeField] private float switchCooldown = 0.2f;

    // State
    private int currentPlayerIndex = 0; // Index of currently controlled player
    private GameObject currentControlIndicator; // Visual indicator instance
    private float lastSwitchTime = -999f;
    private Transform puckTransform;
    private Dictionary<GameObject, Color> originalPlayerColors = new Dictionary<GameObject, Color>(); // Store original colors

    // Public properties
    public GameObject CurrentPlayer => (teamPlayers.Count > 0 && currentPlayerIndex < teamPlayers.Count)
        ? teamPlayers[currentPlayerIndex]
        : null;

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

        // Validate team setup
        if (teamPlayers.Count == 0)
        {
            Debug.LogWarning("PlayerManager: No team players assigned! Searching for players with 'Player' tag...");
            AutoDetectPlayers();
        }

        // Initialize all players (disable control on all of them first)
        InitializeAllPlayers();

        // Set initial controlled player (enable control on player 0)
        if (teamPlayers.Count > 0)
        {
            SwitchToPlayer(0);
        }

        // Subscribe to SWITCH button events
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnSwitchRequested += HandleSwitchRequested;
        }
    }

    /// <summary>
    /// Initialize all players - disable player control, enable AI, store original colors
    /// </summary>
    private void InitializeAllPlayers()
    {
        foreach (GameObject player in teamPlayers)
        {
            if (player == null) continue;

            // Store original color
            SpriteRenderer spriteRenderer = player.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && !originalPlayerColors.ContainsKey(player))
            {
                originalPlayerColors[player] = spriteRenderer.color;
            }

            // Disable all player control scripts
            DisablePlayerControl(player);
        }

        Debug.Log($"PlayerManager: Initialized {teamPlayers.Count} players (all set to AI mode)");
    }

    /// <summary>
    /// Auto-detect players with "Player" tag and add to team
    /// </summary>
    private void AutoDetectPlayers()
    {
        GameObject[] foundPlayers = GameObject.FindGameObjectsWithTag("Player");
        teamPlayers.Clear();

        foreach (GameObject player in foundPlayers)
        {
            teamPlayers.Add(player);
        }

        Debug.Log($"PlayerManager: Auto-detected {teamPlayers.Count} players");
    }

    /// <summary>
    /// Handle SWITCH button request from ContextButtonManager
    /// </summary>
    private void HandleSwitchRequested(bool isHeld)
    {
        // Check cooldown
        if (Time.time - lastSwitchTime < switchCooldown)
        {
            return;
        }

        if (isHeld)
        {
            // Hold = switch to last defender (furthest back player)
            SwitchToLastDefender();
        }
        else
        {
            // Tap = switch to player nearest puck
            SwitchToNearestToPuck();
        }

        lastSwitchTime = Time.time;
    }

    /// <summary>
    /// Switch to player nearest the puck
    /// </summary>
    private void SwitchToNearestToPuck()
    {
        if (puckTransform == null || teamPlayers.Count == 0) return;

        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < teamPlayers.Count; i++)
        {
            if (teamPlayers[i] == null) continue;

            float distance = Vector2.Distance(teamPlayers[i].transform.position, puckTransform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        SwitchToPlayer(nearestIndex);
        Debug.Log($"Switched to player nearest puck: Player {nearestIndex + 1}");
    }

    /// <summary>
    /// Switch to last defender (furthest back player)
    /// Assumes "back" means lowest Y position (defending bottom goal)
    /// </summary>
    private void SwitchToLastDefender()
    {
        if (teamPlayers.Count == 0) return;

        int lastDefenderIndex = 0;
        float lowestY = float.MaxValue;

        for (int i = 0; i < teamPlayers.Count; i++)
        {
            if (teamPlayers[i] == null) continue;

            float yPosition = teamPlayers[i].transform.position.y;
            if (yPosition < lowestY)
            {
                lowestY = yPosition;
                lastDefenderIndex = i;
            }
        }

        SwitchToPlayer(lastDefenderIndex);
        Debug.Log($"Switched to last defender: Player {lastDefenderIndex + 1} (Y: {lowestY:F2})");
    }

    /// <summary>
    /// Cycle to next player clockwise
    /// </summary>
    public void CycleToNextPlayer()
    {
        if (teamPlayers.Count == 0) return;

        int nextIndex = (currentPlayerIndex + 1) % teamPlayers.Count;
        SwitchToPlayer(nextIndex);
        Debug.Log($"Cycled to next player: Player {nextIndex + 1}");
    }

    /// <summary>
    /// Switch control to a specific player by index
    /// </summary>
    private void SwitchToPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= teamPlayers.Count) return;
        if (teamPlayers[playerIndex] == null) return;

        // Store reference to old player before switching
        GameObject oldPlayer = CurrentPlayer;

        // Disable control on old player
        if (oldPlayer != null)
        {
            DisablePlayerControl(oldPlayer);
            RestorePlayerColor(oldPlayer); // Restore original color
        }

        // Update current player
        currentPlayerIndex = playerIndex;

        // Enable control on new player
        EnablePlayerControl(CurrentPlayer);

        // Update visual indicator (this will change color to controlled color)
        UpdateControlIndicator();
    }

    /// <summary>
    /// Enable player control (disable AI, enable input)
    /// </summary>
    private void EnablePlayerControl(GameObject player)
    {
        if (player == null) return;

        // Enable PlayerController
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        // Enable all player component scripts (shooting, passing, checking)
        ShootingController shootingController = player.GetComponent<ShootingController>();
        if (shootingController != null) shootingController.enabled = true;

        PassingController passingController = player.GetComponent<PassingController>();
        if (passingController != null) passingController.enabled = true;

        CheckingController checkingController = player.GetComponent<CheckingController>();
        if (checkingController != null) checkingController.enabled = true;

        // Disable AI (if it exists)
        TeammateController aiController = player.GetComponent<TeammateController>();
        if (aiController != null)
        {
            aiController.enabled = false;
        }

        Debug.Log($"Enabled control for {player.name}");
    }

    /// <summary>
    /// Disable player control (enable AI, disable input)
    /// </summary>
    private void DisablePlayerControl(GameObject player)
    {
        if (player == null) return;

        // Disable PlayerController
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // Disable all player component scripts
        ShootingController shootingController = player.GetComponent<ShootingController>();
        if (shootingController != null) shootingController.enabled = false;

        PassingController passingController = player.GetComponent<PassingController>();
        if (passingController != null) passingController.enabled = false;

        CheckingController checkingController = player.GetComponent<CheckingController>();
        if (checkingController != null) checkingController.enabled = false;

        // Enable AI (if it exists)
        TeammateController aiController = player.GetComponent<TeammateController>();
        if (aiController != null)
        {
            aiController.enabled = true;
            aiController.isAI = true; // Ensure AI mode is on
        }

        Debug.Log($"Disabled control for {player.name}");
    }

    /// <summary>
    /// Update visual indicator to show controlled player
    /// </summary>
    private void UpdateControlIndicator()
    {
        if (CurrentPlayer == null) return;

        // Destroy old indicator
        if (currentControlIndicator != null)
        {
            Destroy(currentControlIndicator);
        }

        // Create new indicator if prefab exists
        if (controlIndicatorPrefab != null)
        {
            currentControlIndicator = Instantiate(controlIndicatorPrefab, CurrentPlayer.transform);
            currentControlIndicator.transform.localPosition = Vector3.zero;

            // Set indicator color
            SpriteRenderer indicatorSprite = currentControlIndicator.GetComponent<SpriteRenderer>();
            if (indicatorSprite != null)
            {
                indicatorSprite.color = controlledPlayerColor;
            }
        }
        else
        {
            // Fallback: tint player sprite
            SpriteRenderer playerSprite = CurrentPlayer.GetComponent<SpriteRenderer>();
            if (playerSprite != null)
            {
                playerSprite.color = controlledPlayerColor;
            }
        }
    }

    /// <summary>
    /// Restore player's original color
    /// </summary>
    private void RestorePlayerColor(GameObject player)
    {
        if (player == null) return;

        // Only restore color if using sprite tint fallback (no indicator prefab)
        if (controlIndicatorPrefab == null)
        {
            SpriteRenderer playerSprite = player.GetComponent<SpriteRenderer>();
            if (playerSprite != null && originalPlayerColors.ContainsKey(player))
            {
                playerSprite.color = originalPlayerColors[player];
                Debug.Log($"Restored {player.name} to original color: {originalPlayerColors[player]}");
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnSwitchRequested -= HandleSwitchRequested;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Debug visualization
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (teamPlayers == null || teamPlayers.Count == 0) return;

        // Draw connections between team players
        Gizmos.color = Color.cyan;
        for (int i = 0; i < teamPlayers.Count; i++)
        {
            if (teamPlayers[i] == null) continue;

            // Draw sphere at player position
            Gizmos.DrawWireSphere(teamPlayers[i].transform.position, 0.5f);

            // Draw line to next player (cycle)
            int nextIndex = (i + 1) % teamPlayers.Count;
            if (teamPlayers[nextIndex] != null)
            {
                Gizmos.DrawLine(teamPlayers[i].transform.position, teamPlayers[nextIndex].transform.position);
            }
        }

        // Highlight current player
        if (Application.isPlaying && CurrentPlayer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(CurrentPlayer.transform.position, 0.8f);
        }
    }
}

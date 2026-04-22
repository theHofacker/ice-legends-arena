using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the team of 5 players and handles switching between them.
/// Singleton pattern for global access.
/// 3D: distance checks use XZ plane, "last defender" checks X position (rink length).
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    // Event fired when player switches
    public delegate void PlayerSwitchedDelegate(GameObject newPlayer, GameObject oldPlayer);
    public event PlayerSwitchedDelegate OnPlayerSwitched;

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

    [Header("Team Orientation")]
    [Tooltip("X position of own goal (negative = left side). Used to determine 'last defender'.")]
    [SerializeField] private float ownGoalX = 25f;

    // State
    private int currentPlayerIndex = 0;
    private GameObject currentControlIndicator;
    private float lastSwitchTime = -999f;
    private Transform puckTransform;
    private Dictionary<GameObject, Color> originalPlayerColors = new Dictionary<GameObject, Color>();

    // Public properties
    public GameObject CurrentPlayer => (teamPlayers.Count > 0 && currentPlayerIndex < teamPlayers.Count)
        ? teamPlayers[currentPlayerIndex]
        : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        UpdatePuckReference();

        if (teamPlayers.Count == 0)
        {
            Debug.LogWarning("PlayerManager: No team players assigned! Searching for players with 'Player' tag...");
            AutoDetectPlayers();
        }

        InitializeAllPlayers();

        if (teamPlayers.Count > 0)
        {
            SwitchToPlayer(0);
        }

        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnSwitchRequested += HandleSwitchRequested;
        }
    }

    private void UpdatePuckReference()
    {
        if (puckTransform == null)
        {
            GameObject puck = GameObject.FindGameObjectWithTag("Puck");
            if (puck != null)
            {
                puckTransform = puck.transform;
            }
        }
    }

    private void InitializeAllPlayers()
    {
        foreach (GameObject player in teamPlayers)
        {
            if (player == null) continue;

            // Store original color (for SpriteRenderer fallback if still using sprites)
            SpriteRenderer spriteRenderer = player.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && !originalPlayerColors.ContainsKey(player))
            {
                originalPlayerColors[player] = spriteRenderer.color;
            }

            DisablePlayerControl(player);
        }

        Debug.Log($"PlayerManager: Initialized {teamPlayers.Count} players (all set to AI mode)");
    }

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

    private void HandleSwitchRequested(bool isHeld)
    {
        if (Time.time - lastSwitchTime < switchCooldown)
        {
            return;
        }

        if (isHeld)
        {
            SwitchToLastDefender();
        }
        else
        {
            SwitchToNearestToPuck();
        }

        lastSwitchTime = Time.time;
    }

    private void SwitchToNearestToPuck()
    {
        UpdatePuckReference();

        if (puckTransform == null)
        {
            Debug.LogWarning("PlayerManager: Cannot switch - puck reference is null!");
            return;
        }

        if (teamPlayers.Count == 0)
        {
            Debug.LogWarning("PlayerManager: Cannot switch - no team players!");
            return;
        }

        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < teamPlayers.Count; i++)
        {
            if (teamPlayers[i] == null) continue;

            float distance = PhysicsHelper.DistanceXZ(teamPlayers[i].transform.position, puckTransform.position);

            Debug.Log($"  Player {i + 1} distance to puck: {distance:F2}");

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        Debug.Log($"-> Switching to Player {nearestIndex + 1} (nearest to puck at {nearestDistance:F2} units)");
        SwitchToPlayer(nearestIndex);
    }

    /// <summary>
    /// Switch to last defender (closest to own goal along X axis).
    /// In 3D: X = rink length, own goal is at ownGoalX.
    /// </summary>
    private void SwitchToLastDefender()
    {
        if (teamPlayers.Count == 0) return;

        int lastDefenderIndex = 0;
        float closestToOwnGoal = float.MaxValue;

        for (int i = 0; i < teamPlayers.Count; i++)
        {
            if (teamPlayers[i] == null) continue;

            // Distance from own goal along X axis (rink length)
            float distFromOwnGoal = Mathf.Abs(teamPlayers[i].transform.position.x - ownGoalX);
            if (distFromOwnGoal < closestToOwnGoal)
            {
                closestToOwnGoal = distFromOwnGoal;
                lastDefenderIndex = i;
            }
        }

        SwitchToPlayer(lastDefenderIndex);
        Debug.Log($"Switched to last defender: Player {lastDefenderIndex + 1} (X: {teamPlayers[lastDefenderIndex].transform.position.x:F2})");
    }

    public void CycleToNextPlayer()
    {
        if (teamPlayers.Count == 0) return;

        int nextIndex = (currentPlayerIndex + 1) % teamPlayers.Count;
        SwitchToPlayer(nextIndex);
        Debug.Log($"Cycled to next player: Player {nextIndex + 1}");
    }

    public void SwitchToPlayerByGameObject(GameObject targetPlayer)
    {
        if (targetPlayer == null) return;

        int playerIndex = teamPlayers.IndexOf(targetPlayer);

        if (playerIndex >= 0)
        {
            SwitchToPlayer(playerIndex);
            Debug.Log($"Auto-switched to {targetPlayer.name} (pass receiver)");
        }
        else
        {
            Debug.LogWarning($"Cannot auto-switch: {targetPlayer.name} not found in team players list!");
        }
    }

    private void SwitchToPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= teamPlayers.Count) return;
        if (teamPlayers[playerIndex] == null) return;

        GameObject oldPlayer = CurrentPlayer;

        if (oldPlayer != null)
        {
            DisablePlayerControl(oldPlayer);
            RestorePlayerColor(oldPlayer);
        }

        currentPlayerIndex = playerIndex;

        EnablePlayerControl(CurrentPlayer);
        UpdateControlIndicator();

        OnPlayerSwitched?.Invoke(CurrentPlayer, oldPlayer);
    }

    private void EnablePlayerControl(GameObject player)
    {
        if (player == null) return;

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null) playerController.enabled = true;

        ShootingController shootingController = player.GetComponent<ShootingController>();
        if (shootingController != null) shootingController.enabled = true;

        PassingController passingController = player.GetComponent<PassingController>();
        if (passingController != null) passingController.enabled = true;

        CheckingController checkingController = player.GetComponent<CheckingController>();
        if (checkingController != null) checkingController.enabled = true;

        TeammateController aiController = player.GetComponent<TeammateController>();
        if (aiController != null) aiController.enabled = false;

        Debug.Log($"Enabled control for {player.name}");
    }

    private void DisablePlayerControl(GameObject player)
    {
        if (player == null) return;

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null) playerController.enabled = false;

        ShootingController shootingController = player.GetComponent<ShootingController>();
        if (shootingController != null) shootingController.enabled = false;

        PassingController passingController = player.GetComponent<PassingController>();
        if (passingController != null) passingController.enabled = false;

        CheckingController checkingController = player.GetComponent<CheckingController>();
        if (checkingController != null) checkingController.enabled = false;

        TeammateController aiController = player.GetComponent<TeammateController>();
        if (aiController != null)
        {
            aiController.enabled = true;
            aiController.isAI = true;
            Debug.Log($"Enabled AI for {player.name} (isAI={aiController.isAI}, enabled={aiController.enabled})");
        }
        else
        {
            Debug.LogWarning($"{player.name} missing TeammateController! AI will not work.");
        }

        Debug.Log($"Disabled control for {player.name}");
    }

    private void UpdateControlIndicator()
    {
        if (CurrentPlayer == null) return;

        if (currentControlIndicator != null)
        {
            Destroy(currentControlIndicator);
        }

        if (controlIndicatorPrefab != null)
        {
            currentControlIndicator = Instantiate(controlIndicatorPrefab, CurrentPlayer.transform);
            currentControlIndicator.transform.localPosition = Vector3.zero;

            SpriteRenderer indicatorSprite = currentControlIndicator.GetComponent<SpriteRenderer>();
            if (indicatorSprite != null)
            {
                indicatorSprite.color = controlledPlayerColor;
            }
        }
        else
        {
            SpriteRenderer playerSprite = CurrentPlayer.GetComponent<SpriteRenderer>();
            if (playerSprite != null)
            {
                playerSprite.color = controlledPlayerColor;
            }
        }
    }

    private void RestorePlayerColor(GameObject player)
    {
        if (player == null) return;

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
        if (ContextButtonManager.Instance != null)
        {
            ContextButtonManager.Instance.OnSwitchRequested -= HandleSwitchRequested;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (teamPlayers == null || teamPlayers.Count == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < teamPlayers.Count; i++)
        {
            if (teamPlayers[i] == null) continue;

            Gizmos.DrawWireSphere(teamPlayers[i].transform.position, 0.5f);

            int nextIndex = (i + 1) % teamPlayers.Count;
            if (teamPlayers[nextIndex] != null)
            {
                Gizmos.DrawLine(teamPlayers[i].transform.position, teamPlayers[nextIndex].transform.position);
            }
        }

        if (Application.isPlaying && CurrentPlayer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(CurrentPlayer.transform.position, 0.8f);
        }
    }
}

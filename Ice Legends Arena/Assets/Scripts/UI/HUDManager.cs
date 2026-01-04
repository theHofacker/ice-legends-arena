using UnityEngine;
using TMPro;

/// <summary>
/// Manages the in-game HUD (timer, score, pause button, character portraits)
/// Subscribes to GameManager events to update UI in real-time
/// 100% transferable to 2.5D/3D!
/// </summary>
public class HUDManager : MonoBehaviour
{
    [Header("Score & Timer Display")]
    [Tooltip("TextMeshPro for displaying score (e.g., '2 - 1')")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Tooltip("TextMeshPro for displaying match timer (e.g., '4:32')")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Goal Celebration")]
    [Tooltip("TextMeshPro for goal celebration (e.g., 'GOAL!')")]
    [SerializeField] private TextMeshProUGUI goalCelebrationText;

    [Tooltip("How long to show goal celebration")]
    [SerializeField] private float goalCelebrationDuration = 2f;

    [Header("Pause Menu")]
    [Tooltip("Pause menu panel (shown when paused)")]
    [SerializeField] private GameObject pauseMenuPanel;

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = true;

    // Note: ESC key handling removed - use Pause button or add to InputSystem_Actions asset
    // The new Unity Input System is configured in this project, not the legacy Input class

    private void Start()
    {
        // Hide goal celebration initially
        if (goalCelebrationText != null)
        {
            goalCelebrationText.gameObject.SetActive(false);
        }

        // Hide pause menu initially
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        // Subscribe to GameManager events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged += UpdateScoreDisplay;
            GameManager.Instance.OnTimerChanged += UpdateTimerDisplay;
            GameManager.Instance.OnGoalScored += ShowGoalCelebration;
            GameManager.Instance.OnMatchStateChanged += HandleMatchStateChanged;

            // Initialize displays
            UpdateScoreDisplay(GameManager.Instance.PlayerScore, GameManager.Instance.OpponentScore);
            UpdateTimerDisplay(GameManager.Instance.TimeRemaining);
        }
        else
        {
            Debug.LogError("HUDManager: No GameManager found!");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged -= UpdateScoreDisplay;
            GameManager.Instance.OnTimerChanged -= UpdateTimerDisplay;
            GameManager.Instance.OnGoalScored -= ShowGoalCelebration;
            GameManager.Instance.OnMatchStateChanged -= HandleMatchStateChanged;
        }
    }

    /// <summary>
    /// Update score display when score changes
    /// </summary>
    private void UpdateScoreDisplay(int playerScore, int opponentScore)
    {
        if (scoreText != null)
        {
            scoreText.text = $"{playerScore} - {opponentScore}";
        }

        if (showDebugMessages)
        {
            Debug.Log($"HUD: Score updated to {playerScore} - {opponentScore}");
        }
    }

    /// <summary>
    /// Update timer display every frame
    /// </summary>
    private void UpdateTimerDisplay(float timeRemaining)
    {
        if (timerText != null)
        {
            timerText.text = GameManager.FormatTime(timeRemaining);
        }
    }

    /// <summary>
    /// Show goal celebration overlay
    /// </summary>
    private void ShowGoalCelebration(bool scoredByPlayer, int playerScore, int opponentScore)
    {
        if (goalCelebrationText != null)
        {
            // Show celebration
            goalCelebrationText.gameObject.SetActive(true);

            // Set celebration text
            if (scoredByPlayer)
            {
                goalCelebrationText.text = "GOAL!";
                goalCelebrationText.color = Color.green;
            }
            else
            {
                goalCelebrationText.text = "GOAL...";
                goalCelebrationText.color = Color.red;
            }

            // Hide after duration
            Invoke(nameof(HideGoalCelebration), goalCelebrationDuration);
        }

        if (showDebugMessages)
        {
            Debug.Log($"HUD: Goal celebration shown ({(scoredByPlayer ? "Player" : "Opponent")} scored)");
        }
    }

    /// <summary>
    /// Hide goal celebration overlay
    /// </summary>
    private void HideGoalCelebration()
    {
        if (goalCelebrationText != null)
        {
            goalCelebrationText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Handle match state changes (show/hide pause menu, etc.)
    /// </summary>
    private void HandleMatchStateChanged(GameManager.MatchState newState)
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(newState == GameManager.MatchState.Paused);
        }

        if (showDebugMessages)
        {
            Debug.Log($"HUD: Match state changed to {newState}");
        }
    }

    /// <summary>
    /// Public method to toggle pause (called from pause button)
    /// </summary>
    public void TogglePause()
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CurrentState == GameManager.MatchState.Paused)
            {
                GameManager.Instance.ResumeMatch();
            }
            else if (GameManager.Instance.CurrentState == GameManager.MatchState.Playing)
            {
                GameManager.Instance.PauseMatch();
            }
        }
    }

    /// <summary>
    /// Public method to resume match (called from pause menu button)
    /// </summary>
    public void ResumeMatch()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResumeMatch();
        }
    }

    /// <summary>
    /// Public method to restart match (called from pause menu button)
    /// </summary>
    public void RestartMatch()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartMatch();
        }
    }
}

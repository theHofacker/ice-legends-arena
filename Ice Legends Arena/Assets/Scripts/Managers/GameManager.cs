using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton GameManager that handles match flow, scoring, timer, and game state.
/// Controls the entire match from face-off to final whistle.
/// 100% transferable to 3D - pure game logic!
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Don't destroy on load if you want match to persist across scenes
        // DontDestroyOnLoad(gameObject);
    }
    #endregion

    [Header("Match Settings")]
    [Tooltip("Match duration in seconds (300 = 5:00)")]
    [SerializeField] private float matchDuration = 300f; // 5 minutes

    [Tooltip("Enable overtime if tied at end of regulation")]
    [SerializeField] private bool enableOvertime = false;

    [Tooltip("Overtime duration in seconds (60 = 1:00 sudden death)")]
    [SerializeField] private float overtimeDuration = 60f;

    [Header("Team Names")]
    [SerializeField] private string playerTeamName = "Player Team";
    [SerializeField] private string opponentTeamName = "Opponent Team";

    [Header("Face-Off Settings")]
    [Tooltip("Center ice position for face-offs")]
    [SerializeField] private Vector2 centerIcePosition = Vector2.zero;

    [Tooltip("Delay before face-off (seconds)")]
    [Range(1f, 5f)]
    [SerializeField] private float faceOffDelay = 2f;

    // Match state
    public enum MatchState
    {
        WaitingForFaceOff,  // Before puck drop
        Playing,            // Active gameplay
        GoalScored,         // Goal celebration
        Paused,             // Game paused
        MatchEnded          // Game over
    }

    [Header("Current Match State")]
    [SerializeField] private MatchState currentState = MatchState.WaitingForFaceOff;

    // Score tracking
    private int playerScore = 0;
    private int opponentScore = 0;

    // Timer
    private float timeRemaining;
    private bool isOvertime = false;

    // References
    private GameObject puck;
    private Transform playerTeam;
    private Transform opponentTeam;

    // Events (for UI updates)
    public delegate void ScoreChangedDelegate(int playerScore, int opponentScore);
    public event ScoreChangedDelegate OnScoreChanged;

    public delegate void TimerChangedDelegate(float timeRemaining);
    public event TimerChangedDelegate OnTimerChanged;

    public delegate void MatchStateChangedDelegate(MatchState newState);
    public event MatchStateChangedDelegate OnMatchStateChanged;

    public delegate void GoalScoredDelegate(bool scoredByPlayer, int playerScore, int opponentScore);
    public event GoalScoredDelegate OnGoalScored;

    // Public accessors
    public MatchState CurrentState => currentState;
    public int PlayerScore => playerScore;
    public int OpponentScore => opponentScore;
    public float TimeRemaining => timeRemaining;
    public bool IsOvertime => isOvertime;

    private void Start()
    {
        // Find puck
        GameObject puckObj = GameObject.FindGameObjectWithTag("Puck");
        if (puckObj != null)
        {
            puck = puckObj;
        }
        else
        {
            Debug.LogError("GameManager: No puck found! Make sure puck has 'Puck' tag.");
        }

        // Initialize timer
        timeRemaining = matchDuration;

        // Start match with face-off
        StartMatch();
    }

    private void Update()
    {
        // Update timer when playing
        if (currentState == MatchState.Playing)
        {
            UpdateMatchTimer();
        }
    }

    /// <summary>
    /// Start the match with initial face-off
    /// </summary>
    public void StartMatch()
    {
        Debug.Log("=== MATCH STARTING ===");
        Debug.Log($"{playerTeamName} vs {opponentTeamName}");
        Debug.Log($"Match Duration: {FormatTime(matchDuration)}");

        // Reset scores
        playerScore = 0;
        opponentScore = 0;
        OnScoreChanged?.Invoke(playerScore, opponentScore);

        // Start with face-off
        StartFaceOff();
    }

    /// <summary>
    /// Update match timer and check for time expiration
    /// </summary>
    private void UpdateMatchTimer()
    {
        timeRemaining -= Time.deltaTime;

        // Notify listeners (UI) of timer change
        OnTimerChanged?.Invoke(timeRemaining);

        // Check if time expired
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            OnTimeExpired();
        }
    }

    /// <summary>
    /// Handle time expiration (end of regulation or overtime)
    /// </summary>
    private void OnTimeExpired()
    {
        Debug.Log("=== TIME EXPIRED ===");

        // Check if tied
        if (playerScore == opponentScore)
        {
            if (enableOvertime && !isOvertime)
            {
                // Start overtime
                StartOvertime();
            }
            else
            {
                // Match ends in tie (or overtime already played)
                EndMatch(false, true); // isTie = true
            }
        }
        else
        {
            // Someone won in regulation
            EndMatch(playerScore > opponentScore, false);
        }
    }

    /// <summary>
    /// Start overtime period
    /// </summary>
    private void StartOvertime()
    {
        Debug.Log("=== OVERTIME - SUDDEN DEATH ===");
        isOvertime = true;
        timeRemaining = overtimeDuration;

        // Face-off to start overtime
        StartFaceOff();
    }

    /// <summary>
    /// Goal scored! Handle scoring logic.
    /// </summary>
    /// <param name="scoredByPlayer">True if player team scored, false if opponent scored</param>
    public void GoalScored(bool scoredByPlayer)
    {
        // Update score
        if (scoredByPlayer)
        {
            playerScore++;
            Debug.Log($"🚨 GOAL! {playerTeamName} scores! ({playerScore}-{opponentScore})");
        }
        else
        {
            opponentScore++;
            Debug.Log($"🚨 GOAL! {opponentTeamName} scores! ({playerScore}-{opponentScore})");
        }

        // Notify listeners
        OnScoreChanged?.Invoke(playerScore, opponentScore);
        OnGoalScored?.Invoke(scoredByPlayer, playerScore, opponentScore);

        // Change state to goal celebration
        ChangeState(MatchState.GoalScored);

        // Check for overtime sudden death win
        if (isOvertime)
        {
            // Overtime goal = instant win
            Debug.Log("=== OVERTIME GOAL - GAME OVER ===");
            EndMatch(scoredByPlayer, false);
        }
        else
        {
            // Regular goal - reset with face-off after delay
            Invoke(nameof(StartFaceOff), faceOffDelay);
        }
    }

    /// <summary>
    /// Start face-off at center ice
    /// </summary>
    public void StartFaceOff()
    {
        Debug.Log("Face-off at center ice...");

        ChangeState(MatchState.WaitingForFaceOff);

        // Move puck to center ice
        if (puck != null)
        {
            puck.transform.position = centerIcePosition;
            Rigidbody2D puckRb = puck.GetComponent<Rigidbody2D>();
            if (puckRb != null)
            {
                puckRb.linearVelocity = Vector2.zero;
            }
        }

        // Position all players for face-off
        PositionPlayersForFaceOff();

        // Drop the puck and start play after delay
        Invoke(nameof(DropPuck), faceOffDelay);
    }

    /// <summary>
    /// Position all players (teammates and opponents) for face-off
    /// </summary>
    private void PositionPlayersForFaceOff()
    {
        // Position player team (using TeammateController components)
        FormationManager playerFormation = FormationManager.GetFormationManager(FormationManager.Team.Player);
        if (playerFormation != null)
        {
            PositionPlayerTeamForFaceOff(playerFormation);
        }

        // Position opponent team (using AIController components)
        FormationManager opponentFormation = FormationManager.GetFormationManager(FormationManager.Team.Opponent);
        if (opponentFormation != null)
        {
            PositionOpponentTeamForFaceOff(opponentFormation);
        }
    }

    /// <summary>
    /// Position player team for face-off (teammates + controlled player)
    /// </summary>
    private void PositionPlayerTeamForFaceOff(FormationManager formation)
    {
        // Find all teammates (including the one player is currently controlling)
        TeammateController[] teammates = FindObjectsOfType<TeammateController>();

        foreach (TeammateController teammate in teammates)
        {
            // Get player role
            FormationManager.PlayerRole role = GetPlayerRole(teammate.gameObject);

            // Get face-off position from formation manager
            Vector2 faceOffPosition = formation.GetFaceOffPosition(role, centerIcePosition);

            // Move player to face-off position
            teammate.transform.position = faceOffPosition;

            // Stop player movement
            Rigidbody2D playerRb = teammate.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
            }
        }

        Debug.Log("Player team positioned for face-off");
    }

    /// <summary>
    /// Position opponent team for face-off
    /// </summary>
    private void PositionOpponentTeamForFaceOff(FormationManager formation)
    {
        // Find all AI opponents
        AIController[] opponents = FindObjectsOfType<AIController>();

        foreach (AIController opponent in opponents)
        {
            // Get player role
            FormationManager.PlayerRole role = GetPlayerRole(opponent.gameObject);

            // Get face-off position from formation manager
            Vector2 faceOffPosition = formation.GetFaceOffPosition(role, centerIcePosition);

            // Move opponent to face-off position
            opponent.transform.position = faceOffPosition;

            // Stop opponent movement
            Rigidbody2D opponentRb = opponent.GetComponent<Rigidbody2D>();
            if (opponentRb != null)
            {
                opponentRb.linearVelocity = Vector2.zero;
            }
        }

        Debug.Log("Opponent team positioned for face-off");
    }

    /// <summary>
    /// Determine player role from GameObject name or components
    /// </summary>
    private FormationManager.PlayerRole GetPlayerRole(GameObject player)
    {
        string playerName = player.name.ToLower();

        if (playerName.Contains("center")) return FormationManager.PlayerRole.Center;
        if (playerName.Contains("leftwing")) return FormationManager.PlayerRole.LeftWing;
        if (playerName.Contains("rightwing")) return FormationManager.PlayerRole.RightWing;
        if (playerName.Contains("leftdefense")) return FormationManager.PlayerRole.LeftDefense;
        if (playerName.Contains("rightdefense")) return FormationManager.PlayerRole.RightDefense;

        // Default to center if unknown
        return FormationManager.PlayerRole.Center;
    }

    /// <summary>
    /// Drop the puck and resume play
    /// </summary>
    private void DropPuck()
    {
        Debug.Log("Puck drop! Play begins!");
        ChangeState(MatchState.Playing);

        // Optional: Add small upward impulse to simulate puck drop
        if (puck != null)
        {
            Rigidbody2D puckRb = puck.GetComponent<Rigidbody2D>();
            if (puckRb != null)
            {
                // Small random velocity for realistic puck drop
                Vector2 randomDrop = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                puckRb.linearVelocity = randomDrop;
            }
        }
    }

    /// <summary>
    /// Pause the match
    /// </summary>
    public void PauseMatch()
    {
        if (currentState == MatchState.Playing)
        {
            ChangeState(MatchState.Paused);
            Time.timeScale = 0f; // Pause game time
            Debug.Log("Match paused");
        }
    }

    /// <summary>
    /// Resume the match
    /// </summary>
    public void ResumeMatch()
    {
        if (currentState == MatchState.Paused)
        {
            ChangeState(MatchState.Playing);
            Time.timeScale = 1f; // Resume game time
            Debug.Log("Match resumed");
        }
    }

    /// <summary>
    /// End the match and show results
    /// </summary>
    /// <param name="playerWon">Did player team win?</param>
    /// <param name="isTie">Is it a tie?</param>
    private void EndMatch(bool playerWon, bool isTie)
    {
        ChangeState(MatchState.MatchEnded);

        Debug.Log("=== MATCH ENDED ===");
        Debug.Log($"Final Score: {playerTeamName} {playerScore} - {opponentScore} {opponentTeamName}");

        if (isTie)
        {
            Debug.Log("Result: TIE");
        }
        else if (playerWon)
        {
            Debug.Log($"Result: {playerTeamName} WINS! 🏆");
        }
        else
        {
            Debug.Log($"Result: {opponentTeamName} WINS");
        }

        // TODO: Show victory/defeat screen (UI)
        // For now, just log the results
    }

    /// <summary>
    /// Restart the match (reload scene or reset)
    /// </summary>
    public void RestartMatch()
    {
        Time.timeScale = 1f; // Ensure time is running
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Return to main menu
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // Ensure time is running
        // TODO: Load main menu scene when implemented
        Debug.Log("Return to main menu (not yet implemented)");
    }

    /// <summary>
    /// Change match state and notify listeners
    /// </summary>
    private void ChangeState(MatchState newState)
    {
        if (currentState != newState)
        {
            MatchState oldState = currentState;
            currentState = newState;

            Debug.Log($"Match state: {oldState} → {newState}");
            OnMatchStateChanged?.Invoke(newState);
        }
    }

    /// <summary>
    /// Format time as MM:SS
    /// </summary>
    public static string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes}:{seconds:00}";
    }

    /// <summary>
    /// Get formatted time remaining
    /// </summary>
    public string GetFormattedTimeRemaining()
    {
        return FormatTime(timeRemaining);
    }
}

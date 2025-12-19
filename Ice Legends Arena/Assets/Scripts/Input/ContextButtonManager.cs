using UnityEngine;

/// <summary>
/// Manages the three context-sensitive buttons on the right side of the screen.
/// Switches button actions based on whether the player has possession of the puck.
/// </summary>
public class ContextButtonManager : MonoBehaviour
{
    public static ContextButtonManager Instance { get; private set; }

    [Header("Button References")]
    [SerializeField] private ContextButton button1; // Right-most button
    [SerializeField] private ContextButton button2; // Middle button
    [SerializeField] private ContextButton button3; // Left-most button

    [Header("Possession Detection")]
    [SerializeField] private float possessionCheckRadius = 1.5f; // How close player must be to puck to "have" it
    [SerializeField] private LayerMask puckLayer;

    // State tracking
    public enum PossessionState
    {
        Offense,  // Player has puck
        Defense   // Player does not have puck
    }

    public PossessionState CurrentState { get; private set; } = PossessionState.Defense;

    // References
    private Transform playerTransform;
    private Transform puckTransform;

    // Public API for other systems
    public bool HasPuck { get; private set; } = false;

    // Events for action systems to subscribe to
    public System.Action<bool> OnShootRequested; // Parameter: isCharged (true for slapshot, false for wrist shot)

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
        // Find player and puck
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("ContextButtonManager: No Player found! Tag your player with 'Player' tag.");
        }

        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
        }
        else
        {
            Debug.LogError("ContextButtonManager: No Puck found! Tag your puck with 'Puck' tag.");
        }

        // Subscribe to button events
        if (button1 != null)
            button1.OnButtonActivated += HandleButton1Activated;
        if (button2 != null)
            button2.OnButtonActivated += HandleButton2Activated;
        if (button3 != null)
            button3.OnButtonActivated += HandleButton3Activated;

        // Set initial state
        UpdateButtonContext();
    }

    private void Update()
    {
        // Check possession state every frame
        CheckPossessionState();
    }

    private void CheckPossessionState()
    {
        if (playerTransform == null || puckTransform == null)
        {
            HasPuck = false;
            return;
        }

        // Check distance between player and puck
        float distance = Vector2.Distance(playerTransform.position, puckTransform.position);
        HasPuck = distance <= possessionCheckRadius;

        // Update button context if state changed
        PossessionState newState = HasPuck ? PossessionState.Offense : PossessionState.Defense;
        if (newState != CurrentState)
        {
            CurrentState = newState;
            UpdateButtonContext();
            Debug.Log($"Possession state changed to: {CurrentState}");
        }
    }

    private void UpdateButtonContext()
    {
        if (CurrentState == PossessionState.Offense)
        {
            // Player has puck - offensive actions
            button1?.SetAction(ContextButton.ButtonAction.Shoot);
            button2?.SetAction(ContextButton.ButtonAction.Pass);
            button3?.SetAction(ContextButton.ButtonAction.Deke);
        }
        else
        {
            // Player does not have puck - defensive actions
            button1?.SetAction(ContextButton.ButtonAction.Check);
            button2?.SetAction(ContextButton.ButtonAction.Switch);
            button3?.SetAction(ContextButton.ButtonAction.Defend);
        }
    }

    // Button activation handlers
    private void HandleButton1Activated(ContextButton.ButtonAction action, ContextButton.GestureType gesture)
    {
        Debug.Log($"Button 1 activated: {action} ({gesture})");

        switch (action)
        {
            case ContextButton.ButtonAction.Shoot:
                HandleShootAction(gesture);
                break;
            case ContextButton.ButtonAction.Check:
                HandleCheckAction(gesture);
                break;
        }
    }

    private void HandleButton2Activated(ContextButton.ButtonAction action, ContextButton.GestureType gesture)
    {
        Debug.Log($"Button 2 activated: {action} ({gesture})");

        switch (action)
        {
            case ContextButton.ButtonAction.Pass:
                HandlePassAction(gesture);
                break;
            case ContextButton.ButtonAction.Switch:
                HandleSwitchAction(gesture);
                break;
        }
    }

    private void HandleButton3Activated(ContextButton.ButtonAction action, ContextButton.GestureType gesture)
    {
        Debug.Log($"Button 3 activated: {action} ({gesture})");

        switch (action)
        {
            case ContextButton.ButtonAction.Deke:
                HandleDekeAction(gesture);
                break;
            case ContextButton.ButtonAction.Defend:
                HandleDefendAction(gesture);
                break;
        }
    }

    // Action implementations (to be filled in by specific systems)
    private void HandleShootAction(ContextButton.GestureType gesture)
    {
        if (gesture == ContextButton.GestureType.Tap)
        {
            Debug.Log("→ Wrist shot (tap)");
            OnShootRequested?.Invoke(false); // false = not charged
        }
        else if (gesture == ContextButton.GestureType.Hold)
        {
            Debug.Log("→ Slapshot (hold)");
            OnShootRequested?.Invoke(true); // true = charged/slapshot
        }
    }

    private void HandlePassAction(ContextButton.GestureType gesture)
    {
        // Will be implemented in Sprint 3 (Passing System)
        if (gesture == ContextButton.GestureType.Tap)
        {
            Debug.Log("→ Basic pass");
        }
        else if (gesture == ContextButton.GestureType.Hold)
        {
            Debug.Log("→ Saucer pass");
        }
        else if (gesture == ContextButton.GestureType.SwipeOff)
        {
            Debug.Log("→ Fake pass!");
        }
    }

    private void HandleDekeAction(ContextButton.GestureType gesture)
    {
        // Future implementation
        Debug.Log("→ Deke move");
    }

    private void HandleCheckAction(ContextButton.GestureType gesture)
    {
        // Will be implemented in Sprint 4 (Checking System)
        if (gesture == ContextButton.GestureType.Tap)
        {
            Debug.Log("→ Poke check");
        }
        else if (gesture == ContextButton.GestureType.Hold)
        {
            Debug.Log("→ Body check charging");
        }
    }

    private void HandleSwitchAction(ContextButton.GestureType gesture)
    {
        // Will be implemented in Sprint 4
        Debug.Log("→ Switch player");
    }

    private void HandleDefendAction(ContextButton.GestureType gesture)
    {
        // Future implementation
        Debug.Log("→ Defensive stance");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (button1 != null)
            button1.OnButtonActivated -= HandleButton1Activated;
        if (button2 != null)
            button2.OnButtonActivated -= HandleButton2Activated;
        if (button3 != null)
            button3.OnButtonActivated -= HandleButton3Activated;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Visualization for debugging
    private void OnDrawGizmosSelected()
    {
        if (playerTransform != null)
        {
            Gizmos.color = HasPuck ? Color.green : Color.red;
            Gizmos.DrawWireSphere(playerTransform.position, possessionCheckRadius);
        }
    }
}

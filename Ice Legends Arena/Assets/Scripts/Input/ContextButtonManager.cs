using UnityEngine;
using UnityEngine.InputSystem;

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
    public System.Action OnShotChargeStarted; // Fired when player starts holding SHOOT button
    public System.Action OnShotChargeEnded; // Fired when player releases SHOOT button
    public System.Action<bool> OnPassRequested; // Parameter: isCharged (true for saucer pass, false for basic pass)
    public System.Action OnPassChargeStarted; // Fired when player starts holding PASS button
    public System.Action OnPassChargeEnded; // Fired when player releases PASS button
    public System.Action OnFakePassRequested; // Fired when player swipes off PASS button (fake pass)
    public System.Action<bool> OnCheckRequested; // Parameter: isCharged (true for body check, false for poke check)
    public System.Action OnCheckChargeStarted; // Fired when player starts holding CHECK button
    public System.Action OnCheckChargeEnded; // Fired when player releases CHECK button
    public System.Action OnFakeCheckRequested; // Fired when player swipes off CHECK button (fake check)
    public System.Action<bool> OnSwitchRequested; // Parameter: isHeld (true for hold = last defender, false for tap = nearest to puck)

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

        // Track shot charging for perfect timing system
        TrackShotCharging();

        // Track pass charging for saucer pass timing system
        TrackPassCharging();

        // Track check charging for body check timing system
        TrackCheckCharging();
    }

    private void TrackShotCharging()
    {
        // Check if SHOOT is being held (via button or keyboard)
        if (CurrentState == PossessionState.Offense)
        {
            bool isHoldingShoot = false;

            // Check button input
            if (button1 != null)
            {
                isHoldingShoot = button1.IsHeld();
            }

            // Also check keyboard input (Space or Enter specifically)
            // NOTE: We check specific keys instead of Attack/Jump actions because those
            // actions also have mouse/touch bindings that conflict with the virtual joystick
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                isHoldingShoot = isHoldingShoot ||
                               keyboard.spaceKey.isPressed ||
                               keyboard.enterKey.isPressed;
            }

            // Start charging when hold begins
            if (isHoldingShoot && !isShotCharging)
            {
                isShotCharging = true;
                OnShotChargeStarted?.Invoke();
                Debug.Log("Shot charging started!");
            }
            // Stop charging when released
            else if (!isHoldingShoot && isShotCharging)
            {
                isShotCharging = false;
                OnShotChargeEnded?.Invoke();
                Debug.Log("Shot charging ended!");
            }
        }
    }

    private void TrackPassCharging()
    {
        // Check if PASS is being held (button2)
        if (CurrentState == PossessionState.Offense)
        {
            bool isHoldingPass = false;

            // Check button input
            if (button2 != null)
            {
                isHoldingPass = button2.IsHeld();
            }

            // Start charging when hold begins
            if (isHoldingPass && !isPassCharging)
            {
                isPassCharging = true;
                OnPassChargeStarted?.Invoke();
                Debug.Log("Pass charging started!");
            }
            // Stop charging when released
            else if (!isHoldingPass && isPassCharging)
            {
                isPassCharging = false;
                OnPassChargeEnded?.Invoke();
                Debug.Log("Pass charging ended!");
            }
        }
    }

    private void TrackCheckCharging()
    {
        // Check if CHECK is being held (button1 in Defense mode)
        if (CurrentState == PossessionState.Defense)
        {
            bool isHoldingCheck = false;

            // Check button input
            if (button1 != null)
            {
                isHoldingCheck = button1.IsHeld();
            }

            // Start charging when hold begins
            if (isHoldingCheck && !isCheckCharging)
            {
                isCheckCharging = true;
                OnCheckChargeStarted?.Invoke();
                Debug.Log("Body check charging started!");
            }
            // Stop charging when released
            else if (!isHoldingCheck && isCheckCharging)
            {
                isCheckCharging = false;
                OnCheckChargeEnded?.Invoke();
                Debug.Log("Body check charging ended!");
            }
        }
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

    // State for tracking shot charging
    private bool isShotCharging = false;

    // State for tracking pass charging
    private bool isPassCharging = false;

    // State for tracking check charging
    private bool isCheckCharging = false;

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
        if (gesture == ContextButton.GestureType.Tap)
        {
            Debug.Log("→ Basic pass");
            OnPassRequested?.Invoke(false); // false = basic pass
        }
        else if (gesture == ContextButton.GestureType.Hold)
        {
            Debug.Log("→ Saucer pass (hold)");
            OnPassRequested?.Invoke(true); // true = saucer pass (charged)
        }
        else if (gesture == ContextButton.GestureType.SwipeOff)
        {
            Debug.Log("→ Fake pass!");
            OnFakePassRequested?.Invoke();
        }
    }

    private void HandleDekeAction(ContextButton.GestureType gesture)
    {
        // Future implementation
        Debug.Log("→ Deke move");
    }

    private void HandleCheckAction(ContextButton.GestureType gesture)
    {
        if (gesture == ContextButton.GestureType.Tap)
        {
            Debug.Log("→ Poke check");
            OnCheckRequested?.Invoke(false); // false = poke check (tap)
        }
        else if (gesture == ContextButton.GestureType.Hold)
        {
            Debug.Log("→ Body check charging");
            OnCheckRequested?.Invoke(true); // true = body check (hold)
        }
        else if (gesture == ContextButton.GestureType.SwipeOff)
        {
            Debug.Log("→ Fake check!");
            OnFakeCheckRequested?.Invoke();
        }
    }

    private void HandleSwitchAction(ContextButton.GestureType gesture)
    {
        if (gesture == ContextButton.GestureType.Tap)
        {
            Debug.Log("→ Switch to nearest player (tap)");
            OnSwitchRequested?.Invoke(false); // false = tap (nearest to puck)
        }
        else if (gesture == ContextButton.GestureType.Hold)
        {
            Debug.Log("→ Switch to last defender (hold)");
            OnSwitchRequested?.Invoke(true); // true = hold (last defender)
        }
    }

    private void HandleDefendAction(ContextButton.GestureType gesture)
    {
        // Future implementation
        Debug.Log("→ Defensive stance");
    }

    /// <summary>
    /// Update button1 color for timing meter feedback (called by CheckingController)
    /// </summary>
    public void UpdateButton1Color(Color color)
    {
        if (button1 != null)
        {
            button1.UpdateButtonColor(color);
        }
    }

    /// <summary>
    /// Reset button1 to normal color
    /// </summary>
    public void ResetButton1Color()
    {
        if (button1 != null)
        {
            button1.ResetColor();
        }
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

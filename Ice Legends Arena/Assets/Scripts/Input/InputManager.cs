using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private VirtualJoystick virtualJoystick;

    [Header("Input Actions Asset")]
    [SerializeField] private InputActionAsset inputActionsAsset;

    private InputSystem_Actions inputActions;

    // Public API for accessing input
    public Vector2 MoveInput { get; private set; }
    /// <summary>Joystick input mapped to 3D world space (X,0,Z)</summary>
    public Vector3 MoveInputWorld => PhysicsHelper.InputToWorld(MoveInput);

    /// <summary>
    /// A keyboard-key-equivalent fed by on-screen UI buttons (<see cref="TouchActionButton"/>). The
    /// controllers OR these with their raw <c>Keyboard.current</c> reads, so an action works from either
    /// input. <see cref="Down"/>/<see cref="Up"/> are frame-accurate (true only the frame of press/release)
    /// and cleared in <see cref="InputManager.LateUpdate"/> — set during the Update phase (pointer events),
    /// read by controllers in Update, cleared after, so the read ordering can't drop an event.
    /// </summary>
    public class VirtualButton
    {
        public bool Down { get; private set; }  // pressed THIS frame
        public bool Up { get; private set; }    // released THIS frame
        public bool Held { get; private set; }

        public void Press() { Down = true; Held = true; }
        public void Release() { Up = true; Held = false; }
        public void ClearFrame() { Down = false; Up = false; }
    }

    // Touch/UI action buttons. Movement stays on the analog joystick path above; these cover the
    // discrete actions the Test controllers otherwise poll straight off the keyboard.
    public VirtualButton Shoot { get; } = new VirtualButton();
    public VirtualButton Pass { get; } = new VirtualButton();
    public VirtualButton Check { get; } = new VirtualButton();
    public VirtualButton Ability1 { get; } = new VirtualButton();
    public VirtualButton Ability2 { get; } = new VirtualButton();
    public VirtualButton Switch { get; } = new VirtualButton();

    /// <summary>Resolve the virtual button a <see cref="TouchActionButton"/> drives.</summary>
    public VirtualButton GetButton(TouchActionButton.Action action)
    {
        switch (action)
        {
            case TouchActionButton.Action.Shoot: return Shoot;
            case TouchActionButton.Action.Pass: return Pass;
            case TouchActionButton.Action.Check: return Check;
            case TouchActionButton.Action.Ability1: return Ability1;
            case TouchActionButton.Action.Ability2: return Ability2;
            case TouchActionButton.Action.Switch: return Switch;
            default: return Shoot;
        }
    }

    /// <summary>Ability slot lookup for <see cref="TestAbilityController"/> (None → null).</summary>
    public VirtualButton GetAbility(TestAbilityController.TouchSlot slot)
    {
        switch (slot)
        {
            case TestAbilityController.TouchSlot.Ability1: return Ability1;
            case TestAbilityController.TouchSlot.Ability2: return Ability2;
            default: return null;
        }
    }
    public bool IsJumpPressed => inputActions != null && inputActions.Player.Jump.triggered;
    public bool IsJumpHeld => inputActions != null && inputActions.Player.Jump.IsPressed();
    public bool IsAttackPressed => inputActions != null && inputActions.Player.Attack.triggered;
    public bool IsAttackHeld => inputActions != null && inputActions.Player.Attack.IsPressed();
    public bool IsSprintPressed => inputActions != null && inputActions.Player.Sprint.triggered;
    public bool IsSprintHeld => inputActions != null && inputActions.Player.Sprint.IsPressed();

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize Input System
        if (inputActionsAsset != null)
        {
            inputActions = new InputSystem_Actions();
            inputActions.Enable();
        }
        else
        {
            Debug.LogError("InputManager: InputSystem_Actions asset is not assigned!");
        }

        // Platform-specific setup
        SetupPlatformControls();
    }

    private void OnEnable()
    {
        inputActions?.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void Update()
    {
        AggregateInput();
    }

    // Clear the one-frame Down/Up flags AFTER every controller's Update has had a chance to read them.
    // LateUpdate is guaranteed to run after all Updates, so a button press set during the Update phase is
    // never cleared before its consumer sees it (regardless of script execution order).
    private void LateUpdate()
    {
        Shoot.ClearFrame();
        Pass.ClearFrame();
        Check.ClearFrame();
        Ability1.ClearFrame();
        Ability2.ClearFrame();
        Switch.ClearFrame();
    }

    private void AggregateInput()
    {
        if (inputActions == null) return;

        // Get input from Input System (keyboard, gamepad, etc.)
        Vector2 inputSystemMove = inputActions.Player.Move.ReadValue<Vector2>();

        // Get input from Virtual Joystick (if enabled)
        Vector2 virtualMove = Vector2.zero;
        if (virtualJoystick != null && virtualJoystick.gameObject.activeSelf)
        {
            virtualMove = virtualJoystick.InputVector;
        }

        // Priority: Virtual joystick overrides other input when actively being used
        // Check magnitude threshold to determine if joystick is actively being touched
        MoveInput = virtualMove.magnitude > 0.1f ? virtualMove : inputSystemMove;
    }

    private void SetupPlatformControls()
    {
        // Determine if we're on a mobile platform
        bool isMobile = Application.platform == RuntimePlatform.Android ||
                        Application.platform == RuntimePlatform.IPhonePlayer;

#if UNITY_EDITOR
        // In editor, enable virtual controls for testing
        isMobile = true;
#endif

        // Enable or disable virtual joystick based on platform
        if (virtualJoystick != null)
        {
            virtualJoystick.gameObject.SetActive(isMobile);

            if (isMobile)
            {
                Debug.Log("InputManager: Virtual controls enabled for mobile platform");
            }
            else
            {
                Debug.Log("InputManager: Virtual controls disabled (using keyboard/gamepad)");
            }
        }
    }

    // Optional: Method to manually toggle virtual controls (useful for testing)
    public void SetVirtualControlsActive(bool active)
    {
        if (virtualJoystick != null)
        {
            virtualJoystick.gameObject.SetActive(active);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        inputActions?.Disable();
    }
}

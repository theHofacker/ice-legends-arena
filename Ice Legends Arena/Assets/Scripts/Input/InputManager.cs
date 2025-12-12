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

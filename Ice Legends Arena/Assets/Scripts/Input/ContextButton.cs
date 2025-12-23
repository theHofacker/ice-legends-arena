using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// A single context-sensitive button that changes its action based on game state.
/// Supports tap, hold, and swipe-off gestures.
/// </summary>
public class ContextButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private TextMeshProUGUI buttonLabel;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color pressedColor = Color.gray;
    [SerializeField] private Color highlightColor = Color.yellow;

    [Header("Hold Detection")]
    [SerializeField] private float holdThreshold = 0.3f; // Time to trigger hold

    // Current button state
    public enum ButtonAction
    {
        // Offense actions
        Shoot,
        Pass,
        Deke,

        // Defense actions
        Check,
        Switch,
        Defend
    }

    // Input gesture types
    public enum GestureType
    {
        None,
        Tap,        // Quick press and release
        Hold,       // Press and hold
        SwipeOff    // Press, drag off button, release
    }

    // Current configuration
    public ButtonAction CurrentAction { get; private set; }
    public GestureType LastGesture { get; private set; }

    // State tracking
    private bool isPressed = false;
    private float pressStartTime;
    private bool hasHoldTriggered = false;
    private bool wasSwipedOff = false;

    // Events
    public System.Action<ButtonAction, GestureType> OnButtonActivated;

    private void Start()
    {
        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        if (buttonLabel == null)
            buttonLabel = GetComponentInChildren<TextMeshProUGUI>();

        SetVisualState(normalColor);
    }

    private void Update()
    {
        // Check for hold gesture
        if (isPressed && !hasHoldTriggered)
        {
            float pressDuration = Time.time - pressStartTime;
            if (pressDuration >= holdThreshold)
            {
                hasHoldTriggered = true;
                OnHoldActivated();
            }
        }
    }

    /// <summary>
    /// Configure this button for a specific action
    /// </summary>
    public void SetAction(ButtonAction action)
    {
        CurrentAction = action;

        // Update button label based on action
        if (buttonLabel != null)
        {
            buttonLabel.text = GetActionLabel(action);
        }
    }

    private string GetActionLabel(ButtonAction action)
    {
        switch (action)
        {
            case ButtonAction.Shoot: return "SHOOT";
            case ButtonAction.Pass: return "PASS";
            case ButtonAction.Deke: return "DEKE";
            case ButtonAction.Check: return "CHECK";
            case ButtonAction.Switch: return "SWITCH";
            case ButtonAction.Defend: return "DEFEND";
            default: return "";
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        pressStartTime = Time.time;
        hasHoldTriggered = false;
        wasSwipedOff = false;

        SetVisualState(pressedColor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed) return;

        isPressed = false;
        SetVisualState(normalColor);

        // Determine gesture type and trigger action
        if (wasSwipedOff)
        {
            // Swipe-off gesture (fake)
            LastGesture = GestureType.SwipeOff;
            OnButtonActivated?.Invoke(CurrentAction, GestureType.SwipeOff);
        }
        else if (hasHoldTriggered)
        {
            // Hold was already triggered in Update(), this is the release
            LastGesture = GestureType.Hold;
            // Note: Hold activation happens in OnHoldActivated(),
            // but we could add a "HoldRelease" event here if needed
        }
        else
        {
            // Quick tap
            float pressDuration = Time.time - pressStartTime;
            if (pressDuration < holdThreshold)
            {
                LastGesture = GestureType.Tap;
                OnButtonActivated?.Invoke(CurrentAction, GestureType.Tap);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // User dragged finger off button while still holding
        if (isPressed)
        {
            wasSwipedOff = true;
            SetVisualState(normalColor);
        }
    }

    private void OnHoldActivated()
    {
        // Visual feedback for hold
        SetVisualState(highlightColor);

        // Trigger hold action
        OnButtonActivated?.Invoke(CurrentAction, GestureType.Hold);
    }

    private void SetVisualState(Color color)
    {
        if (buttonImage != null)
        {
            buttonImage.color = color;
        }
    }

    /// <summary>
    /// Check if button is currently being held
    /// </summary>
    public bool IsHeld()
    {
        return isPressed && hasHoldTriggered;
    }

    /// <summary>
    /// Get current hold duration
    /// </summary>
    public float GetHoldDuration()
    {
        if (!isPressed) return 0f;
        return Time.time - pressStartTime;
    }

    /// <summary>
    /// Public method to update button color (for timing meter feedback)
    /// </summary>
    public void UpdateButtonColor(Color color)
    {
        SetVisualState(color);
    }

    /// <summary>
    /// Reset button to normal color
    /// </summary>
    public void ResetColor()
    {
        SetVisualState(normalColor);
    }
}

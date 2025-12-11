using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    [Header("Settings")]
    [SerializeField] private float handleRange = 50f;        // Max distance handle can move from center
    [SerializeField] private float deadZone = 0.1f;          // Ignore inputs below this magnitude
    [SerializeField] private bool returnToCenter = true;     // Should handle return to center on release?
    [SerializeField] private float returnSpeed = 10f;        // Speed of return animation

    [Header("Visual Feedback")]
    [SerializeField] private float activeAlpha = 1f;         // Alpha when joystick is active
    [SerializeField] private float inactiveAlpha = 0.5f;     // Alpha when joystick is inactive

    // Output - normalized direction vector (-1 to 1)
    public Vector2 InputVector { get; private set; }

    // Internal state
    private bool isDragging = false;
    private Image backgroundImage;
    private Image handleImage;
    private Vector2 initialHandlePosition;

    private void Start()
    {
        // Cache image components for visual feedback
        backgroundImage = background?.GetComponent<Image>();
        handleImage = handle?.GetComponent<Image>();

        // Store initial handle position
        if (handle != null)
        {
            initialHandlePosition = handle.anchoredPosition;
        }

        // Set initial visual state
        SetVisualState(false);
    }

    private void Update()
    {
        // Return handle to center when not dragging
        if (!isDragging && returnToCenter && handle != null)
        {
            // Smoothly lerp handle back to center
            if (handle.anchoredPosition.magnitude > 0.01f)
            {
                handle.anchoredPosition = Vector2.Lerp(
                    handle.anchoredPosition,
                    initialHandlePosition,
                    returnSpeed * Time.deltaTime
                );
            }
            else
            {
                handle.anchoredPosition = initialHandlePosition;
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        OnDrag(eventData);  // Process initial touch position immediately
        SetVisualState(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || background == null || handle == null)
            return;

        // Convert screen point to local point in background RectTransform
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        // Clamp the point to the maximum handle range
        Vector2 clampedPoint = Vector2.ClampMagnitude(localPoint, handleRange);

        // Update handle position
        handle.anchoredPosition = clampedPoint;

        // Calculate normalized input vector (-1 to 1)
        InputVector = clampedPoint / handleRange;

        // Apply dead zone
        if (InputVector.magnitude < deadZone)
        {
            InputVector = Vector2.zero;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        InputVector = Vector2.zero;

        // If not using smooth return, snap to center immediately
        if (!returnToCenter && handle != null)
        {
            handle.anchoredPosition = initialHandlePosition;
        }

        SetVisualState(false);
    }

    private void SetVisualState(bool active)
    {
        float targetAlpha = active ? activeAlpha : inactiveAlpha;

        if (backgroundImage != null)
        {
            Color bgColor = backgroundImage.color;
            bgColor.a = targetAlpha;
            backgroundImage.color = bgColor;
        }

        if (handleImage != null)
        {
            Color handleColor = handleImage.color;
            handleColor.a = targetAlpha;
            handleImage.color = handleColor;
        }
    }

    // Optional: Draw gizmos in editor to visualize handle range
    private void OnDrawGizmosSelected()
    {
        if (background != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(background.position, handleRange);
        }
    }

    // Optional: Validate references in editor
    private void OnValidate()
    {
        if (background == null)
        {
            Debug.LogWarning("VirtualJoystick: Background RectTransform not assigned!", this);
        }

        if (handle == null)
        {
            Debug.LogWarning("VirtualJoystick: Handle RectTransform not assigned!", this);
        }

        if (handleRange <= 0)
        {
            handleRange = 50f;
            Debug.LogWarning("VirtualJoystick: handleRange must be positive, reset to 50", this);
        }

        if (deadZone < 0 || deadZone > 1)
        {
            deadZone = Mathf.Clamp01(deadZone);
            Debug.LogWarning("VirtualJoystick: deadZone clamped to 0-1 range", this);
        }
    }
}

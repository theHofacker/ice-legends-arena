using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Super simple timing meter - just changes the bar's width and color.
/// No sliders, no fill images, just a basic colored rectangle.
/// </summary>
public class SimpleTimingMeterUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject playerObject;

    private RectTransform barRect;
    private Image barImage;
    private TimingMeter timingMeter;

    private float maxWidth = 300f; // Maximum bar width

    private void Start()
    {
        // Get components
        barRect = GetComponent<RectTransform>();
        barImage = GetComponent<Image>();

        // Store max width
        maxWidth = barRect.sizeDelta.x;

        // Find TimingMeter
        if (playerObject != null)
        {
            timingMeter = playerObject.GetComponent<TimingMeter>();
        }

        if (timingMeter == null)
        {
            timingMeter = FindObjectOfType<TimingMeter>();
        }

        if (timingMeter == null)
        {
            Debug.LogError("SimpleTimingMeterUI: No TimingMeter found!");
            return;
        }

        // Subscribe to events
        timingMeter.OnChargeUpdated += UpdateBar;

        // Hide initially
        gameObject.SetActive(false);

        Debug.Log("SimpleTimingMeterUI: Connected successfully!");
    }

    private void Update()
    {
        // Show/hide based on charging state
        if (timingMeter != null)
        {
            gameObject.SetActive(timingMeter.IsCharging);
        }
    }

    private void UpdateBar(float normalizedCharge)
    {
        if (barRect == null || barImage == null || timingMeter == null) return;

        // Update width
        float newWidth = maxWidth * Mathf.Clamp01(normalizedCharge);
        barRect.sizeDelta = new Vector2(newWidth, barRect.sizeDelta.y);

        // Update color based on zone
        TimingMeter.TimingResult currentZone = timingMeter.GetCurrentZone();
        barImage.color = timingMeter.GetZoneColor(currentZone);

        Debug.Log($"Bar: Width={newWidth}, Zone={currentZone}, Color={barImage.color}");
    }

    private void OnDestroy()
    {
        if (timingMeter != null)
        {
            timingMeter.OnChargeUpdated -= UpdateBar;
        }
    }
}

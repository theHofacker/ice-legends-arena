using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Visual representation of the perfect timing meter.
/// Shows charging progress with color-coded zones (yellow/green/red).
/// </summary>
public class TimingMeterUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject meterPanel;
    [SerializeField] private Image fillBar;
    [SerializeField] private UnityEngine.UI.Slider slider; // Optional: use slider instead
    [SerializeField] private Image greenZoneIndicator;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Timing Meter Reference")]
    [Tooltip("Drag the Player GameObject here (or leave empty to auto-find)")]
    [SerializeField] private GameObject playerObject;

    [Header("Visual Settings")]
    [SerializeField] private float resultDisplayDuration = 0.5f;

    // Component references
    private TimingMeter timingMeter;

    // State
    private float resultDisplayTimer = 0f;

    private void Start()
    {
        // Find TimingMeter
        if (playerObject != null)
        {
            // Use assigned player reference
            timingMeter = playerObject.GetComponent<TimingMeter>();
        }

        if (timingMeter == null)
        {
            // Try to find it automatically
            timingMeter = FindObjectOfType<TimingMeter>();
        }

        if (timingMeter == null)
        {
            Debug.LogError("TimingMeterUI: No TimingMeter found! Assign Player GameObject in Inspector or ensure Player has TimingMeter component.");
            return;
        }

        // Subscribe to events
        timingMeter.OnChargeUpdated += UpdateMeterVisual;
        timingMeter.OnTimingComplete += ShowResult;

        Debug.Log("TimingMeterUI: Successfully connected to TimingMeter!");

        // Hide meter initially
        if (meterPanel != null)
        {
            meterPanel.SetActive(false);
        }

        // Hide result text
        if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Show/hide meter based on charging state
        if (meterPanel != null)
        {
            meterPanel.SetActive(timingMeter != null && timingMeter.IsCharging);
        }

        // Handle result display timer
        if (resultDisplayTimer > 0)
        {
            resultDisplayTimer -= Time.deltaTime;
            if (resultDisplayTimer <= 0 && resultText != null)
            {
                resultText.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateMeterVisual(float normalizedCharge)
    {
        Debug.Log($"UpdateMeterVisual called! Charge: {normalizedCharge}");

        // Use slider if available, otherwise use fillBar
        if (slider != null)
        {
            slider.value = Mathf.Clamp01(normalizedCharge);
            Debug.Log($"Slider value set to: {slider.value}");

            // Update slider fill color - get the actual Fill child object's Image
            if (slider.fillRect != null && timingMeter != null)
            {
                // Find the Fill child of Fill Area
                Transform fillTransform = slider.fillRect.Find("Fill");
                if (fillTransform != null)
                {
                    var fillImage = fillTransform.GetComponent<Image>();
                    if (fillImage != null)
                    {
                        TimingMeter.TimingResult currentZone = timingMeter.GetCurrentZone();
                        Color zoneColor = timingMeter.GetZoneColor(currentZone);
                        fillImage.color = zoneColor;
                        Debug.Log($"Zone: {currentZone}, Color: {zoneColor}, Applied to Fill Image");
                    }
                }
                else
                {
                    // Fallback: try to get Image directly from fillRect
                    var fillImage = slider.fillRect.GetComponent<Image>();
                    if (fillImage != null)
                    {
                        TimingMeter.TimingResult currentZone = timingMeter.GetCurrentZone();
                        Color zoneColor = timingMeter.GetZoneColor(currentZone);
                        fillImage.color = zoneColor;
                        Debug.Log($"Zone: {currentZone}, Color: {zoneColor}, Applied to FillRect Image");
                    }
                }
            }
        }
        else if (fillBar != null)
        {
            // Fallback to fillBar
            fillBar.fillAmount = Mathf.Clamp01(normalizedCharge);
            Debug.Log($"Fill amount set to: {fillBar.fillAmount}");

            if (timingMeter != null)
            {
                TimingMeter.TimingResult currentZone = timingMeter.GetCurrentZone();
                Color zoneColor = timingMeter.GetZoneColor(currentZone);
                fillBar.color = zoneColor;
                Debug.Log($"Zone: {currentZone}, Color: {zoneColor}");
            }
        }
        else
        {
            Debug.LogError("Both Slider and FillBar are NULL!");
        }
    }

    private void ShowResult(TimingMeter.TimingResult result)
    {
        if (resultText == null) return;

        // Show result feedback
        string resultMessage = GetResultMessage(result);
        Color resultColor = timingMeter.GetZoneColor(result);

        resultText.text = resultMessage;
        resultText.color = resultColor;
        resultText.gameObject.SetActive(true);

        resultDisplayTimer = resultDisplayDuration;
    }

    private string GetResultMessage(TimingMeter.TimingResult result)
    {
        switch (result)
        {
            case TimingMeter.TimingResult.Weak:
                return "WEAK";
            case TimingMeter.TimingResult.Perfect:
                return "PERFECT!";
            case TimingMeter.TimingResult.Overcharged:
                return "OVERCHARGED";
            default:
                return "";
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (timingMeter != null)
        {
            timingMeter.OnChargeUpdated -= UpdateMeterVisual;
            timingMeter.OnTimingComplete -= ShowResult;
        }
    }
}

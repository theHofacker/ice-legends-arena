using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Changes the SHOOT button's color based on charge timing.
/// Based on Perplexity's recommendation - change Image.color directly!
/// </summary>
public class ShotButtonChargeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image buttonImage; // The SHOOT button's Image component
    [SerializeField] private GameObject playerObject;

    [Header("Colors")]
    [SerializeField] private Color lowColor = Color.yellow;   // 1-75%
    [SerializeField] private Color midColor = Color.green;    // 76-94%
    [SerializeField] private Color highColor = Color.red;     // 95-100%
    [SerializeField] private Color normalColor = Color.white; // Default when not charging

    private TimingMeter timingMeter;

    private void Start()
    {
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
            Debug.LogError("ShotButtonChargeUI: No TimingMeter found!");
            return;
        }

        // Subscribe to timing meter events
        timingMeter.OnChargeUpdated += UpdateButtonColor;

        // Set to normal color initially
        if (buttonImage != null)
        {
            buttonImage.color = normalColor;
        }

        Debug.Log("ShotButtonChargeUI: Connected to TimingMeter successfully!");
    }

    private void Update()
    {
        // Reset color when not charging
        if (timingMeter != null && !timingMeter.IsCharging && buttonImage != null)
        {
            buttonImage.color = normalColor;
        }
    }

    private void UpdateButtonColor(float charge)
    {
        if (buttonImage == null) return;

        // Convert to percent (0-1 to 0-100)
        float percent = charge * 100f;

        // Change color based on charge percentage
        if (percent <= 75f)
        {
            buttonImage.color = lowColor;  // Yellow
        }
        else if (percent <= 94f)
        {
            buttonImage.color = midColor;  // Green
        }
        else
        {
            buttonImage.color = highColor; // Red
        }

        Debug.Log($"Button color updated: {percent:F1}% -> {buttonImage.color}");
    }

    private void OnDestroy()
    {
        if (timingMeter != null)
        {
            timingMeter.OnChargeUpdated -= UpdateButtonColor;
        }
    }
}

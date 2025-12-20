using UnityEngine;

/// <summary>
/// Core perfect timing system - handles charge timing logic for shots, checks, etc.
/// Yellow zone (weak) → Green zone (perfect) → Red zone (overcharged)
/// </summary>
public class TimingMeter : MonoBehaviour
{
    [Header("Timing Windows")]
    [Tooltip("Total time to charge from 0 to overcharge")]
    [Range(0.5f, 3f)]
    [SerializeField] private float chargeDuration = 1.0f;

    [Tooltip("Start of green zone (percentage of charge duration)")]
    [Range(0f, 1f)]
    [SerializeField] private float greenZoneStart = 0.75f; // 75% of charge

    [Tooltip("End of green zone (percentage of charge duration)")]
    [Range(0f, 1f)]
    [SerializeField] private float greenZoneEnd = 0.95f; // 95% of charge

    [Header("Power Multipliers")]
    [Tooltip("Power multiplier for yellow zone (weak)")]
    [Range(0.5f, 1.5f)]
    [SerializeField] private float yellowZoneMultiplier = 0.8f;

    [Tooltip("Power multiplier for green zone (perfect)")]
    [Range(1f, 3f)]
    [SerializeField] private float greenZoneMultiplier = 2.0f;

    [Tooltip("Power multiplier for red zone (overcharged)")]
    [Range(0.3f, 1f)]
    [SerializeField] private float redZoneMultiplier = 0.6f;

    // Timing result
    public enum TimingResult
    {
        None,
        Weak,        // Yellow zone - released too early
        Perfect,     // Green zone - perfect timing!
        Overcharged  // Red zone - held too long
    }

    // State
    private bool isCharging = false;
    private float currentCharge = 0f;
    private TimingResult lastResult = TimingResult.None;

    // Events
    public System.Action<float> OnChargeUpdated; // Parameter: normalized charge (0-1+)
    public System.Action<TimingResult> OnTimingComplete;

    // Properties
    public bool IsCharging => isCharging;
    public float CurrentCharge => currentCharge;
    public float NormalizedCharge => currentCharge / chargeDuration;
    public TimingResult LastResult => lastResult;

    private void Update()
    {
        if (isCharging)
        {
            // Increase charge
            currentCharge += Time.deltaTime;

            // Notify listeners of charge progress
            OnChargeUpdated?.Invoke(NormalizedCharge);
        }
    }

    /// <summary>
    /// Start charging the timing meter
    /// </summary>
    public void StartCharging()
    {
        isCharging = true;
        currentCharge = 0f;
        lastResult = TimingResult.None;

        OnChargeUpdated?.Invoke(0f);
    }

    /// <summary>
    /// Stop charging and calculate timing result
    /// </summary>
    public TimingResult StopCharging()
    {
        if (!isCharging) return TimingResult.None;

        isCharging = false;
        lastResult = CalculateTimingResult();

        OnTimingComplete?.Invoke(lastResult);

        return lastResult;
    }

    /// <summary>
    /// Cancel charging without calculating result
    /// </summary>
    public void CancelCharging()
    {
        isCharging = false;
        currentCharge = 0f;
        lastResult = TimingResult.None;

        OnChargeUpdated?.Invoke(0f);
    }

    /// <summary>
    /// Calculate timing result based on current charge
    /// </summary>
    private TimingResult CalculateTimingResult()
    {
        float normalized = NormalizedCharge;

        // Check zones
        if (normalized >= greenZoneStart && normalized <= greenZoneEnd)
        {
            return TimingResult.Perfect; // Green zone!
        }
        else if (normalized > greenZoneEnd)
        {
            return TimingResult.Overcharged; // Red zone (held too long)
        }
        else
        {
            return TimingResult.Weak; // Yellow zone (released too early)
        }
    }

    /// <summary>
    /// Get power multiplier for a timing result
    /// </summary>
    public float GetPowerMultiplier(TimingResult result)
    {
        switch (result)
        {
            case TimingResult.Weak:
                return yellowZoneMultiplier;
            case TimingResult.Perfect:
                return greenZoneMultiplier;
            case TimingResult.Overcharged:
                return redZoneMultiplier;
            default:
                return 1f;
        }
    }

    /// <summary>
    /// Get power multiplier for last result
    /// </summary>
    public float GetLastPowerMultiplier()
    {
        return GetPowerMultiplier(lastResult);
    }

    /// <summary>
    /// Get current zone based on charge progress
    /// </summary>
    public TimingResult GetCurrentZone()
    {
        if (!isCharging) return TimingResult.None;

        float normalized = NormalizedCharge;

        if (normalized >= greenZoneStart && normalized <= greenZoneEnd)
            return TimingResult.Perfect;
        else if (normalized > greenZoneEnd)
            return TimingResult.Overcharged;
        else
            return TimingResult.Weak;
    }

    /// <summary>
    /// Get color for a timing zone (for UI feedback)
    /// </summary>
    public Color GetZoneColor(TimingResult zone)
    {
        switch (zone)
        {
            case TimingResult.Weak:
                return Color.yellow;
            case TimingResult.Perfect:
                return Color.green;
            case TimingResult.Overcharged:
                return Color.red;
            default:
                return Color.white;
        }
    }
}

using UnityEngine;

/// <summary>
/// Abstract base class for all character abilities
/// Handles cooldown, activation, and deactivation logic
/// Inherit from this class to create specific abilities (e.g., MeteorStrike, TemporalRewind)
/// </summary>
public abstract class Ability : MonoBehaviour
{
    [Header("Ability Settings")]
    [Tooltip("Reference to the ability data (name, cooldown, type, etc.)")]
    [SerializeField] protected AbilityData abilityData;

    [Header("Runtime State")]
    [SerializeField] protected bool isOnCooldown = false;
    [SerializeField] protected float cooldownRemaining = 0f;

    // Properties
    public AbilityData AbilityData => abilityData;
    public bool IsOnCooldown => isOnCooldown;
    public float CooldownRemaining => cooldownRemaining;
    public float CooldownProgress => abilityData != null ? (1f - (cooldownRemaining / abilityData.cooldown)) : 0f;

    // Events
    public delegate void AbilityActivatedDelegate(Ability ability);
    public event AbilityActivatedDelegate OnAbilityActivated;

    public delegate void AbilityEndedDelegate(Ability ability);
    public event AbilityEndedDelegate OnAbilityEnded;

    public delegate void CooldownChangedDelegate(float cooldownRemaining, float cooldownProgress);
    public event CooldownChangedDelegate OnCooldownChanged;

    protected virtual void Update()
    {
        // Update cooldown timer
        if (isOnCooldown && cooldownRemaining > 0f)
        {
            cooldownRemaining -= Time.deltaTime;

            // Notify listeners of cooldown change
            OnCooldownChanged?.Invoke(cooldownRemaining, CooldownProgress);

            if (cooldownRemaining <= 0f)
            {
                cooldownRemaining = 0f;
                isOnCooldown = false;
                Debug.Log($"{abilityData.abilityName} cooldown finished!");
            }
        }
    }

    /// <summary>
    /// Check if ability can be used (not on cooldown)
    /// Override this to add custom conditions (e.g., meter full, energy cost, etc.)
    /// </summary>
    public virtual bool CanUseAbility()
    {
        if (abilityData == null)
        {
            Debug.LogError("Ability: AbilityData is not assigned!");
            return false;
        }

        return !isOnCooldown;
    }

    /// <summary>
    /// Activate the ability
    /// Call this from UI button or input handler
    /// </summary>
    public void TryActivateAbility()
    {
        if (!CanUseAbility())
        {
            Debug.LogWarning($"{abilityData.abilityName} cannot be used right now (cooldown: {cooldownRemaining:F1}s)");
            return;
        }

        // Execute ability-specific logic
        ActivateAbility();

        // Start cooldown
        StartCooldown();

        // Notify listeners
        OnAbilityActivated?.Invoke(this);

        Debug.Log($"⚡ {abilityData.abilityName} ACTIVATED!");
    }

    /// <summary>
    /// Abstract method - implement ability-specific logic here
    /// Example: Spawn meteor, rewind time, create shield, etc.
    /// </summary>
    protected abstract void ActivateAbility();

    /// <summary>
    /// Start cooldown after ability is used
    /// </summary>
    protected virtual void StartCooldown()
    {
        if (abilityData != null)
        {
            isOnCooldown = true;
            cooldownRemaining = abilityData.cooldown;
            Debug.Log($"{abilityData.abilityName} on cooldown for {cooldownRemaining}s");
        }
    }

    /// <summary>
    /// Force ability off cooldown (for testing or special conditions)
    /// </summary>
    public void ResetCooldown()
    {
        isOnCooldown = false;
        cooldownRemaining = 0f;
        Debug.Log($"{abilityData.abilityName} cooldown reset!");
    }

    /// <summary>
    /// Get formatted cooldown time (e.g., "12.5s")
    /// </summary>
    public string GetFormattedCooldown()
    {
        return $"{cooldownRemaining:F1}s";
    }
}

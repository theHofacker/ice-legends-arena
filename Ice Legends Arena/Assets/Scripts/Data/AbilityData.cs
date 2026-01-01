using UnityEngine;

/// <summary>
/// ScriptableObject containing data for a character's special ability.
/// This defines WHAT the ability is - the actual implementation lives in Ability scripts.
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "Ice Legends/Ability Data")]
public class AbilityData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Ability name (e.g., 'Meteor Strike', 'Temporal Rewind')")]
    public string abilityName = "New Ability";

    [Tooltip("Ability description for UI")]
    [TextArea(3, 5)]
    public string description = "Special ability description here.";

    [Header("Mechanics")]
    [Tooltip("Cooldown in seconds")]
    [Range(15f, 60f)]
    public float cooldown = 30f;

    [Tooltip("Duration of ability effect (0 = instant)")]
    [Range(0f, 10f)]
    public float duration = 0f;

    [Tooltip("Ability type category")]
    public AbilityType abilityType = AbilityType.Offensive;

    [Header("Visual & Audio")]
    [Tooltip("Icon for ability button")]
    public Sprite abilityIcon;

    [Tooltip("Particle effect prefab for ability")]
    public GameObject particleEffectPrefab;

    [Tooltip("Sound effect for ability activation")]
    public AudioClip activationSound;

    /// <summary>
    /// Get formatted ability info for UI
    /// </summary>
    public string GetAbilityInfo()
    {
        string info = $"<b>{abilityName}</b>\n";
        info += $"{description}\n\n";
        info += $"Cooldown: {cooldown}s";
        if (duration > 0)
        {
            info += $" | Duration: {duration}s";
        }
        return info;
    }
}

/// <summary>
/// Ability type categories
/// </summary>
public enum AbilityType
{
    Offensive,   // Deals damage or enhances attack (Meteor Strike, Rampage)
    Defensive,   // Protects or blocks (Holy Barrier)
    Utility,     // Movement or positioning (Phantom Step)
    Support,     // Buffs teammates or debuffs enemies
    Control      // Time manipulation, crowd control (Temporal Rewind)
}

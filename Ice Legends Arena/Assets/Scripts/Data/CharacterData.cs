using UnityEngine;

/// <summary>
/// ScriptableObject containing all data for a hockey character.
/// This is 100% transferable to 3D - only visual references need swapping.
/// </summary>
[CreateAssetMenu(fileName = "New Character", menuName = "Ice Legends/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Character name (e.g., 'Blaze the Elementalist')")]
    public string characterName = "New Character";

    [Tooltip("Character class/type (e.g., 'Elementalist', 'Berserker', 'Chronomancer')")]
    public string characterClass = "Unknown";

    [Tooltip("Player position")]
    public FormationManager.PlayerRole position = FormationManager.PlayerRole.Center;

    [Header("Stats")]
    [Tooltip("Shot power (multiplier for shot force) - Range: 0.8 to 1.5")]
    [Range(0.8f, 1.5f)]
    public float shotPower = 1.0f;

    [Tooltip("Movement speed (multiplier) - Range: 0.8 to 1.3")]
    [Range(0.8f, 1.3f)]
    public float speed = 1.0f;

    [Tooltip("Checking/hitting power (multiplier) - Range: 0.7 to 1.5")]
    [Range(0.7f, 1.5f)]
    public float checking = 1.0f;

    [Tooltip("Shot accuracy (affects aim cone tightness) - Range: 0.7 to 1.3")]
    [Range(0.7f, 1.3f)]
    public float accuracy = 1.0f;

    [Tooltip("Puck control (how quickly they possess puck) - Range: 0.8 to 1.2")]
    [Range(0.8f, 1.2f)]
    public float puckControl = 1.0f;

    [Header("Ability")]
    [Tooltip("Special ability for this character")]
    public AbilityData ability;

    [Header("Visual (2D - will swap to 3D model later)")]
    [Tooltip("Character sprite (2D) or preview icon (3D)")]
    public Sprite characterSprite;

    [Tooltip("Character color for team differentiation")]
    public Color characterColor = Color.white;

    [Tooltip("Animator controller for this character")]
    public RuntimeAnimatorController animatorController;

    [Header("Unlock & Progression")]
    [Tooltip("Is this character unlocked from start?")]
    public bool unlockedByDefault = true;

    [Tooltip("XP level required to unlock (if not default)")]
    public int unlockLevel = 0;

    /// <summary>
    /// Get a summary description of this character's strengths
    /// </summary>
    public string GetStatsDescription()
    {
        string desc = $"{characterName} ({characterClass})\n";
        desc += $"Position: {position}\n";
        desc += $"Shot Power: {GetStatRating(shotPower)}\n";
        desc += $"Speed: {GetStatRating(speed)}\n";
        desc += $"Checking: {GetStatRating(checking)}\n";
        desc += $"Accuracy: {GetStatRating(accuracy)}\n";
        desc += $"Puck Control: {GetStatRating(puckControl)}";
        return desc;
    }

    /// <summary>
    /// Convert stat multiplier to letter grade (S/A/B/C/D)
    /// </summary>
    private string GetStatRating(float stat)
    {
        if (stat >= 1.4f) return "S";
        if (stat >= 1.2f) return "A";
        if (stat >= 1.0f) return "B";
        if (stat >= 0.9f) return "C";
        return "D";
    }
}

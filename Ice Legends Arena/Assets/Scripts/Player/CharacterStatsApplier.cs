using UnityEngine;

/// <summary>
/// Applies CharacterData stats to player gameplay.
/// Attach to any player GameObject (player-controlled or AI) to give them character-specific stats.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CharacterStatsApplier : MonoBehaviour
{
    [Header("Character Assignment")]
    [Tooltip("Character data to apply to this player")]
    public CharacterData characterData;

    [Header("Auto-Find Components")]
    [Tooltip("Automatically find and apply stats to these components")]
    public bool autoApplyOnStart = true;

    // Component references (will auto-find these)
    private PlayerController playerController;
    private ShootingController shootingController;
    private CheckingController checkingController;
    private PassingController passingController;
    private SpriteRenderer spriteRenderer;

    // AI components
    private AIController aiController;
    private TeammateController teammateController;

    // Ability component (dynamically created based on character)
    private Ability currentAbility;

    /// <summary>
    /// The ability component attached to this character
    /// </summary>
    public Ability CurrentAbility => currentAbility;

    private void Start()
    {
        if (characterData == null)
        {
            Debug.LogWarning($"{gameObject.name}: No CharacterData assigned! Using default stats.");
            return;
        }

        FindComponents();

        if (autoApplyOnStart)
        {
            ApplyAllStats();
        }
    }

    private void FindComponents()
    {
        playerController = GetComponent<PlayerController>();
        shootingController = GetComponent<ShootingController>();
        checkingController = GetComponent<CheckingController>();
        passingController = GetComponent<PassingController>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        aiController = GetComponent<AIController>();
        teammateController = GetComponent<TeammateController>();
    }

    public void ApplyAllStats()
    {
        if (characterData == null) return;

        ApplyMovementStats();
        ApplyShootingStats();
        ApplyCheckingStats();
        ApplyPassingStats();
        ApplyVisualStats();
        ApplyAIStats();
        ApplyAbility();

        Debug.Log($"Applied {characterData.characterName} stats to {gameObject.name}");
    }

    private void ApplyAbility()
    {
        if (characterData.ability == null)
        {
            Debug.LogWarning($"{gameObject.name}: No ability assigned to {characterData.characterName}");
            return;
        }

        Ability[] existingAbilities = GetComponents<Ability>();
        foreach (Ability existingAbility in existingAbilities)
        {
            Debug.Log($"  Removing old ability: {existingAbility.GetType().Name} from {gameObject.name}");
            DestroyImmediate(existingAbility);
        }

        string abilityName = characterData.ability.abilityName.Replace(" ", "");
        Debug.Log($"  Creating ability: {abilityName} for {characterData.characterName}");

        switch (abilityName)
        {
            case "MeteorStrike":
                currentAbility = gameObject.AddComponent<MeteorStrike>();
                break;
            case "HolyBarrier":
                currentAbility = gameObject.AddComponent<HolyBarrier>();
                break;
            case "RampageMode":
                currentAbility = gameObject.AddComponent<RampageMode>();
                break;
            case "TemporalRewind":
                currentAbility = gameObject.AddComponent<TemporalRewind>();
                break;
            case "PhantomStep":
                currentAbility = gameObject.AddComponent<PhantomStep>();
                break;
            case "CarbuncleCall":
                currentAbility = gameObject.AddComponent<CarbuncleCall>();
                break;
            case "TrickShot":
                currentAbility = gameObject.AddComponent<TrickShot>();
                break;
            case "Shapeshift":
                currentAbility = gameObject.AddComponent<Shapeshift>();
                break;
            default:
                Debug.LogWarning($"  Unknown ability: {abilityName}");
                return;
        }

        Debug.Log($"  Ability: {characterData.ability.abilityName} attached to {gameObject.name}");
    }

    private void ApplyMovementStats()
    {
        if (playerController != null)
        {
            playerController.moveSpeed *= characterData.speed;
            Debug.Log($"  Speed: {characterData.speed}x (final: {playerController.moveSpeed})");
        }

        if (aiController != null)
        {
            aiController.moveSpeed *= characterData.speed;
        }

        if (teammateController != null)
        {
            teammateController.aiMoveSpeed *= characterData.speed;
        }
    }

    private void ApplyShootingStats()
    {
        if (shootingController != null)
        {
            shootingController.wristShotPower *= characterData.shotPower;
            shootingController.slapShotPower *= characterData.shotPower;

            Debug.Log($"  Shot Power: {characterData.shotPower}x");
        }
    }

    private void ApplyCheckingStats()
    {
        if (checkingController != null)
        {
            checkingController.checkForce *= characterData.checking;

            Debug.Log($"  Checking: {characterData.checking}x (final: {checkingController.checkForce})");
        }
    }

    private void ApplyPassingStats()
    {
        if (passingController != null)
        {
            passingController.passPower *= characterData.shotPower;
            passingController.saucerPassPower *= characterData.accuracy;

            Debug.Log($"  Pass Power: {characterData.shotPower}x");
        }
    }

    private void ApplyVisualStats()
    {
        if (spriteRenderer != null)
        {
            if (characterData.characterColor != Color.white)
            {
                spriteRenderer.color = characterData.characterColor;
            }

            if (characterData.characterSprite != null)
            {
                spriteRenderer.sprite = characterData.characterSprite;
            }

            Debug.Log($"  Applied visual: {characterData.characterColor}");
        }
    }

    private void ApplyAIStats()
    {
        if (aiController != null)
        {
            aiController.possessionRadius *= characterData.puckControl;
        }

        if (teammateController != null)
        {
            teammateController.receiveRadius *= characterData.puckControl;
        }
    }

    public string GetCharacterName()
    {
        return characterData != null ? characterData.characterName : "Unknown";
    }

    public AbilityData GetAbility()
    {
        return characterData != null ? characterData.ability : null;
    }

    private void OnDrawGizmosSelected()
    {
        if (characterData == null) return;

        Gizmos.color = characterData.characterColor;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}

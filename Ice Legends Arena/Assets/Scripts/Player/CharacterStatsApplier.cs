using UnityEngine;

/// <summary>
/// Applies CharacterData stats to player gameplay.
/// Attach to any player GameObject (player-controlled or AI) to give them character-specific stats.
/// This component is 100% transferable to 3D!
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
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

    private void Start()
    {
        if (characterData == null)
        {
            Debug.LogWarning($"{gameObject.name}: No CharacterData assigned! Using default stats.");
            return;
        }

        // Find all relevant components
        FindComponents();

        if (autoApplyOnStart)
        {
            ApplyAllStats();
        }
    }

    /// <summary>
    /// Find all gameplay components on this GameObject
    /// </summary>
    private void FindComponents()
    {
        playerController = GetComponent<PlayerController>();
        shootingController = GetComponent<ShootingController>();
        checkingController = GetComponent<CheckingController>();
        passingController = GetComponent<PassingController>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // AI components
        aiController = GetComponent<AIController>();
        teammateController = GetComponent<TeammateController>();
    }

    /// <summary>
    /// Apply all character stats to gameplay systems
    /// </summary>
    public void ApplyAllStats()
    {
        if (characterData == null) return;

        ApplyMovementStats();
        ApplyShootingStats();
        ApplyCheckingStats();
        ApplyPassingStats();
        ApplyVisualStats();
        ApplyAIStats();

        Debug.Log($"Applied {characterData.characterName} stats to {gameObject.name}");
    }

    /// <summary>
    /// Apply speed multiplier to movement
    /// </summary>
    private void ApplyMovementStats()
    {
        if (playerController != null)
        {
            // Multiply base move speed by character's speed stat
            playerController.moveSpeed *= characterData.speed;
            Debug.Log($"  Speed: {characterData.speed}x (final: {playerController.moveSpeed})");
        }

        // Apply to AI controllers
        if (aiController != null)
        {
            aiController.moveSpeed *= characterData.speed;
        }

        if (teammateController != null)
        {
            teammateController.aiMoveSpeed *= characterData.speed;
        }
    }

    /// <summary>
    /// Apply shot power and accuracy to shooting
    /// </summary>
    private void ApplyShootingStats()
    {
        if (shootingController != null)
        {
            // Shot power affects all shot types
            shootingController.wristShotPower *= characterData.shotPower;
            shootingController.slapShotPower *= characterData.shotPower;

            // Accuracy affects aim spread (inverse relationship - higher accuracy = tighter spread)
            float accuracyMultiplier = 1f / characterData.accuracy;
            shootingController.maxAimSpread *= accuracyMultiplier;

            Debug.Log($"  Shot Power: {characterData.shotPower}x, Accuracy: {characterData.accuracy}x");
        }
    }

    /// <summary>
    /// Apply checking power to body checks
    /// </summary>
    private void ApplyCheckingStats()
    {
        if (checkingController != null)
        {
            // Checking multiplier affects knockback force
            checkingController.checkForce *= characterData.checking;

            Debug.Log($"  Checking: {characterData.checking}x (final: {checkingController.checkForce})");
        }
    }

    /// <summary>
    /// Apply passing stats
    /// </summary>
    private void ApplyPassingStats()
    {
        if (passingController != null)
        {
            // Pass power affected by shot power (same muscle strength)
            passingController.passPower *= characterData.shotPower;

            // Saucer pass affected by accuracy
            passingController.saucerPassPower *= characterData.accuracy;

            Debug.Log($"  Pass Power: {characterData.shotPower}x");
        }
    }

    /// <summary>
    /// Apply visual changes (sprite, color)
    /// </summary>
    private void ApplyVisualStats()
    {
        if (spriteRenderer != null)
        {
            // Apply character color (if not already overridden by team color)
            if (characterData.characterColor != Color.white)
            {
                spriteRenderer.color = characterData.characterColor;
            }

            // Apply character sprite (2D only - in 3D this would swap model)
            if (characterData.characterSprite != null)
            {
                spriteRenderer.sprite = characterData.characterSprite;
            }

            Debug.Log($"  Applied visual: {characterData.characterColor}");
        }
    }

    /// <summary>
    /// Apply stats to AI controllers (if this is an AI player)
    /// </summary>
    private void ApplyAIStats()
    {
        // Puck control affects possession radius for AI
        if (aiController != null)
        {
            aiController.possessionRadius *= characterData.puckControl;
        }

        if (teammateController != null)
        {
            teammateController.receiveRadius *= characterData.puckControl;
        }
    }

    /// <summary>
    /// Get character name for UI
    /// </summary>
    public string GetCharacterName()
    {
        return characterData != null ? characterData.characterName : "Unknown";
    }

    /// <summary>
    /// Get character ability data
    /// </summary>
    public AbilityData GetAbility()
    {
        return characterData != null ? characterData.ability : null;
    }

    /// <summary>
    /// Debug: Show stats in Inspector
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (characterData == null) return;

        // This will show in Scene view when selected
        Gizmos.color = characterData.characterColor;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}

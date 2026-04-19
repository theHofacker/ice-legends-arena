using UnityEngine;

/// <summary>
/// Druid ability: Shapeshift
/// Transform into different animal forms with different stat bonuses
/// Cycles through: Human -> Bear -> Cheetah -> Hawk -> Human
/// </summary>
public class Shapeshift : Ability
{
    public enum ShapeshiftForm
    {
        Human,      // Normal form
        Bear,       // Tank - +50% checking, -30% speed
        Cheetah,    // Speed - +50% speed, -30% checking
        Hawk        // Agile - +30% speed, +30% acceleration, smaller hitbox
    }

    [Header("Form Stats")]
    [Tooltip("Bear form checking multiplier")]
    [SerializeField] private float bearCheckingMult = 1.5f;
    [Tooltip("Bear form speed multiplier")]
    [SerializeField] private float bearSpeedMult = 0.7f;

    [Tooltip("Cheetah form speed multiplier")]
    [SerializeField] private float cheetahSpeedMult = 1.5f;
    [Tooltip("Cheetah form checking multiplier")]
    [SerializeField] private float cheetahCheckingMult = 0.7f;

    [Tooltip("Hawk form speed multiplier")]
    [SerializeField] private float hawkSpeedMult = 1.3f;
    [Tooltip("Hawk form acceleration multiplier")]
    [SerializeField] private float hawkAccelMult = 1.5f;

    [Header("Visual Settings")]
    [SerializeField] private Color bearColor = new Color(0.6f, 0.4f, 0.2f, 1f);      // Brown
    [SerializeField] private Color cheetahColor = new Color(1f, 0.8f, 0.2f, 1f);     // Yellow/Gold
    [SerializeField] private Color hawkColor = new Color(0.5f, 0.7f, 1f, 1f);        // Light blue

    // References
    private PlayerManager playerManager;
    private PlayerController playerController;
    private CheckingController checkingController;
    private SpriteRenderer playerSprite;
    private CapsuleCollider playerCollider;

    // State
    private ShapeshiftForm currentForm = ShapeshiftForm.Human;
    private float originalSpeed;
    private float originalAcceleration;
    private float originalCheckForce;
    private float originalColliderRadius;
    private Color originalColor;

    public ShapeshiftForm CurrentForm => currentForm;

    protected override void Awake()
    {
        base.Awake();
        playerManager = FindAnyObjectByType<PlayerManager>();
    }

    protected override void ActivateAbility()
    {
        // Get controlled player
        GameObject controlledPlayer = GetControlledPlayer();
        if (controlledPlayer == null)
        {
            Debug.LogWarning("Shapeshift: No controlled player found!");
            return;
        }

        // Cache references (only on first use)
        if (playerController == null)
        {
            playerController = controlledPlayer.GetComponent<PlayerController>();
            checkingController = controlledPlayer.GetComponent<CheckingController>();
            playerSprite = controlledPlayer.GetComponent<SpriteRenderer>();
            if (playerSprite == null)
                playerSprite = controlledPlayer.GetComponentInChildren<SpriteRenderer>();
            playerCollider = controlledPlayer.GetComponent<CapsuleCollider>();

            // Store original values
            if (playerController != null)
            {
                originalSpeed = playerController.moveSpeed;
                originalAcceleration = playerController.acceleration;
            }
            if (checkingController != null)
            {
                originalCheckForce = 15f; // Default check force
            }
            if (playerCollider != null)
            {
                originalColliderRadius = playerCollider.radius;
            }
            if (playerSprite != null)
            {
                originalColor = playerSprite.color;
            }
        }

        // Cycle to next form
        CycleForm(controlledPlayer);
    }

    private GameObject GetControlledPlayer()
    {
        if (playerManager != null && playerManager.CurrentPlayer != null)
        {
            return playerManager.CurrentPlayer;
        }
        return GameObject.FindGameObjectWithTag("Player");
    }

    /// <summary>
    /// Cycle to the next shapeshift form
    /// </summary>
    private void CycleForm(GameObject player)
    {
        // Progress to next form
        currentForm = (ShapeshiftForm)(((int)currentForm + 1) % 4);

        // Apply form stats
        ApplyFormStats(player);

        // Visual feedback
        ApplyFormVisuals(player);

        Debug.Log($"SHAPESHIFT! Now in {currentForm} form!");
    }

    /// <summary>
    /// Apply stat changes for the current form
    /// </summary>
    private void ApplyFormStats(GameObject player)
    {
        // Reset to base stats first
        if (playerController != null)
        {
            playerController.moveSpeed = originalSpeed;
            playerController.acceleration = originalAcceleration;
        }

        // Add or get modifier
        ShapeshiftModifier modifier = player.GetComponent<ShapeshiftModifier>();
        if (modifier == null)
        {
            modifier = player.AddComponent<ShapeshiftModifier>();
        }

        switch (currentForm)
        {
            case ShapeshiftForm.Human:
                // Back to normal
                modifier.SetMultipliers(1f, 1f, 1f);
                if (playerCollider != null)
                    playerCollider.radius = originalColliderRadius;
                Debug.Log("  Human form: Base stats restored");
                break;

            case ShapeshiftForm.Bear:
                // Tank - high checking, slow
                modifier.SetMultipliers(bearSpeedMult, 1f, bearCheckingMult);
                if (playerController != null)
                    playerController.moveSpeed = originalSpeed * bearSpeedMult;
                if (playerCollider != null)
                    playerCollider.radius = originalColliderRadius * 1.2f; // Bigger hitbox
                Debug.Log($"  Bear form: Speed {bearSpeedMult}x, Checking {bearCheckingMult}x");
                break;

            case ShapeshiftForm.Cheetah:
                // Speed - fast, weak checking
                modifier.SetMultipliers(cheetahSpeedMult, 1f, cheetahCheckingMult);
                if (playerController != null)
                    playerController.moveSpeed = originalSpeed * cheetahSpeedMult;
                if (playerCollider != null)
                    playerCollider.radius = originalColliderRadius * 0.9f; // Slightly smaller
                Debug.Log($"  Cheetah form: Speed {cheetahSpeedMult}x, Checking {cheetahCheckingMult}x");
                break;

            case ShapeshiftForm.Hawk:
                // Agile - fast acceleration, small hitbox
                modifier.SetMultipliers(hawkSpeedMult, hawkAccelMult, 1f);
                if (playerController != null)
                {
                    playerController.moveSpeed = originalSpeed * hawkSpeedMult;
                    playerController.acceleration = originalAcceleration * hawkAccelMult;
                }
                if (playerCollider != null)
                    playerCollider.radius = originalColliderRadius * 0.75f; // Much smaller
                Debug.Log($"  Hawk form: Speed {hawkSpeedMult}x, Accel {hawkAccelMult}x, Small hitbox");
                break;
        }
    }

    /// <summary>
    /// Apply visual changes for the current form
    /// </summary>
    private void ApplyFormVisuals(GameObject player)
    {
        if (playerSprite == null) return;

        Color targetColor;
        float targetScale;

        switch (currentForm)
        {
            case ShapeshiftForm.Bear:
                targetColor = bearColor;
                targetScale = 1.2f;
                break;
            case ShapeshiftForm.Cheetah:
                targetColor = cheetahColor;
                targetScale = 1.0f;
                break;
            case ShapeshiftForm.Hawk:
                targetColor = hawkColor;
                targetScale = 0.85f;
                break;
            default:
                targetColor = originalColor;
                targetScale = 1.0f;
                break;
        }

        // Apply color
        playerSprite.color = targetColor;

        // Apply scale (visual only, collider handles actual size)
        player.transform.localScale = Vector3.one * 8f * targetScale; // Assuming base scale is 8

        // Transformation effect
        StartCoroutine(TransformEffect(player));
    }

    /// <summary>
    /// Visual transformation effect
    /// </summary>
    private System.Collections.IEnumerator TransformEffect(GameObject player)
    {
        if (playerSprite == null) yield break;

        // Brief flash
        Color flashColor = Color.white;
        Color targetColor = playerSprite.color;

        playerSprite.color = flashColor;
        yield return new WaitForSeconds(0.1f);
        playerSprite.color = targetColor;

        // Pulse effect
        Vector3 baseScale = player.transform.localScale;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.1f;
            player.transform.localScale = baseScale * pulse;
            yield return null;
        }

        player.transform.localScale = baseScale;
    }

    private void OnDestroy()
    {
        // Restore original stats if destroyed while transformed
        if (currentForm != ShapeshiftForm.Human && playerController != null)
        {
            playerController.moveSpeed = originalSpeed;
            playerController.acceleration = originalAcceleration;
            if (playerSprite != null)
                playerSprite.color = originalColor;
            if (playerCollider != null)
                playerCollider.radius = originalColliderRadius;
        }
    }
}

/// <summary>
/// Modifier component for shapeshift stat changes
/// Can be checked by CheckingController for damage multipliers
/// </summary>
public class ShapeshiftModifier : MonoBehaviour
{
    public float SpeedMultiplier { get; private set; } = 1f;
    public float AccelerationMultiplier { get; private set; } = 1f;
    public float CheckingMultiplier { get; private set; } = 1f;

    public void SetMultipliers(float speed, float accel, float checking)
    {
        SpeedMultiplier = speed;
        AccelerationMultiplier = accel;
        CheckingMultiplier = checking;
    }
}

using UnityEngine;
using System.Collections;

/// <summary>
/// Assassin ability: Phantom Step
/// Becomes invisible, gains speed boost, and leaves a shadow clone decoy
/// Perfect for sneaky plays and puck steals
/// </summary>
public class PhantomStep : Ability
{
    [Header("Phantom Step Settings")]
    [Tooltip("Speed multiplier during phantom step (1.5 = +50%)")]
    [Range(1f, 2f)]
    [SerializeField] private float speedMultiplier = 1.5f;

    [Tooltip("Player transparency during phantom step (0 = invisible, 1 = visible)")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float invisibilityAlpha = 0.3f;

    [Tooltip("How long the shadow clone decoy lasts")]
    [Range(1f, 5f)]
    [SerializeField] private float cloneDuration = 2f;

    [Header("Visual Settings")]
    [Tooltip("Color tint for the shadow clone")]
    [SerializeField] private Color cloneColor = new Color(0.2f, 0.2f, 0.3f, 0.7f);

    // References
    private PlayerManager playerManager;
    private PlayerController playerController;
    private SpriteRenderer playerSprite;

    // Original values
    private float originalMoveSpeed;
    private Color originalColor;

    // State
    private bool isPhantomActive = false;
    private GameObject shadowClone;
    private Coroutine phantomCoroutine;

    // Public property
    public bool IsPhantomActive => isPhantomActive;

    protected override void Awake()
    {
        base.Awake();
        playerManager = FindAnyObjectByType<PlayerManager>();
    }

    protected override void ActivateAbility()
    {
        if (isPhantomActive)
        {
            Debug.Log("Phantom Step already active!");
            return;
        }

        // Get controlled player
        GameObject controlledPlayer = GetControlledPlayer();
        if (controlledPlayer == null)
        {
            Debug.LogWarning("PhantomStep: No controlled player found!");
            return;
        }

        // Cache references
        playerController = controlledPlayer.GetComponent<PlayerController>();
        playerSprite = controlledPlayer.GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = controlledPlayer.GetComponentInChildren<SpriteRenderer>();

        // Store original values
        if (playerController != null)
            originalMoveSpeed = playerController.moveSpeed;
        if (playerSprite != null)
            originalColor = playerSprite.color;

        // Activate phantom step
        ActivatePhantom(controlledPlayer);

        // Start duration timer
        float duration = abilityData != null ? abilityData.duration : 3f;
        phantomCoroutine = StartCoroutine(PhantomDuration(duration, controlledPlayer));

        Debug.Log($"PHANTOM STEP ACTIVATED! Duration: {duration}s");
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
    /// Activate phantom step effects
    /// </summary>
    private void ActivatePhantom(GameObject player)
    {
        isPhantomActive = true;

        // Spawn shadow clone at current position BEFORE moving
        SpawnShadowClone(player);

        // Apply speed boost
        if (playerController != null)
        {
            playerController.moveSpeed = originalMoveSpeed * speedMultiplier;
            Debug.Log($"Speed boosted: {originalMoveSpeed} -> {playerController.moveSpeed}");
        }

        // Apply invisibility (semi-transparent)
        if (playerSprite != null)
        {
            Color invisColor = originalColor;
            invisColor.a = invisibilityAlpha;
            playerSprite.color = invisColor;
            Debug.Log($"Player now semi-invisible (alpha: {invisibilityAlpha})");
        }

        // Add phantom modifier for special interactions (e.g., steal without penalty)
        PhantomStepModifier modifier = player.GetComponent<PhantomStepModifier>();
        if (modifier == null)
        {
            modifier = player.AddComponent<PhantomStepModifier>();
        }
        modifier.Activate();
    }

    /// <summary>
    /// Spawn a shadow clone decoy at the player's position
    /// </summary>
    private void SpawnShadowClone(GameObject player)
    {
        // Create clone object
        shadowClone = new GameObject("ShadowClone");
        shadowClone.transform.position = player.transform.position;
        shadowClone.transform.rotation = player.transform.rotation;
        shadowClone.transform.localScale = player.transform.localScale;

        // Copy sprite (will be replaced by 3D visuals later)
        SpriteRenderer cloneSr = shadowClone.AddComponent<SpriteRenderer>();
        if (playerSprite != null)
        {
            cloneSr.sprite = playerSprite.sprite;
            cloneSr.sortingOrder = playerSprite.sortingOrder - 1;
        }
        cloneSr.color = cloneColor;

        // Add fade-out behavior
        ShadowCloneBehavior behavior = shadowClone.AddComponent<ShadowCloneBehavior>();
        behavior.Initialize(cloneDuration, cloneColor);

        Debug.Log($"Shadow clone spawned at {shadowClone.transform.position}");
    }

    /// <summary>
    /// Phantom step duration coroutine
    /// </summary>
    private IEnumerator PhantomDuration(float duration, GameObject player)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Flicker effect near end (last 0.5 seconds)
            if (duration - elapsed < 0.5f && playerSprite != null)
            {
                float flicker = Mathf.PingPong(Time.time * 15f, 1f);
                Color flickerColor = originalColor;
                flickerColor.a = Mathf.Lerp(invisibilityAlpha, originalColor.a, flicker);
                playerSprite.color = flickerColor;
            }

            yield return null;
        }

        EndPhantom(player);
    }

    /// <summary>
    /// End phantom step and restore normal state
    /// </summary>
    private void EndPhantom(GameObject player)
    {
        isPhantomActive = false;

        // Restore speed
        if (playerController != null)
        {
            playerController.moveSpeed = originalMoveSpeed;
            Debug.Log($"Speed restored: {originalMoveSpeed}");
        }

        // Restore visibility
        if (playerSprite != null)
        {
            playerSprite.color = originalColor;
            Debug.Log("Player visibility restored");
        }

        // Remove phantom modifier
        PhantomStepModifier modifier = player.GetComponent<PhantomStepModifier>();
        if (modifier != null)
        {
            modifier.Deactivate();
        }

        Debug.Log("PHANTOM STEP ENDED!");
    }

    private void OnDestroy()
    {
        if (isPhantomActive && phantomCoroutine != null)
        {
            StopCoroutine(phantomCoroutine);
            if (playerController != null)
                playerController.moveSpeed = originalMoveSpeed;
            if (playerSprite != null)
                playerSprite.color = originalColor;
        }
        if (shadowClone != null)
        {
            Destroy(shadowClone);
        }
    }
}

/// <summary>
/// Modifier component for phantom step special interactions
/// Can be checked by other systems for special behavior (e.g., puck steal without penalty)
/// </summary>
public class PhantomStepModifier : MonoBehaviour
{
    public bool IsActive { get; private set; }

    public void Activate()
    {
        IsActive = true;
        Debug.Log("PhantomStepModifier: Stealth mode active - can steal without penalty!");
    }

    public void Deactivate()
    {
        IsActive = false;
        Debug.Log("PhantomStepModifier: Stealth mode ended");
    }
}

/// <summary>
/// Behavior for the shadow clone decoy
/// Handles fade-out animation and destruction
/// </summary>
public class ShadowCloneBehavior : MonoBehaviour
{
    private float duration;
    private float elapsed = 0f;
    private SpriteRenderer spriteRenderer;
    private Color baseColor;

    public void Initialize(float fadeDuration, Color color)
    {
        duration = fadeDuration;
        baseColor = color;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        if (spriteRenderer != null)
        {
            // Fade out over time
            float t = elapsed / duration;
            Color fadeColor = baseColor;
            fadeColor.a = Mathf.Lerp(baseColor.a, 0f, t);
            spriteRenderer.color = fadeColor;

            // Slight scale pulse for ghostly effect
            float pulse = 1f + Mathf.Sin(elapsed * 5f) * 0.05f;
            transform.localScale = Vector3.one * 8f * pulse; // Assuming base scale is 8
        }

        // Destroy when fully faded
        if (elapsed >= duration)
        {
            Debug.Log("Shadow clone faded away");
            Destroy(gameObject);
        }
    }
}

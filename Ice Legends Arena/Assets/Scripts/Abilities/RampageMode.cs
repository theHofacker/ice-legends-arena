using UnityEngine;
using System.Collections;

/// <summary>
/// Berserker ability: Rampage Mode
/// Enters a berserker rage with increased speed and checking power
/// All body checks during rampage stun opponents
/// </summary>
public class RampageMode : Ability
{
    [Header("Rampage Buffs")]
    [Tooltip("Speed multiplier during rampage (1.5 = +50%)")]
    [Range(1f, 3f)]
    [SerializeField] private float speedMultiplier = 1.5f;

    [Tooltip("Checking power multiplier during rampage (2.0 = +100%)")]
    [Range(1f, 4f)]
    [SerializeField] private float checkingMultiplier = 2.0f;

    [Tooltip("Stun duration for body checks during rampage")]
    [Range(1f, 5f)]
    [SerializeField] private float rampageStunDuration = 2f;

    [Header("Visual Settings")]
    [Tooltip("Color tint during rampage")]
    [SerializeField] private Color rampageColor = new Color(1f, 0.2f, 0.2f, 1f); // Red

    [Tooltip("Aura effect prefab (optional)")]
    [SerializeField] private GameObject auraPrefab;

    // References
    private PlayerManager playerManager;
    private PlayerController playerController;
    private CheckingController checkingController;
    private SpriteRenderer playerSprite;

    // Original values to restore after rampage
    private float originalMoveSpeed;
    private float originalCheckForce;
    private float originalStunDuration;
    private Color originalColor;

    // Active state
    private bool isRampaging = false;
    private GameObject activeAura;
    private Coroutine rampageCoroutine;

    // Public property for external checks
    public bool IsRampaging => isRampaging;

    protected override void Awake()
    {
        base.Awake();
        playerManager = FindAnyObjectByType<PlayerManager>();
        if (playerManager == null)
        {
            Debug.LogWarning("RampageMode: Could not find PlayerManager!");
        }
    }

    protected override void ActivateAbility()
    {
        if (isRampaging)
        {
            Debug.Log("Already in Rampage Mode!");
            return;
        }

        // Get controlled player and their components
        GameObject controlledPlayer = GetControlledPlayer();
        if (controlledPlayer == null)
        {
            Debug.LogWarning("RampageMode: No controlled player found!");
            return;
        }

        // Cache references
        playerController = controlledPlayer.GetComponent<PlayerController>();
        checkingController = controlledPlayer.GetComponent<CheckingController>();
        playerSprite = controlledPlayer.GetComponent<SpriteRenderer>();
        if (playerSprite == null)
            playerSprite = controlledPlayer.GetComponentInChildren<SpriteRenderer>();

        // Store original values
        StoreOriginalValues();

        // Apply rampage buffs
        ApplyRampageBuffs(controlledPlayer);

        // Start rampage duration timer
        float duration = abilityData != null ? abilityData.duration : 5f;
        rampageCoroutine = StartCoroutine(RampageDuration(duration, controlledPlayer));

        Debug.Log($"RAMPAGE MODE ACTIVATED! Duration: {duration}s");
    }

    /// <summary>
    /// Get the currently controlled player from PlayerManager
    /// </summary>
    private GameObject GetControlledPlayer()
    {
        if (playerManager != null && playerManager.CurrentPlayer != null)
        {
            return playerManager.CurrentPlayer;
        }
        return GameObject.FindGameObjectWithTag("Player");
    }

    /// <summary>
    /// Store original values before applying buffs
    /// </summary>
    private void StoreOriginalValues()
    {
        if (playerController != null)
        {
            originalMoveSpeed = playerController.moveSpeed;
        }

        if (checkingController != null)
        {
            // Use reflection or make these fields accessible
            // For now, we'll store defaults and apply multipliers differently
            originalCheckForce = 15f; // Default from CheckingController
            originalStunDuration = 1.5f; // Default stun duration
        }

        if (playerSprite != null)
        {
            originalColor = playerSprite.color;
        }
    }

    /// <summary>
    /// Apply rampage buffs to the player
    /// </summary>
    private void ApplyRampageBuffs(GameObject player)
    {
        isRampaging = true;

        // Buff speed
        if (playerController != null)
        {
            playerController.moveSpeed = originalMoveSpeed * speedMultiplier;
            Debug.Log($"Speed buffed: {originalMoveSpeed} -> {playerController.moveSpeed}");
        }

        // Buff checking (we'll handle this via the RampageCheckModifier component)
        RampageCheckModifier modifier = player.GetComponent<RampageCheckModifier>();
        if (modifier == null)
        {
            modifier = player.AddComponent<RampageCheckModifier>();
        }
        modifier.Activate(checkingMultiplier, rampageStunDuration);

        // Visual: Change color to red
        if (playerSprite != null)
        {
            playerSprite.color = rampageColor;
        }

        // Visual: Add aura effect
        CreateRampageAura(player);
    }

    /// <summary>
    /// Create visual aura effect around player
    /// </summary>
    private void CreateRampageAura(GameObject player)
    {
        if (auraPrefab != null)
        {
            activeAura = Instantiate(auraPrefab, player.transform);
        }
        else
        {
            // Create placeholder aura
            activeAura = new GameObject("RampageAura");
            activeAura.transform.SetParent(player.transform);
            activeAura.transform.localPosition = Vector3.zero;

            // Add pulsing ring effect (will be replaced by 3D visuals later)
            SpriteRenderer auraSr = activeAura.AddComponent<SpriteRenderer>();
            auraSr.sprite = CreateAuraSprite();
            auraSr.color = new Color(1f, 0f, 0f, 0.4f);
            auraSr.sortingOrder = -1; // Behind player

            activeAura.transform.localScale = Vector3.one * 2f;

            // Add pulsing animation
            activeAura.AddComponent<RampageAuraPulse>();
        }
    }

    /// <summary>
    /// Create a simple circular aura sprite
    /// </summary>
    private Sprite CreateAuraSprite()
    {
        int resolution = 64;
        Texture2D texture = new Texture2D(resolution, resolution);
        Color[] colors = new Color[resolution * resolution];

        Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
        float outerRadius = resolution / 2f;
        float innerRadius = resolution / 3f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist < outerRadius && dist > innerRadius)
                {
                    // Gradient from inner to outer
                    float t = (dist - innerRadius) / (outerRadius - innerRadius);
                    float alpha = 1f - t;
                    colors[y * resolution + x] = new Color(1f, 1f, 1f, alpha * 0.5f);
                }
                else
                {
                    colors[y * resolution + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    /// <summary>
    /// Rampage duration coroutine
    /// </summary>
    private IEnumerator RampageDuration(float duration, GameObject player)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Flash effect near end of rampage (last 1 second)
            if (duration - elapsed < 1f && playerSprite != null)
            {
                float flash = Mathf.PingPong(Time.time * 10f, 1f);
                playerSprite.color = Color.Lerp(rampageColor, originalColor, flash);
            }

            yield return null;
        }

        // End rampage
        EndRampage(player);
    }

    /// <summary>
    /// End rampage and restore original values
    /// </summary>
    private void EndRampage(GameObject player)
    {
        isRampaging = false;

        // Restore speed
        if (playerController != null)
        {
            playerController.moveSpeed = originalMoveSpeed;
            Debug.Log($"Speed restored: {originalMoveSpeed}");
        }

        // Remove check modifier
        RampageCheckModifier modifier = player.GetComponent<RampageCheckModifier>();
        if (modifier != null)
        {
            modifier.Deactivate();
        }

        // Restore color
        if (playerSprite != null)
        {
            playerSprite.color = originalColor;
        }

        // Remove aura
        if (activeAura != null)
        {
            Destroy(activeAura);
            activeAura = null;
        }

        Debug.Log("RAMPAGE MODE ENDED!");
    }

    /// <summary>
    /// Clean up if ability is destroyed during rampage
    /// </summary>
    private void OnDestroy()
    {
        if (isRampaging && rampageCoroutine != null)
        {
            StopCoroutine(rampageCoroutine);
            // Try to restore values
            if (playerController != null)
            {
                playerController.moveSpeed = originalMoveSpeed;
            }
            if (playerSprite != null)
            {
                playerSprite.color = originalColor;
            }
            if (activeAura != null)
            {
                Destroy(activeAura);
            }
        }
    }
}

/// <summary>
/// Component that modifies checking behavior during rampage
/// Attached to player when rampage is active
/// </summary>
public class RampageCheckModifier : MonoBehaviour
{
    public bool IsActive { get; private set; }
    public float CheckMultiplier { get; private set; }
    public float StunDuration { get; private set; }

    public void Activate(float checkMultiplier, float stunDuration)
    {
        IsActive = true;
        CheckMultiplier = checkMultiplier;
        StunDuration = stunDuration;
        Debug.Log($"RampageCheckModifier activated: {checkMultiplier}x power, {stunDuration}s stun");
    }

    public void Deactivate()
    {
        IsActive = false;
        Debug.Log("RampageCheckModifier deactivated");
    }
}

/// <summary>
/// Pulsing animation for rampage aura
/// </summary>
public class RampageAuraPulse : MonoBehaviour
{
    private Vector3 baseScale;
    private float pulseSpeed = 3f;
    private SpriteRenderer sr;

    private void Start()
    {
        baseScale = transform.localScale;
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        // Pulsing scale
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.2f;
        transform.localScale = baseScale * pulse;

        // Rotating effect around Y axis (vertical) instead of Z
        transform.Rotate(0, 60f * Time.deltaTime, 0);

        // Pulsing alpha
        if (sr != null)
        {
            Color c = sr.color;
            c.a = 0.3f + Mathf.Sin(Time.time * pulseSpeed * 2f) * 0.2f;
            sr.color = c;
        }
    }
}

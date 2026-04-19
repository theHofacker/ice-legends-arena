using UnityEngine;

/// <summary>
/// Gunslinger ability: Trick Shot
/// Next shot ricochets off boards at sharp angles
/// The puck becomes unpredictable and can curve around defenders
/// </summary>
public class TrickShot : Ability
{
    [Header("Trick Shot Settings")]
    [Tooltip("Number of ricochets before trick shot wears off")]
    [Range(1, 5)]
    [SerializeField] private int maxRicochets = 3;

    [Tooltip("Bounciness multiplier for trick shot (1.0 = normal, 2.0 = very bouncy)")]
    [Range(1f, 2f)]
    [SerializeField] private float bouncinessMultiplier = 1.5f;

    [Tooltip("Speed retained after each ricochet (1.0 = no loss, 0.8 = 20% loss)")]
    [Range(0.7f, 1.1f)]
    [SerializeField] private float ricochetSpeedRetention = 0.95f;

    [Header("Visual Settings")]
    [Tooltip("Trail color for trick shot puck")]
    [SerializeField] private Color trailColor = new Color(1f, 0.8f, 0.2f, 1f); // Golden

    // References
    private PlayerManager playerManager;

    // State
    private bool isTrickShotReady = false;

    public bool IsTrickShotReady => isTrickShotReady;

    protected override void Awake()
    {
        base.Awake();
        playerManager = FindAnyObjectByType<PlayerManager>();
    }

    protected override void ActivateAbility()
    {
        if (isTrickShotReady)
        {
            Debug.Log("Trick Shot already loaded!");
            return;
        }

        // Get controlled player
        GameObject controlledPlayer = GetControlledPlayer();
        if (controlledPlayer == null)
        {
            Debug.LogWarning("TrickShot: No controlled player found!");
            return;
        }

        // Add or activate trick shot modifier
        TrickShotModifier modifier = controlledPlayer.GetComponent<TrickShotModifier>();
        if (modifier == null)
        {
            modifier = controlledPlayer.AddComponent<TrickShotModifier>();
        }

        modifier.LoadTrickShot(maxRicochets, bouncinessMultiplier, ricochetSpeedRetention, trailColor, this);
        isTrickShotReady = true;

        Debug.Log($"TRICK SHOT LOADED! Next shot will ricochet {maxRicochets} times!");

        // Visual feedback - could add glow to puck or player
        ShowTrickShotReadyIndicator(controlledPlayer);
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
    /// Show visual indicator that trick shot is ready
    /// </summary>
    private void ShowTrickShotReadyIndicator(GameObject player)
    {
        // Add golden glow/outline to player or their stick
        SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
        if (sr == null) sr = player.GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
        {
            // Store original color and apply golden tint
            TrickShotVisual visual = player.GetComponent<TrickShotVisual>();
            if (visual == null)
            {
                visual = player.AddComponent<TrickShotVisual>();
            }
            visual.Activate(sr, trailColor);
        }
    }

    /// <summary>
    /// Called by TrickShotModifier when the shot is fired
    /// </summary>
    public void OnTrickShotFired()
    {
        isTrickShotReady = false;
        Debug.Log("Trick Shot FIRED!");
    }
}

/// <summary>
/// Modifier component that tracks trick shot state on a player
/// ShootingController checks for this to apply trick shot properties
/// </summary>
public class TrickShotModifier : MonoBehaviour
{
    public bool IsLoaded { get; private set; }
    public int MaxRicochets { get; private set; }
    public float BouncinessMultiplier { get; private set; }
    public float SpeedRetention { get; private set; }
    public Color TrailColor { get; private set; }

    private TrickShot ownerAbility;

    public void LoadTrickShot(int ricochets, float bounciness, float retention, Color color, TrickShot owner)
    {
        IsLoaded = true;
        MaxRicochets = ricochets;
        BouncinessMultiplier = bounciness;
        SpeedRetention = retention;
        TrailColor = color;
        ownerAbility = owner;

        Debug.Log($"TrickShotModifier: Loaded with {ricochets} ricochets, {bounciness}x bounce");
    }

    /// <summary>
    /// Called when a shot is fired - applies trick shot to puck and consumes charge
    /// </summary>
    public void ApplyToPuck(GameObject puck)
    {
        if (!IsLoaded) return;

        // Add TrickShotPuck component
        TrickShotPuck trickPuck = puck.GetComponent<TrickShotPuck>();
        if (trickPuck == null)
        {
            trickPuck = puck.AddComponent<TrickShotPuck>();
        }

        trickPuck.Activate(MaxRicochets, BouncinessMultiplier, SpeedRetention, TrailColor);

        // Consume the trick shot
        IsLoaded = false;

        // Notify the ability
        if (ownerAbility != null)
        {
            ownerAbility.OnTrickShotFired();
        }

        // Remove visual indicator
        TrickShotVisual visual = GetComponent<TrickShotVisual>();
        if (visual != null)
        {
            visual.Deactivate();
        }

        Debug.Log("TrickShotModifier: Applied to puck, charge consumed!");
    }
}

/// <summary>
/// Component added to puck when trick shot is active
/// Handles enhanced ricochet behavior
/// </summary>
public class TrickShotPuck : MonoBehaviour
{
    private int remainingRicochets;
    private float bouncinessMultiplier;
    private float speedRetention;
    private Color trailColor;
    private bool isActive;

    private Rigidbody rb;
    private TrailRenderer trailRenderer;
    private float originalBounciness;
    private PhysicsMaterial originalMaterial;

    public void Activate(int ricochets, float bounciness, float retention, Color color)
    {
        remainingRicochets = ricochets;
        bouncinessMultiplier = bounciness;
        speedRetention = retention;
        trailColor = color;
        isActive = true;

        rb = GetComponent<Rigidbody>();

        // Add trail effect
        AddTrailEffect();

        // Modify physics material for extra bounce
        ModifyBounciness();

        Debug.Log($"TrickShotPuck activated: {ricochets} ricochets remaining");
    }

    private void AddTrailEffect()
    {
        // Add golden trail
        trailRenderer = gameObject.GetComponent<TrailRenderer>();
        if (trailRenderer == null)
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
        }

        trailRenderer.time = 0.3f;
        trailRenderer.startWidth = 0.3f;
        trailRenderer.endWidth = 0.05f;
        trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
        trailRenderer.startColor = trailColor;
        trailRenderer.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
        trailRenderer.sortingOrder = 10;
    }

    private void ModifyBounciness()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && col.sharedMaterial != null)
        {
            // Store original and create modified material
            originalMaterial = col.sharedMaterial;
            originalBounciness = col.sharedMaterial.bounciness;

            PhysicsMaterial trickMaterial = new PhysicsMaterial("TrickShotMaterial");
            trickMaterial.bounciness = Mathf.Min(originalBounciness * bouncinessMultiplier, 1f);
            trickMaterial.dynamicFriction = col.sharedMaterial.dynamicFriction * 0.5f; // Less friction for smoother ricochets
            trickMaterial.staticFriction = col.sharedMaterial.staticFriction * 0.5f;

            col.sharedMaterial = trickMaterial;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive) return;

        // Check if we hit a board/wall (use name check to avoid tag-not-defined errors)
        bool hitBoard = collision.gameObject.name.Contains("Board") ||
                        collision.gameObject.name.Contains("Wall") ||
                        collision.gameObject.name.Contains("Rink") ||
                        collision.gameObject.name.Contains("Boundary") ||
                        collision.gameObject.layer == LayerMask.NameToLayer("Boards");

        // Also check tag if it exists (wrapped to avoid errors)
        try
        {
            if (collision.gameObject.CompareTag("Board") || collision.gameObject.CompareTag("Wall"))
            {
                hitBoard = true;
            }
        }
        catch { /* Tag doesn't exist, ignore */ }

        if (hitBoard)
        {
            remainingRicochets--;
            Debug.Log($"TRICK SHOT RICOCHET! {remainingRicochets} remaining");

            // Apply speed retention
            if (rb != null)
            {
                Vector3 vel = rb.linearVelocity;
                vel *= speedRetention;

                // Add slight random angle variation on XZ plane for unpredictability
                float randomAngle = Random.Range(-10f, 10f);
                vel = Quaternion.Euler(0, randomAngle, 0) * vel;
                rb.linearVelocity = vel;
            }

            // Flash effect on ricochet
            StartCoroutine(RicochetFlash());

            if (remainingRicochets <= 0)
            {
                Deactivate();
            }
        }
    }

    private System.Collections.IEnumerator RicochetFlash()
    {
        // Brief bright flash on ricochet
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color originalColor = sr.color;
            sr.color = Color.yellow;
            yield return new WaitForSeconds(0.1f);
            sr.color = originalColor;
        }
    }

    private void Deactivate()
    {
        isActive = false;
        Debug.Log("Trick Shot effect ended");

        // Remove trail
        if (trailRenderer != null)
        {
            Destroy(trailRenderer);
        }

        // Restore original physics
        Collider col = GetComponent<Collider>();
        if (col != null && originalMaterial != null)
        {
            col.sharedMaterial = originalMaterial;
        }

        // Remove this component
        Destroy(this);
    }

    private void OnDestroy()
    {
        // Clean up trail if still exists
        if (trailRenderer != null)
        {
            Destroy(trailRenderer);
        }
    }
}

/// <summary>
/// Visual indicator that trick shot is ready
/// </summary>
public class TrickShotVisual : MonoBehaviour
{
    private SpriteRenderer targetRenderer;
    private Color originalColor;
    private Color glowColor;
    private bool isActive;

    public void Activate(SpriteRenderer sr, Color color)
    {
        targetRenderer = sr;
        originalColor = sr.color;
        glowColor = color;
        isActive = true;
    }

    public void Deactivate()
    {
        isActive = false;
        if (targetRenderer != null)
        {
            targetRenderer.color = originalColor;
        }
        Destroy(this);
    }

    private void Update()
    {
        if (!isActive || targetRenderer == null) return;

        // Pulsing glow effect
        float pulse = 0.7f + Mathf.Sin(Time.time * 4f) * 0.3f;
        targetRenderer.color = Color.Lerp(originalColor, glowColor, pulse * 0.3f);
    }

    private void OnDestroy()
    {
        if (targetRenderer != null)
        {
            targetRenderer.color = originalColor;
        }
    }
}

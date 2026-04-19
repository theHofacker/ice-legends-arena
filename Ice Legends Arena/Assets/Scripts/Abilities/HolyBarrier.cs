using UnityEngine;

/// <summary>
/// Paladin ability: Holy Barrier
/// Creates a holy wall of light that blocks pucks and opponents
/// Perfect for defensive plays and protecting the goal
/// </summary>
public class HolyBarrier : Ability
{
    [Header("Barrier Settings")]
    [Tooltip("Distance in front of player to spawn barrier")]
    [Range(1f, 5f)]
    [SerializeField] private float spawnDistance = 2f;

    [Tooltip("Width of the barrier")]
    [Range(2f, 10f)]
    [SerializeField] private float barrierWidth = 6f;

    [Tooltip("Height of the barrier")]
    [Range(1f, 5f)]
    [SerializeField] private float barrierHeight = 3f;

    [Tooltip("Thickness of the barrier collider")]
    [Range(0.2f, 1f)]
    [SerializeField] private float barrierThickness = 0.5f;

    [Header("Visual Settings")]
    [Tooltip("Color of the barrier")]
    [SerializeField] private Color barrierColor = new Color(1f, 0.85f, 0.3f, 0.7f); // Golden

    [Tooltip("Barrier prefab (optional - will create placeholder if not set)")]
    [SerializeField] private GameObject barrierPrefab;

    // Reference to PlayerManager for finding controlled player
    private PlayerManager playerManager;

    // Currently active barrier (only one at a time)
    private GameObject activeBarrier;

    protected override void Awake()
    {
        base.Awake();
        playerManager = FindAnyObjectByType<PlayerManager>();
        if (playerManager == null)
        {
            Debug.LogWarning("HolyBarrier: Could not find PlayerManager!");
        }
    }

    protected override void ActivateAbility()
    {
        // Destroy any existing barrier first
        if (activeBarrier != null)
        {
            Destroy(activeBarrier);
        }

        // Get controlled player
        GameObject controlledPlayer = GetControlledPlayer();
        if (controlledPlayer == null)
        {
            Debug.LogWarning("HolyBarrier: No controlled player found!");
            return;
        }

        // Calculate spawn position (in front of player based on their facing/movement)
        Vector3 spawnPosition = GetBarrierSpawnPosition(controlledPlayer);
        float rotationY = GetBarrierRotation(controlledPlayer);

        Debug.Log($"Holy Barrier activated! Spawning at {spawnPosition}");

        // Create the barrier
        activeBarrier = CreateBarrier(spawnPosition, rotationY);

        // Schedule destruction after duration
        float duration = abilityData != null ? abilityData.duration : 4f;
        Destroy(activeBarrier, duration);

        Debug.Log($"Holy Barrier will last {duration} seconds");
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
    /// Calculate where to spawn the barrier (in front of player)
    /// </summary>
    private Vector3 GetBarrierSpawnPosition(GameObject player)
    {
        Vector3 playerPos = player.transform.position;
        Rigidbody rb = player.GetComponent<Rigidbody>();

        // Determine facing direction on XZ plane
        Vector3 facingDirection;

        if (rb != null && PhysicsHelper.SpeedXZ(rb.linearVelocity) > 0.5f)
        {
            // Use movement direction if moving
            facingDirection = PhysicsHelper.FlattenY(rb.linearVelocity).normalized;
        }
        else
        {
            // Default to facing along +X (toward opponent goal) or use sprite flip
            SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
            if (sr == null) sr = player.GetComponentInChildren<SpriteRenderer>();

            if (sr != null && sr.flipX)
            {
                facingDirection = Vector3.left; // -X
            }
            else
            {
                facingDirection = Vector3.right; // +X
            }
        }

        Vector3 spawnPos = playerPos + facingDirection * spawnDistance;
        spawnPos.y = 0f; // Keep on ice surface
        return spawnPos;
    }

    /// <summary>
    /// Calculate barrier rotation around Y axis (perpendicular to player facing on XZ plane)
    /// Returns angle in degrees for Y-axis rotation
    /// </summary>
    private float GetBarrierRotation(GameObject player)
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();

        if (rb != null && PhysicsHelper.SpeedXZ(rb.linearVelocity) > 0.5f)
        {
            // Make barrier perpendicular to movement direction on XZ plane
            Vector3 dir = PhysicsHelper.FlattenY(rb.linearVelocity).normalized;
            // Atan2 on XZ plane, then add 90 degrees to make perpendicular
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 90f;
        }

        // Default to barrier facing along Z axis (blocking X-axis movement/shots)
        return 0f;
    }

    /// <summary>
    /// Create the barrier GameObject
    /// </summary>
    private GameObject CreateBarrier(Vector3 position, float rotationY)
    {
        GameObject barrier;

        if (barrierPrefab != null)
        {
            // Use prefab if assigned
            barrier = Instantiate(barrierPrefab, position, Quaternion.Euler(0, rotationY, 0));
        }
        else
        {
            // Create placeholder barrier
            barrier = CreatePlaceholderBarrier(position, rotationY);
        }

        // Add the barrier behavior component
        BarrierBehavior behavior = barrier.AddComponent<BarrierBehavior>();
        behavior.Initialize(barrierColor);

        return barrier;
    }

    /// <summary>
    /// Create a placeholder barrier with visual and collider
    /// </summary>
    private GameObject CreatePlaceholderBarrier(Vector3 position, float rotationY)
    {
        GameObject barrier = new GameObject("HolyBarrier");
        barrier.transform.position = new Vector3(position.x, barrierHeight * 0.5f, position.z);
        barrier.transform.rotation = Quaternion.Euler(0, rotationY, 0);

        // Add visual (sprite renderer with procedural texture - will be replaced by 3D visuals later)
        SpriteRenderer sr = barrier.AddComponent<SpriteRenderer>();
        sr.sprite = CreateBarrierSprite();
        sr.color = barrierColor;
        sr.sortingOrder = 50;

        // Scale sprite to match desired barrier size
        // The sprite is 32x128 pixels, we want barrierWidth x barrierHeight world units
        float spriteWidth = 32f / 32f; // 1 unit
        float spriteHeight = 128f / 32f; // 4 units
        barrier.transform.localScale = new Vector3(
            barrierWidth / spriteWidth,
            barrierHeight / spriteHeight,
            1f
        );

        // Add 3D box collider for blocking
        BoxCollider collider = barrier.AddComponent<BoxCollider>();
        collider.size = new Vector3(spriteWidth, spriteHeight, barrierThickness);

        // Add rigidbody (kinematic so it doesn't move but still collides)
        Rigidbody rb = barrier.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        Debug.Log($"Holy Barrier created: {barrierWidth}x{barrierHeight} units, rotation {rotationY}");

        return barrier;
    }

    /// <summary>
    /// Create a simple rectangular sprite for the barrier
    /// </summary>
    private Sprite CreateBarrierSprite()
    {
        int width = 32;
        int height = 128;
        Texture2D texture = new Texture2D(width, height);
        Color[] colors = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Create gradient effect (brighter in center)
                float centerDist = Mathf.Abs(x - width / 2f) / (width / 2f);
                float alpha = 1f - (centerDist * 0.5f);

                // Add some vertical variation for visual interest
                float verticalWave = Mathf.Sin(y * 0.1f) * 0.1f + 0.9f;

                colors[y * width + x] = new Color(1f, 1f, 1f, alpha * verticalWave);
            }
        }

        texture.SetPixels(colors);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;

        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
    }
}

/// <summary>
/// Behavior component for the barrier - handles visual effects and collision
/// </summary>
public class BarrierBehavior : MonoBehaviour
{
    private Color baseColor;
    private SpriteRenderer spriteRenderer;
    private float pulseSpeed = 2f;
    private float lifetime = 0f;

    public void Initialize(Color color)
    {
        baseColor = color;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        lifetime += Time.deltaTime;

        // Pulsing glow effect
        if (spriteRenderer != null)
        {
            float pulse = 0.7f + Mathf.Sin(lifetime * pulseSpeed) * 0.3f;
            Color c = baseColor;
            c.a = baseColor.a * pulse;
            spriteRenderer.color = c;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Flash brighter when something hits the barrier
        if (spriteRenderer != null)
        {
            StartCoroutine(FlashOnHit());
        }

        // Log what hit the barrier
        if (collision.gameObject.CompareTag("Puck"))
        {
            Debug.Log("Holy Barrier blocked the puck!");
        }
        else if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponent<AIController>() != null)
        {
            Debug.Log($"Holy Barrier blocked {collision.gameObject.name}!");
        }
    }

    private System.Collections.IEnumerator FlashOnHit()
    {
        Color flashColor = Color.white;
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = baseColor;
    }

    private void OnDestroy()
    {
        Debug.Log("Holy Barrier faded away!");
    }
}

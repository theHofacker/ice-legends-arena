using UnityEngine;
using System.Collections;

/// <summary>
/// Elementalist ability: Meteor Strike
/// Summons a meteor that crashes down at cursor/target location, stunning nearby opponents
/// Example implementation showing how to inherit from Ability base class
/// </summary>
public class MeteorStrike : Ability
{
    [Header("Meteor Strike Settings")]
    [Tooltip("Meteor prefab to spawn")]
    [SerializeField] private GameObject meteorPrefab;

    [Tooltip("Explosion radius (stun area)")]
    [Range(3f, 10f)]
    [SerializeField] private float explosionRadius = 5f;

    [Tooltip("Stun duration")]
    [Range(1f, 5f)]
    [SerializeField] private float stunDuration = 2f;

    [Tooltip("Damage dealt to puck carrier")]
    [Range(0f, 50f)]
    [SerializeField] private float damage = 25f;

    [Header("Visual Effects")]
    [Tooltip("Explosion particle effect")]
    [SerializeField] private GameObject explosionEffect;

    [Tooltip("Sound effect for meteor impact")]
    [SerializeField] private AudioClip impactSound;

    [Header("Timing")]
    [Tooltip("Delay before meteor impacts (warning time)")]
    [Range(0.3f, 2f)]
    [SerializeField] private float impactDelay = 0.8f;

    [Header("Targeting")]
    [Tooltip("How far behind the player to drop the meteor (for catching pursuers)")]
    [Range(0f, 3f)]
    [SerializeField] private float dropBehindDistance = 1f;

    // Cached reference to PlayerManager
    private PlayerManager playerManager;

    protected override void Awake()
    {
        base.Awake();

        // Find the PlayerManager to get the currently controlled player
        playerManager = FindAnyObjectByType<PlayerManager>();
        if (playerManager == null)
        {
            Debug.LogWarning("MeteorStrike: Could not find PlayerManager!");
        }
    }

    protected override void ActivateAbility()
    {
        Vector3 targetPosition = GetTargetPosition();
        Debug.Log($"Meteor Strike activated at {targetPosition}! (Player at {GetPlayerPosition()})");

        // Start the meteor strike sequence (warning -> impact -> stun)
        StartCoroutine(MeteorStrikeSequence(targetPosition));
    }

    /// <summary>
    /// Get the CURRENTLY CONTROLLED player's position (not cached, real-time)
    /// </summary>
    private Vector3 GetPlayerPosition()
    {
        GameObject controlledPlayer = GetControlledPlayer();
        if (controlledPlayer != null)
        {
            return controlledPlayer.transform.position;
        }
        return transform.position; // Fallback
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
        // Fallback: find any player
        return GameObject.FindGameObjectWithTag("Player");
    }

    /// <summary>
    /// Get target position for meteor - drops behind player to catch pursuers
    /// Perfect for creating breakaways!
    /// </summary>
    private Vector3 GetTargetPosition()
    {
        GameObject controlledPlayer = GetControlledPlayer();
        if (controlledPlayer == null)
        {
            Debug.LogWarning("MeteorStrike: No controlled player found!");
            return transform.position;
        }

        Vector3 playerPosition = controlledPlayer.transform.position;
        Rigidbody playerRb = controlledPlayer.GetComponent<Rigidbody>();

        // Get movement direction from rigidbody velocity
        if (playerRb != null && PhysicsHelper.SpeedXZ(playerRb.linearVelocity) > 0.5f)
        {
            // Player is moving - drop meteor BEHIND them (opposite of movement direction)
            Vector3 movementDirection = PhysicsHelper.FlattenY(playerRb.linearVelocity).normalized;
            Vector3 targetPos = playerPosition - movementDirection * dropBehindDistance;
            targetPos.y = 0f; // Keep on ice surface
            Debug.Log($"MeteorStrike targeting: {controlledPlayer.name} moving {movementDirection}, dropping at {targetPos}");
            return targetPos;
        }
        else
        {
            // Player is stationary - drop meteor right at their position
            Debug.Log($"MeteorStrike targeting: {controlledPlayer.name} stationary at {playerPosition}");
            return playerPosition;
        }
    }

    /// <summary>
    /// Meteor strike sequence: warning indicator -> impact -> stun
    /// </summary>
    private IEnumerator MeteorStrikeSequence(Vector3 targetPosition)
    {
        // Phase 1: Show warning indicator
        GameObject warningIndicator = CreateWarningIndicator(targetPosition);

        // Wait for impact
        yield return new WaitForSeconds(impactDelay);

        // Phase 2: Destroy warning, create explosion
        if (warningIndicator != null)
            Destroy(warningIndicator);

        SpawnMeteorExplosion(targetPosition);

        // Phase 3: Stun nearby opponents
        StunNearbyOpponents(targetPosition);
    }

    /// <summary>
    /// Create a warning indicator showing where meteor will land
    /// </summary>
    private GameObject CreateWarningIndicator(Vector3 position)
    {
        // Create warning circle (red pulsing ring)
        GameObject warning = new GameObject("MeteorWarning");
        warning.transform.position = new Vector3(position.x, 0.01f, position.z); // Slightly above ice

        // Add sprite renderer for the warning circle (will be replaced by 3D visuals later)
        SpriteRenderer sr = warning.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(1f, 0.3f, 0f, 0.5f); // Orange, semi-transparent
        sr.sortingOrder = 100; // Render on top

        // Rotate to lay flat on the XZ plane (ice surface)
        warning.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Scale to match explosion radius
        warning.transform.localScale = Vector3.one * explosionRadius * 2f;

        // Add pulsing animation
        warning.AddComponent<WarningPulse>();

        return warning;
    }

    /// <summary>
    /// Spawn meteor explosion effect
    /// </summary>
    private void SpawnMeteorExplosion(Vector3 position)
    {
        // Use custom prefab if assigned, otherwise create placeholder
        if (explosionEffect != null)
        {
            GameObject explosion = Instantiate(explosionEffect, position, Quaternion.identity);
            Destroy(explosion, 3f);
        }
        else
        {
            // Create placeholder explosion effect
            GameObject explosion = CreateExplosionEffect(position);
            Destroy(explosion, 1f);
        }

        // Play impact sound
        if (impactSound != null)
        {
            AudioSource.PlayClipAtPoint(impactSound, position);
        }

        // Screen shake effect (if camera exists)
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            StartCoroutine(ScreenShake(mainCam, 0.3f, 0.2f));
        }

        Debug.Log($"METEOR IMPACT at {position}!");
    }

    /// <summary>
    /// Create placeholder explosion visual
    /// </summary>
    private GameObject CreateExplosionEffect(Vector3 position)
    {
        GameObject explosion = new GameObject("MeteorExplosion");
        explosion.transform.position = new Vector3(position.x, 0.01f, position.z);

        // Create expanding ring (will be replaced by 3D visuals later)
        SpriteRenderer sr = explosion.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(1f, 0.5f, 0f, 0.8f); // Orange
        sr.sortingOrder = 101;

        // Rotate to lay flat on the XZ plane
        explosion.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Add expansion animation
        explosion.AddComponent<ExplosionExpand>().Initialize(explosionRadius * 2.5f);

        return explosion;
    }

    /// <summary>
    /// Create a simple circle sprite at runtime
    /// </summary>
    private Sprite CreateCircleSprite()
    {
        int resolution = 64;
        Texture2D texture = new Texture2D(resolution, resolution);
        Color[] colors = new Color[resolution * resolution];

        Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
        float radius = resolution / 2f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist < radius && dist > radius - 4)
                {
                    colors[y * resolution + x] = Color.white;
                }
                else if (dist < radius - 4)
                {
                    colors[y * resolution + x] = new Color(1, 1, 1, 0.3f);
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
    /// Simple screen shake effect
    /// </summary>
    private IEnumerator ScreenShake(Camera cam, float duration, float magnitude)
    {
        Vector3 originalPos = cam.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            cam.transform.localPosition = originalPos + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.transform.localPosition = originalPos;
    }

    /// <summary>
    /// Stun all opponents within explosion radius
    /// </summary>
    private void StunNearbyOpponents(Vector3 position)
    {
        AIController[] opponents = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        int stunnedCount = 0;

        foreach (AIController opponent in opponents)
        {
            float distance = PhysicsHelper.DistanceXZ(opponent.transform.position, position);

            if (distance <= explosionRadius)
            {
                // Use the new stun system
                opponent.Stun(stunDuration);
                stunnedCount++;
            }
        }

        Debug.Log($"Meteor Strike stunned {stunnedCount} opponents!");
    }

    /// <summary>
    /// Visualize explosion radius in editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Vector3 targetPos = GetTargetPosition();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPos, explosionRadius);
        }
    }
}

/// <summary>
/// Simple pulsing animation for warning indicator
/// </summary>
public class WarningPulse : MonoBehaviour
{
    private float pulseSpeed = 4f;
    private Vector3 baseScale;

    private void Start()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.15f;
        transform.localScale = baseScale * pulse;

        // Also pulse alpha
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            float alpha = 0.3f + Mathf.Sin(Time.time * pulseSpeed * 2f) * 0.2f;
            sr.color = new Color(1f, 0.3f, 0f, alpha);
        }
    }
}

/// <summary>
/// Expanding explosion animation
/// </summary>
public class ExplosionExpand : MonoBehaviour
{
    private float targetScale;
    private float expandSpeed = 8f;
    private SpriteRenderer sr;

    public void Initialize(float scale)
    {
        targetScale = scale;
        transform.localScale = Vector3.zero;
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        // Expand
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, expandSpeed * Time.deltaTime);

        // Fade out
        if (sr != null)
        {
            Color c = sr.color;
            c.a = Mathf.Lerp(c.a, 0f, 3f * Time.deltaTime);
            sr.color = c;
        }
    }
}

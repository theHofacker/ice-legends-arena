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
    [Tooltip("Delay before meteor impacts (warning time). Also the fall duration: the " +
             "meteor descends from meteorSpawnHeight to the ground over this window.")]
    [Range(0.3f, 2f)]
    [SerializeField] private float impactDelay = 0.8f;

    [Tooltip("Height (m) above the target the meteor spawns and falls from over impactDelay. " +
             "Requires meteorPrefab assigned (e.g. Projectile_Fire_LWRP from the Spells Pack).")]
    [Range(3f, 40f)]
    [SerializeField] private float meteorSpawnHeight = 18f;

    [Header("Targeting")]
    [Tooltip("How far behind the player to drop the meteor (for catching pursuers)")]
    [Range(0f, 3f)]
    [SerializeField] private float dropBehindDistance = 1f;

    [Header("Cast Animation")]
    [Tooltip("Normalized time (0-1) within the SpellCast clip where the caster thrusts " +
             "their hands forward. The meteor sequence (warning -> fall -> impact) begins " +
             "at this frame so it syncs with the visible cast, mirroring the shot/check " +
             "contact-sync pattern. SpellCast spans frames 2-68; the thrust lands ~mid-clip.")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float castContactNormalizedTime = 0.4f;

    [Tooltip("Failsafe (s): if the SpellCast state never reaches its contact frame within " +
             "this window (animator not rebuilt, cast interrupted), fire the strike anyway.")]
    [Range(0.2f, 2f)]
    [SerializeField] private float castContactTimeout = 1f;

    // Cached reference to PlayerManager
    private PlayerManager playerManager;

    private static readonly int CastHash = Animator.StringToHash("Cast");

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
        Debug.Log($"Meteor Strike activated! (Player at {GetPlayerPosition()})");

        // Play the cast animation and defer the strike to its thrust frame so the meteor
        // syncs with the visible cast (mirrors the shot/check contact-sync). If the player
        // has no animator or the controller predates the Cast trigger (not rebuilt), fall
        // back to firing the sequence immediately.
        GameObject player = GetControlledPlayer();
        Animator anim = player != null ? player.GetComponentInChildren<Animator>() : null;
        StickAttacher stick = player != null ? player.GetComponentInChildren<StickAttacher>() : null;

        if (anim != null && HasCastParam(anim))
        {
            anim.SetTrigger(CastHash);
            StartCoroutine(CastThenStrike(anim, stick));
        }
        else
        {
            if (anim == null)
                Debug.LogWarning("MeteorStrike: no Animator on controlled player — firing strike without a cast animation.");
            else
                Debug.LogWarning("MeteorStrike: animator has no 'Cast' trigger (rebuild HockeyPlayerAnimator) — firing strike without a cast animation.");
            StartCoroutine(MeteorStrikeSequence(GetTargetPosition()));
        }
    }

    /// <summary>
    /// True if the animator exposes the "Cast" trigger added by HockeyAnimatorBuilder.
    /// Guards SetTrigger so we don't spam errors on an un-rebuilt controller.
    /// </summary>
    private static bool HasCastParam(Animator anim)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Cast")
                return true;
        }
        return false;
    }

    /// <summary>
    /// Waits for the SpellCast clip to reach its thrust frame (castContactNormalizedTime),
    /// then launches the meteor sequence. Bails early if the cast state is interrupted, and
    /// has a hard timeout failsafe so a missing/replaced state can't swallow the ability.
    /// Target is resolved at the contact frame so "drop behind" uses the position the
    /// player has skated to by the time the cast lands.
    /// </summary>
    private IEnumerator CastThenStrike(Animator anim, StickAttacher stick)
    {
        float timeRemaining = castContactTimeout;
        bool enteredCast = false;

        // Phase A: wait for the cast's thrust frame (with failsafe). On entering SpellCast,
        // free the left hand so the stick swings one-handed like a sword (the right-hand
        // swing already lands with the meteor impact).
        while (timeRemaining > 0f)
        {
            AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("SpellCast"))
            {
                if (!enteredCast)
                {
                    enteredCast = true;
                    if (stick != null) stick.LockTopHandToBottom();
                }
                if (info.normalizedTime >= castContactNormalizedTime)
                    break;
            }
            else if (enteredCast)
            {
                // Was in SpellCast and left it (interrupted by a hit, etc.) — strike now.
                break;
            }

            timeRemaining -= Time.deltaTime;
            yield return null;
        }

        StartCoroutine(MeteorStrikeSequence(GetTargetPosition()));

        if (!enteredCast) yield break; // never cast (no anim / interrupted) — nothing to release

        // Phase B: hold the one-handed grip until the cast clip exits, then restore two-hand
        // tracking. Capped so a missed state-exit can't strand the stick in the right hand.
        float holdTimeout = 4f;
        while (holdTimeout > 0f)
        {
            if (!anim.GetCurrentAnimatorStateInfo(0).IsName("SpellCast")) break;
            holdTimeout -= Time.deltaTime;
            yield return null;
        }
        if (stick != null) stick.ReleaseTopHandLock();
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
        // Test scene (1v1) has no PlayerManager — use the simplified test controller.
        TestPlayerController testPlayer = FindAnyObjectByType<TestPlayerController>();
        if (testPlayer != null)
        {
            return testPlayer.gameObject;
        }
        // Fallback: find any player
        return GameObject.FindGameObjectWithTag("Player");
    }

    /// <summary>
    /// Finds the opponent nearest the controlled player (test scene first, then gameplay
    /// AIController). Returns its ice-level position. False if there are no opponents.
    /// </summary>
    private bool TryGetNearestOpponent(out Vector3 position)
    {
        position = Vector3.zero;
        float bestDist = float.MaxValue;
        bool found = false;
        Vector3 origin = GetPlayerPosition();

        foreach (TestOpponentController opp in FindObjectsByType<TestOpponentController>(FindObjectsSortMode.None))
        {
            float d = PhysicsHelper.DistanceXZ(opp.transform.position, origin);
            if (d < bestDist) { bestDist = d; position = opp.transform.position; found = true; }
        }
        foreach (AIController opp in FindObjectsByType<AIController>(FindObjectsSortMode.None))
        {
            float d = PhysicsHelper.DistanceXZ(opp.transform.position, origin);
            if (d < bestDist) { bestDist = d; position = opp.transform.position; found = true; }
        }

        if (found) position.y = 0f;
        return found;
    }

    /// <summary>
    /// Where the meteor lands. Primary: the nearest opponent (offensive zone strike that
    /// knocks them down). Fallback when no opponent exists: drop behind the moving player
    /// to catch pursuers / spring a breakaway (the original design intent).
    /// </summary>
    private Vector3 GetTargetPosition()
    {
        if (TryGetNearestOpponent(out Vector3 opponentPos))
        {
            Debug.Log($"MeteorStrike targeting nearest opponent at {opponentPos}");
            return opponentPos;
        }

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
        Vector3 impactPoint = new Vector3(targetPosition.x, 0f, targetPosition.z);

        // Phase 1: ground telegraph + spawn the falling meteor high above the impact point.
        GameObject warningIndicator = CreateWarningIndicator(impactPoint);

        GameObject meteor = null;
        Vector3 meteorStart = impactPoint + Vector3.up * meteorSpawnHeight;
        if (meteorPrefab != null)
        {
            // Point the prefab's forward down the fall path (most projectile VFX emit along +Z).
            Quaternion fallRot = Quaternion.LookRotation((impactPoint - meteorStart).normalized);
            meteor = Instantiate(meteorPrefab, meteorStart, fallRot);
        }

        // Phase 2: descend over impactDelay so the crash lands on the telegraph.
        float elapsed = 0f;
        while (elapsed < impactDelay)
        {
            elapsed += Time.deltaTime;
            if (meteor != null)
                meteor.transform.position = Vector3.Lerp(meteorStart, impactPoint, elapsed / impactDelay);
            yield return null;
        }

        // Phase 3: land — clear telegraph + meteor, explode, knock down opponents in radius.
        if (warningIndicator != null) Destroy(warningIndicator);
        if (meteor != null) Destroy(meteor);

        SpawnMeteorExplosion(impactPoint);
        StunNearbyOpponents(impactPoint);
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
    /// Knock down / stun all opponents within the explosion radius.
    /// Test scene: reuse the Heavy-check fall via GetBodyChecked, with knockback radiating
    /// outward from the blast center so bodies are thrown away from the impact — no new
    /// opponent code needed, and the knockdown reads as a real meteor hit.
    /// Gameplay scene: fall back to AIController.Stun (original behavior).
    /// </summary>
    private void StunNearbyOpponents(Vector3 position)
    {
        int hitCount = 0;

        // --- Test-scene opponents: radial knockdown via the existing Heavy fall ---
        TestOpponentController[] testOpponents = FindObjectsByType<TestOpponentController>(FindObjectsSortMode.None);
        foreach (TestOpponentController opponent in testOpponents)
        {
            if (opponent.IsStunned) continue; // don't restart a fall mid-recovery
            float distance = PhysicsHelper.DistanceXZ(opponent.transform.position, position);
            if (distance > explosionRadius) continue;

            // Direction FROM the blast center TO the opponent = outward push.
            Vector3 outward = PhysicsHelper.DirectionXZ(position, opponent.transform.position);
            if (outward.sqrMagnitude < 0.0001f) outward = opponent.transform.forward; // dead-center safety
            opponent.GetBodyChecked(TestOpponentController.CheckTier.Heavy, outward);
            hitCount++;
        }

        // --- Gameplay-scene opponents: original stun system ---
        AIController[] aiOpponents = FindObjectsByType<AIController>(FindObjectsSortMode.None);
        foreach (AIController opponent in aiOpponents)
        {
            float distance = PhysicsHelper.DistanceXZ(opponent.transform.position, position);
            if (distance <= explosionRadius)
            {
                opponent.Stun(stunDuration);
                hitCount++;
            }
        }

        Debug.Log($"Meteor Strike hit {hitCount} opponents!");
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

using UnityEngine;
using System.Collections;

/// <summary>
/// Summoner ability: Carbuncle Call
/// Summons a magical Carbuncle creature that follows the puck
/// Acts as a mobile obstacle that blocks and redirects the puck
/// </summary>
public class CarbuncleCall : Ability
{
    [Header("Carbuncle Settings")]
    [Tooltip("Speed at which Carbuncle chases the puck")]
    [Range(3f, 10f)]
    [SerializeField] private float chaseSpeed = 6f;

    [Tooltip("Distance from puck where Carbuncle stops")]
    [Range(0.5f, 3f)]
    [SerializeField] private float stopDistance = 1.5f;

    [Tooltip("Size of the Carbuncle")]
    [Range(0.5f, 2f)]
    [SerializeField] private float carbuncleSize = 1f;

    [Header("Visual Settings")]
    [Tooltip("Color of the Carbuncle")]
    [SerializeField] private Color carbuncleColor = new Color(0.4f, 0.9f, 0.6f, 1f); // Teal/Cyan

    // References
    private PlayerManager playerManager;
    private GameObject activeCarbuncle;

    protected override void Awake()
    {
        base.Awake();
        playerManager = FindAnyObjectByType<PlayerManager>();
    }

    protected override void ActivateAbility()
    {
        // Destroy existing carbuncle if any
        if (activeCarbuncle != null)
        {
            Destroy(activeCarbuncle);
        }

        // Get spawn position (near controlled player)
        GameObject controlledPlayer = GetControlledPlayer();
        Vector3 spawnPos = controlledPlayer != null
            ? controlledPlayer.transform.position + Vector3.right * 2f
            : Vector3.zero;
        spawnPos.y = 0f; // Keep on ice surface

        // Create the Carbuncle
        activeCarbuncle = CreateCarbuncle(spawnPos);

        // Set duration
        float duration = abilityData != null ? abilityData.duration : 8f;
        StartCoroutine(CarbuncleLifetime(duration));

        Debug.Log($"CARBUNCLE SUMMONED! Will follow puck for {duration} seconds!");
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
    /// Create the Carbuncle creature
    /// </summary>
    private GameObject CreateCarbuncle(Vector3 position)
    {
        GameObject carbuncle = new GameObject("Carbuncle");
        carbuncle.transform.position = position;
        carbuncle.transform.localScale = Vector3.one * carbuncleSize;

        // Add sprite (diamond/crystal shape - will be replaced by 3D visuals later)
        SpriteRenderer sr = carbuncle.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCarbuncleSprite();
        sr.color = carbuncleColor;
        sr.sortingOrder = 20;

        // Add collider for blocking (3D sphere collider)
        SphereCollider collider = carbuncle.AddComponent<SphereCollider>();
        collider.radius = 0.5f;

        // Add rigidbody for physics (3D)
        Rigidbody rb = carbuncle.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.mass = 2f; // Heavier than puck so it can block
        rb.linearDamping = 3f;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        // Add AI behavior
        CarbuncleAI ai = carbuncle.AddComponent<CarbuncleAI>();
        ai.Initialize(chaseSpeed, stopDistance, carbuncleColor);

        // Spawn effect
        StartCoroutine(SpawnEffect(carbuncle));

        return carbuncle;
    }

    /// <summary>
    /// Create a simple diamond-shaped sprite for the Carbuncle
    /// </summary>
    private Sprite CreateCarbuncleSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Diamond shape
                float dx = Mathf.Abs(x - center.x) / (size / 2f);
                float dy = Mathf.Abs(y - center.y) / (size / 2f);

                if (dx + dy < 0.8f)
                {
                    // Inner glow gradient
                    float dist = dx + dy;
                    float alpha = 1f - (dist / 0.8f) * 0.3f;
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    colors[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// Spawn animation effect
    /// </summary>
    private IEnumerator SpawnEffect(GameObject carbuncle)
    {
        if (carbuncle == null) yield break;

        Vector3 targetScale = carbuncle.transform.localScale;
        carbuncle.transform.localScale = Vector3.zero;

        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration && carbuncle != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float bounce = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            carbuncle.transform.localScale = targetScale * t * bounce;
            yield return null;
        }

        if (carbuncle != null)
            carbuncle.transform.localScale = targetScale;
    }

    /// <summary>
    /// Carbuncle lifetime management
    /// </summary>
    private IEnumerator CarbuncleLifetime(float duration)
    {
        yield return new WaitForSeconds(duration - 1f);

        // Warning flash before disappearing
        if (activeCarbuncle != null)
        {
            SpriteRenderer sr = activeCarbuncle.GetComponent<SpriteRenderer>();
            float flashTime = 1f;
            float elapsed = 0f;

            while (elapsed < flashTime && activeCarbuncle != null)
            {
                elapsed += Time.deltaTime;
                if (sr != null)
                {
                    float flash = Mathf.PingPong(elapsed * 8f, 1f);
                    Color c = carbuncleColor;
                    c.a = flash;
                    sr.color = c;
                }
                yield return null;
            }

            if (activeCarbuncle != null)
            {
                Debug.Log("Carbuncle disappeared!");
                Destroy(activeCarbuncle);
            }
        }
    }

    private void OnDestroy()
    {
        if (activeCarbuncle != null)
        {
            Destroy(activeCarbuncle);
        }
    }
}

/// <summary>
/// AI behavior for the Carbuncle creature
/// Chases the puck and acts as a mobile obstacle
/// </summary>
public class CarbuncleAI : MonoBehaviour
{
    private float chaseSpeed;
    private float stopDistance;
    private Color baseColor;
    private Transform puckTransform;
    private Rigidbody rb;
    private SpriteRenderer sr;

    public void Initialize(float speed, float stopDist, Color color)
    {
        chaseSpeed = speed;
        stopDistance = stopDist;
        baseColor = color;

        rb = GetComponent<Rigidbody>();
        sr = GetComponent<SpriteRenderer>();

        // Find puck
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckTransform = puck.transform;
        }
    }

    private void Update()
    {
        if (puckTransform == null) return;

        // Calculate direction to puck on XZ plane
        Vector3 toPuck = PhysicsHelper.DirectionXZ(transform.position, puckTransform.position);
        float distance = PhysicsHelper.DistanceXZ(transform.position, puckTransform.position);

        if (distance > stopDistance)
        {
            // Move toward puck on XZ plane
            Vector3 moveVel = toPuck * chaseSpeed;
            moveVel.y = 0f;
            rb.linearVelocity = moveVel;

            // Rotate to face movement direction around Y axis
            float angle = Mathf.Atan2(toPuck.x, toPuck.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Lerp(transform.rotation,
                Quaternion.Euler(0, angle, 0), Time.deltaTime * 10f);
        }
        else
        {
            // Slow down near puck
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * 5f);
        }

        // Gentle bobbing animation
        if (sr != null)
        {
            float bob = Mathf.Sin(Time.time * 3f) * 0.1f;
            Color c = baseColor;
            c.a = 0.8f + bob;
            sr.color = c;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Puck"))
        {
            Debug.Log("Carbuncle blocked/redirected the puck!");

            // Flash effect
            StartCoroutine(FlashOnContact());
        }
    }

    private System.Collections.IEnumerator FlashOnContact()
    {
        if (sr != null)
        {
            Color original = sr.color;
            sr.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            sr.color = original;
        }
    }
}

using UnityEngine;

/// <summary>
/// Gunslinger ability: Trick Shot (loader).
/// Activating ARMS the player (optional golden aura). The next shot fired becomes a puck that
/// ricochets off the boards several times at sharp, slightly randomized angles before the
/// effect wears off. The shot pipeline (TestPuckController.FireTimedShot) stamps the armed
/// trick shot onto the puck on release, so it rides the player's normal Space shot.
///
/// This is a "loader" ability — distinct from instant casts like MeteorStrike. There's no
/// projectile to spawn or target; the payoff is the player's next shot.
/// </summary>
public class TrickShot : Ability
{
    [Header("Trick Shot Settings")]
    [Tooltip("Number of board ricochets before the trick shot wears off")]
    [Range(1, 5)]
    [SerializeField] private int maxRicochets = 3;

    [Tooltip("Bounciness multiplier for the trick shot puck (1.0 = normal, 2.0 = very bouncy)")]
    [Range(1f, 2f)]
    [SerializeField] private float bouncinessMultiplier = 1.5f;

    [Tooltip("Speed retained after each ricochet (1.0 = no loss, 0.8 = 20% loss)")]
    [Range(0.7f, 1.1f)]
    [SerializeField] private float ricochetSpeedRetention = 0.95f;

    [Header("Visuals")]
    [Tooltip("Trail color for the trick shot puck")]
    [SerializeField] private Color trailColor = new Color(1f, 0.8f, 0.2f, 1f); // Golden

    [Tooltip("Optional VFX spawned on the player while armed (e.g. a mobilized Spells Pack " +
             "aura/buff). Parented to the player root; destroyed when the armed shot fires or " +
             "the ability is re-armed.")]
    [SerializeField] private GameObject armedEffect;

    private PlayerManager playerManager;
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

        GameObject player = GetControlledPlayer();
        if (player == null)
        {
            Debug.LogWarning("TrickShot: No controlled player found!");
            return;
        }

        TrickShotModifier modifier = player.GetComponent<TrickShotModifier>();
        if (modifier == null) modifier = player.AddComponent<TrickShotModifier>();

        GameObject armed = SpawnArmedEffect(player);
        modifier.LoadTrickShot(maxRicochets, bouncinessMultiplier, ricochetSpeedRetention, trailColor, armed, this);
        isTrickShotReady = true;

        Debug.Log($"TRICK SHOT LOADED! Next shot ricochets {maxRicochets}x.");
    }

    /// <summary>
    /// Currently controlled player. Gameplay scene uses PlayerManager; the 1v1 test scene has
    /// none, so fall back to the TestPlayerController, then the Player tag.
    /// </summary>
    private GameObject GetControlledPlayer()
    {
        if (playerManager != null && playerManager.CurrentPlayer != null)
            return playerManager.CurrentPlayer;
        TestPlayerController testPlayer = FindAnyObjectByType<TestPlayerController>();
        if (testPlayer != null) return testPlayer.gameObject;
        return GameObject.FindGameObjectWithTag("Player");
    }

    /// <summary>Spawns the armed aura parented to the player root (no bone scaling needed).</summary>
    private GameObject SpawnArmedEffect(GameObject player)
    {
        if (armedEffect == null) return null;
        GameObject fx = Instantiate(armedEffect, player.transform);
        fx.transform.localPosition = Vector3.zero;
        fx.transform.localRotation = Quaternion.identity;
        return fx;
    }

    /// <summary>Called by TrickShotModifier when the armed shot is consumed.</summary>
    public void OnTrickShotFired()
    {
        isTrickShotReady = false;
        Debug.Log("Trick Shot FIRED!");
    }
}

/// <summary>
/// Marker component the shooter carries while a trick shot is armed. TestPuckController checks
/// for this on the firing player and calls ApplyToPuck when the shot leaves the stick.
/// </summary>
public class TrickShotModifier : MonoBehaviour
{
    public bool IsLoaded { get; private set; }
    public int MaxRicochets { get; private set; }
    public float BouncinessMultiplier { get; private set; }
    public float SpeedRetention { get; private set; }
    public Color TrailColor { get; private set; }

    private TrickShot ownerAbility;
    private GameObject armedInstance;

    public void LoadTrickShot(int ricochets, float bounciness, float retention, Color color, GameObject armed, TrickShot owner)
    {
        if (armedInstance != null) Destroy(armedInstance); // re-arm: clear the old aura

        IsLoaded = true;
        MaxRicochets = ricochets;
        BouncinessMultiplier = bounciness;
        SpeedRetention = retention;
        TrailColor = color;
        armedInstance = armed;
        ownerAbility = owner;

        Debug.Log($"TrickShotModifier: loaded {ricochets} ricochets, {bounciness}x bounce.");
    }

    /// <summary>Stamps the trick shot onto a just-fired puck and consumes the charge.</summary>
    public void ApplyToPuck(GameObject puck)
    {
        if (!IsLoaded) return;

        TrickShotPuck trickPuck = puck.GetComponent<TrickShotPuck>();
        if (trickPuck == null) trickPuck = puck.AddComponent<TrickShotPuck>();
        trickPuck.Activate(MaxRicochets, BouncinessMultiplier, SpeedRetention, TrailColor);

        IsLoaded = false;
        if (armedInstance != null) Destroy(armedInstance);
        if (ownerAbility != null) ownerAbility.OnTrickShotFired();
    }
}

/// <summary>
/// Lives on the puck while a trick shot is in flight. Counts board ricochets, retains speed
/// with a slight random angle for unpredictability, and wears off after maxRicochets. The
/// actual rebound is Unity physics — this just enhances and bounds it.
/// </summary>
public class TrickShotPuck : MonoBehaviour
{
    private int remainingRicochets;
    private float speedRetention;
    private float bouncinessMultiplier;
    private Color trailColor;
    private bool isActive;

    private Rigidbody rb;
    private TrailRenderer trail;
    private PhysicsMaterial originalMaterial;

    public void Activate(int ricochets, float bounciness, float retention, Color color)
    {
        remainingRicochets = ricochets;
        bouncinessMultiplier = bounciness;
        speedRetention = retention;
        trailColor = color;
        isActive = true;
        rb = GetComponent<Rigidbody>();

        AddTrailEffect();
        ModifyBounciness();
        Debug.Log($"TrickShotPuck active: {ricochets} ricochets.");
    }

    private void AddTrailEffect()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = 0.18f; // sized for the 3D puck (the old 0.3 was 2D-scale)
        trail.endWidth = 0.02f;
        trail.numCapVertices = 4;
        trail.material = CreateTrailMaterial();
        trail.startColor = trailColor;
        trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
    }

    /// <summary>URP-safe unlit material so the trail never renders pink; falls through likely
    /// shader names since the exact one varies by URP version.</summary>
    private static Material CreateTrailMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        return new Material(sh);
    }

    private void ModifyBounciness()
    {
        Collider col = GetComponent<Collider>();
        if (col == null || col.sharedMaterial == null) return;
        originalMaterial = col.sharedMaterial;

        PhysicsMaterial trick = new PhysicsMaterial("TrickShotMaterial");
        trick.bounciness = Mathf.Clamp01(col.sharedMaterial.bounciness * bouncinessMultiplier);
        trick.dynamicFriction = col.sharedMaterial.dynamicFriction * 0.5f; // smoother ricochets
        trick.staticFriction = col.sharedMaterial.staticFriction * 0.5f;
        trick.bounceCombine = PhysicsMaterialCombine.Maximum;
        col.sharedMaterial = trick;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive) return;

        // A board hit = collision with a STATIC collider (no rigidbody → walls, not players)
        // whose surface normal is roughly horizontal (a wall, not the ice floor whose normal
        // points up). Name/tag/layer-independent, so it works with the imported arena's walls
        // whatever they're called and never miscounts a player body-check as a ricochet.
        if (collision.rigidbody != null) return;            // dynamic (player/opponent) — ignore
        Vector3 n = collision.GetContact(0).normal;
        if (Mathf.Abs(n.y) >= 0.5f) return;                 // floor/ceiling, not a board

        remainingRicochets--;
        Debug.Log($"TRICK SHOT RICOCHET! {remainingRicochets} left.");

        if (rb != null)
        {
            Vector3 vel = rb.linearVelocity * speedRetention;
            float jitter = Random.Range(-10f, 10f); // unpredictability on the XZ plane
            vel = Quaternion.Euler(0f, jitter, 0f) * vel;
            rb.linearVelocity = vel;
        }

        if (remainingRicochets <= 0) Deactivate();
    }

    private void Deactivate()
    {
        isActive = false;
        if (trail != null) Destroy(trail);

        Collider col = GetComponent<Collider>();
        if (col != null && originalMaterial != null) col.sharedMaterial = originalMaterial;

        Destroy(this);
    }

    private void OnDestroy()
    {
        if (trail != null) Destroy(trail);
    }
}

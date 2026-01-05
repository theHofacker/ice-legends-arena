using UnityEngine;

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

    protected override void ActivateAbility()
    {
        // For now, spawn meteor at a fixed test location
        // TODO: Get target position from cursor/joystick direction
        Vector2 targetPosition = GetTargetPosition();

        Debug.Log($"Meteor Strike activated at {targetPosition}!");

        // Spawn meteor (for now, just create explosion effect)
        // In full implementation: spawn meteor above, animate it falling, then explode
        SpawnMeteorExplosion(targetPosition);

        // Stun nearby opponents
        StunNearbyOpponents(targetPosition);
    }

    /// <summary>
    /// Get target position for meteor
    /// TODO: Implement cursor/joystick targeting
    /// </summary>
    private Vector2 GetTargetPosition()
    {
        // For now, spawn in front of player
        Vector2 playerPosition = transform.position;
        Vector2 forward = transform.right; // Assuming player faces right

        // Spawn meteor 10 units in front of player
        return playerPosition + forward * 10f;
    }

    /// <summary>
    /// Spawn meteor explosion effect
    /// </summary>
    private void SpawnMeteorExplosion(Vector2 position)
    {
        // Spawn explosion effect
        if (explosionEffect != null)
        {
            GameObject explosion = Instantiate(explosionEffect, position, Quaternion.identity);
            Destroy(explosion, 3f); // Clean up after 3 seconds
        }

        // Play impact sound
        if (impactSound != null)
        {
            AudioSource.PlayClipAtPoint(impactSound, position);
        }

        // Visual feedback in editor
        Debug.DrawLine(position, position + Vector2.up * 5f, Color.red, 2f);
    }

    /// <summary>
    /// Stun all opponents within explosion radius
    /// </summary>
    private void StunNearbyOpponents(Vector2 position)
    {
        // Find all AI opponents
        AIController[] opponents = FindObjectsByType<AIController>(FindObjectsSortMode.None);

        int stunnedCount = 0;

        foreach (AIController opponent in opponents)
        {
            float distance = Vector2.Distance(opponent.transform.position, position);

            if (distance <= explosionRadius)
            {
                // Stun the opponent
                // TODO: Implement stun mechanic in AIController
                Debug.Log($"STUNNED: {opponent.name} for {stunDuration}s!");
                stunnedCount++;

                // Visual feedback: freeze opponent temporarily
                Rigidbody2D opponentRb = opponent.GetComponent<Rigidbody2D>();
                if (opponentRb != null)
                {
                    opponentRb.linearVelocity = Vector2.zero;
                }
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
            Vector2 targetPos = GetTargetPosition();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPos, explosionRadius);
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Minimal ability trigger for the 1v1 test scene. Polls a key each frame and fires the
/// referenced Ability's TryActivateAbility() (which handles cooldown, activation, and the
/// ability-specific logic). Deliberately tiny and ability-agnostic: drop one on the test
/// player per ability you want to try, set the key, and go. This is the reusable pattern
/// for wiring the other characters' abilities into the test scene alongside MeteorStrike.
///
/// Input lives in Update (not FixedUpdate) for the same reason as TestPlayerController:
/// new-Input-System wasPressedThisFrame is only stable for one Update frame.
/// </summary>
public class TestAbilityController : MonoBehaviour
{
    [Tooltip("Ability to trigger. Defaults to an Ability on this same GameObject if left empty.")]
    public Ability ability;

    [Tooltip("Key that activates the ability. Space (shot) and F (check) are taken by TestPlayerController.")]
    public Key activationKey = Key.Q;

    private void Awake()
    {
        if (ability == null)
        {
            ability = GetComponent<Ability>();
        }
        if (ability == null)
        {
            Debug.LogWarning($"TestAbilityController on {name}: no Ability assigned or found on this GameObject.");
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || ability == null) return;

        if (Keyboard.current[activationKey].wasPressedThisFrame)
        {
            ability.TryActivateAbility();
        }
    }
}

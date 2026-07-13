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
    /// <summary>Which on-screen ability button (if any) also triggers this ability. None = key-only.</summary>
    public enum TouchSlot { None, Ability1, Ability2 }

    [Tooltip("Ability to trigger. Defaults to an Ability on this same GameObject if left empty.")]
    public Ability ability;

    [Tooltip("Key that activates the ability. Space (shot) and F (check) are taken by TestPlayerController.")]
    public Key activationKey = Key.Q;

    [Tooltip("On-screen ability button that also fires this ability (for touch). None = keyboard only. " +
             "Convention: Ability1 = first ability (e.g. MeteorStrike/Q), Ability2 = second (e.g. TrickShot/E).")]
    public TouchSlot touchSlot = TouchSlot.None;

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
        if (ability == null) return;

        bool keyPressed = Keyboard.current != null && Keyboard.current[activationKey].wasPressedThisFrame;

        InputManager.VirtualButton touchBtn = (touchSlot != TouchSlot.None && InputManager.Instance != null)
            ? InputManager.Instance.GetAbility(touchSlot)
            : null;
        bool touchPressed = touchBtn != null && touchBtn.Down;

        if (keyPressed || touchPressed)
        {
            ability.TryActivateAbility();
        }
    }
}

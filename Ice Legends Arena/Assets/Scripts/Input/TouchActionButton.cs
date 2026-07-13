using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// On-screen action button that drives one of <see cref="InputManager"/>'s <see cref="InputManager.VirtualButton"/>s.
/// The controllers OR these virtual buttons with their raw keyboard reads, so each action works from touch OR
/// keyboard. Unlike the older <see cref="ContextButton"/> (which only emits tap/hold GESTURES on release), this
/// reports frame-accurate press/release the instant they happen — exactly what the hold-to-charge timing meter
/// needs (press → StartCharging, release → StopCharging).
///
/// Pointer-exit is treated as a release so dragging a finger off a held button (e.g. to bail on a charged shot)
/// resolves cleanly instead of sticking "held" forever. Because it pushes into the global InputManager singleton,
/// it automatically targets whichever skater is currently controlled — no per-player wiring, survives switching.
/// </summary>
[RequireComponent(typeof(Image))]
public class TouchActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum Action { Shoot, Pass, Check, Ability1, Ability2, Switch }

    [Tooltip("Which input this button drives. Maps to an InputManager.VirtualButton the controllers read.")]
    public Action action = Action.Shoot;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color pressedColor = new Color(1f, 1f, 1f, 0.9f);

    private Image image;
    private bool isHeld;

    private void Awake()
    {
        image = GetComponent<Image>();
        ApplyColor(false);
    }

    private InputManager.VirtualButton Button =>
        InputManager.Instance != null ? InputManager.Instance.GetButton(action) : null;

    public void OnPointerDown(PointerEventData eventData)
    {
        isHeld = true;
        Button?.Press();
        ApplyColor(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHeld) return;
        isHeld = false;
        Button?.Release();
        ApplyColor(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Finger/cursor dragged off a still-held button → release so it can't stick.
        if (!isHeld) return;
        isHeld = false;
        Button?.Release();
        ApplyColor(false);
    }

    private void ApplyColor(bool pressed)
    {
        if (image != null) image.color = pressed ? pressedColor : normalColor;
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-rigging timing meter HUD for the test scene. Builds its own Canvas + bar at
/// runtime, finds the player's TimingMeter, and shows charge progress with the green
/// zone marked. Drop this script on any GameObject in the test scene — no inspector
/// wiring required.
/// </summary>
public class TestTimingMeterHUD : MonoBehaviour
{
    [Tooltip("Width of the bar in screen pixels")]
    public float barWidth = 360f;

    [Tooltip("Height of the bar in screen pixels")]
    public float barHeight = 28f;

    [Tooltip("Pixels above the bottom of the screen")]
    public float bottomMargin = 60f;

    [Tooltip("Optional explicit reference; auto-finds the player's TimingMeter when empty")]
    public TimingMeter timingMeter;

    private Image fill;
    private RectTransform greenZoneMarker;
    private GameObject root;
    private float greenStart01 = 0.75f;
    private float greenEnd01 = 0.95f;

    private void Start()
    {
        if (timingMeter == null)
            timingMeter = ResolveActiveMeter();

        if (timingMeter == null)
        {
            Debug.LogWarning("TestTimingMeterHUD: no TimingMeter found in scene — HUD will not update.");
            return;
        }

        BuildUI();
        BindMeter(timingMeter);

        // Follow control switches: rebind to whichever skater the human now controls.
        if (TestTeamController.Instance != null)
            TestTeamController.Instance.OnActiveChanged += HandleActiveChanged;

        SetVisible(false);
    }

    private void OnDestroy()
    {
        BindMeter(null);
        if (TestTeamController.Instance != null)
            TestTeamController.Instance.OnActiveChanged -= HandleActiveChanged;
    }

    /// <summary>The active skater's TimingMeter (via the team manager), else the first one found.</summary>
    private TimingMeter ResolveActiveMeter()
    {
        if (TestTeamController.Instance != null && TestTeamController.Instance.ActiveController != null)
            return TestTeamController.Instance.ActiveController.TimingMeter;

        TestPlayerController player = FindFirstObjectByType<TestPlayerController>();
        return player != null ? player.TimingMeter : null;
    }

    private void HandleActiveChanged()
    {
        TimingMeter next = ResolveActiveMeter();
        if (next != null && next != timingMeter)
            BindMeter(next);
    }

    /// <summary>Swap event subscriptions to <paramref name="meter"/> (null = just unsubscribe).</summary>
    private void BindMeter(TimingMeter meter)
    {
        if (timingMeter != null)
        {
            timingMeter.OnChargeUpdated -= HandleChargeUpdated;
            timingMeter.OnTimingComplete -= HandleTimingComplete;
        }
        timingMeter = meter;
        if (timingMeter != null)
        {
            timingMeter.OnChargeUpdated += HandleChargeUpdated;
            timingMeter.OnTimingComplete += HandleTimingComplete;
        }
    }

    private void Update()
    {
        if (timingMeter == null || root == null) return;
        SetVisible(timingMeter.IsCharging);
    }

    private void BuildUI()
    {
        root = new GameObject("TestTimingMeterHUD_Canvas");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        // Bar background
        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        RectTransform bgRect = (RectTransform)bg.transform;
        bgRect.anchorMin = new Vector2(0.5f, 0f);
        bgRect.anchorMax = new Vector2(0.5f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);
        bgRect.sizeDelta = new Vector2(barWidth, barHeight);
        bgRect.anchoredPosition = new Vector2(0f, bottomMargin);
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Green zone marker (drawn behind the fill so the fill overlays it as charge fills)
        GameObject zone = new GameObject("GreenZone", typeof(RectTransform), typeof(Image));
        zone.transform.SetParent(bg.transform, false);
        greenZoneMarker = (RectTransform)zone.transform;
        greenZoneMarker.anchorMin = new Vector2(greenStart01, 0f);
        greenZoneMarker.anchorMax = new Vector2(greenEnd01, 1f);
        greenZoneMarker.offsetMin = Vector2.zero;
        greenZoneMarker.offsetMax = Vector2.zero;
        zone.GetComponent<Image>().color = new Color(0f, 1f, 0f, 0.25f);

        // Fill bar (left-anchored, scaled by charge)
        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(bg.transform, false);
        RectTransform fillRect = (RectTransform)fillGO.transform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = new Vector2(0f, 0f);
        fillRect.sizeDelta = new Vector2(0f, 0f);
        fill = fillGO.GetComponent<Image>();
        fill.color = Color.yellow;
    }

    private void HandleChargeUpdated(float normalized)
    {
        if (fill == null) return;

        // Resize fill horizontally — bar's parent owns barWidth.
        RectTransform fillRect = fill.rectTransform;
        float clamped = Mathf.Clamp01(normalized);
        fillRect.anchorMax = new Vector2(clamped, 1f);

        if (timingMeter != null)
        {
            fill.color = timingMeter.GetZoneColor(timingMeter.GetCurrentZone());
        }
    }

    private void HandleTimingComplete(TimingMeter.TimingResult result)
    {
        // Flash the bar in result color briefly before it hides.
        if (fill != null && timingMeter != null)
        {
            fill.color = timingMeter.GetZoneColor(result);
        }
    }

    private void SetVisible(bool visible)
    {
        if (root != null && root.activeSelf != visible)
            root.SetActive(visible);
    }
}

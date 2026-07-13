using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// STAGE 3 (touch UI) of promoting TestMovement: build the on-screen mobile controls — a virtual joystick
/// (left) plus action buttons (right) for Shoot / Pass / Check / two abilities / Switch — and the EventSystem
/// that lets any of it receive touch. Mirrors <see cref="GameHUDSetup"/> (Undo-wrapped, re-runnable via Find).
///
/// The buttons drive <see cref="InputManager"/>'s virtual buttons through <see cref="TouchActionButton"/>; the
/// Test controllers OR those with their keyboard reads, so touch and keyboard both work. Movement was already
/// abstracted (<see cref="InputManager.AggregateInput"/> folds the joystick into MoveInput) — this just wires a
/// joystick instance into InputManager.virtualJoystick.
///
/// USER STEPS: open TestMovement → run this menu item → Save → Play → drag the joystick / press the buttons
/// (mouse stands in for touch in the editor).
/// </summary>
public static class TouchControlsSetup
{
    private const string FontPath =
        "Ice Legends Arena/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Ice Legends/Setup Touch Controls")]
    public static void SetupTouchControls()
    {
        // --- EventSystem (the foundational fix — the scene has none, so no UI can receive touch today). ---
        EventSystem es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
            Undo.AddComponent<EventSystem>(esGO);
            // InputSystemUIInputModule, NOT the legacy StandaloneInputModule (which throws under the new
            // Input System this project uses).
            Undo.AddComponent<InputSystemUIInputModule>(esGO);
            Debug.Log("Setup Touch Controls: created EventSystem with InputSystemUIInputModule.");
        }
        else if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            Debug.LogWarning("Setup Touch Controls: an EventSystem exists without an InputSystemUIInputModule. " +
                             "Under the new Input System, UI buttons need that module to receive input.");
        }

        // --- Canvas (reuse if a TouchControls already exists so this is re-runnable). ---
        GameObject canvasGO = GameObject.Find("TouchControls");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("TouchControls", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create TouchControls");
            Canvas canvas = Undo.AddComponent<Canvas>(canvasGO);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // below GameHUD (10) so score/timer stay on top
            CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(canvasGO);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Undo.AddComponent<GraphicRaycaster>(canvasGO);
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // --- Virtual joystick (bottom-left). ---
        VirtualJoystick joystick = BuildJoystick(canvasGO.transform, circle);

        // --- Action buttons (bottom-right cluster). Theme colors distinguish them at a glance. ---
        Color shotCol   = new Color(0.90f, 0.25f, 0.20f); // red
        Color passCol    = new Color(0.25f, 0.55f, 0.95f); // blue
        Color checkCol  = new Color(0.95f, 0.55f, 0.15f); // orange
        Color ability1Col = new Color(0.65f, 0.35f, 0.90f); // purple
        Color ability2Col = new Color(0.20f, 0.80f, 0.70f); // teal
        Color switchCol  = new Color(0.55f, 0.58f, 0.62f); // gray

        MakeButton(canvasGO.transform, "ShootButton", "SHOOT", TouchActionButton.Action.Shoot,
            new Vector2(-170f, 175f), 160f, shotCol, circle, font);
        MakeButton(canvasGO.transform, "PassButton", "PASS", TouchActionButton.Action.Pass,
            new Vector2(-340f, 130f), 120f, passCol, circle, font);
        MakeButton(canvasGO.transform, "CheckButton", "CHECK", TouchActionButton.Action.Check,
            new Vector2(-150f, 365f), 120f, checkCol, circle, font);
        MakeButton(canvasGO.transform, "Ability1Button", "Q", TouchActionButton.Action.Ability1,
            new Vector2(-370f, 320f), 110f, ability1Col, circle, font);
        MakeButton(canvasGO.transform, "Ability2Button", "E", TouchActionButton.Action.Ability2,
            new Vector2(-510f, 250f), 110f, ability2Col, circle, font);
        MakeButton(canvasGO.transform, "SwitchButton", "SWITCH", TouchActionButton.Action.Switch,
            new Vector2(-150f, 545f), 100f, switchCol, circle, font);

        // --- Wire the scene InputManager's joystick reference so MoveInput reads the joystick. ---
        InputManager im = Object.FindFirstObjectByType<InputManager>();
        if (im != null && joystick != null)
        {
            var so = new SerializedObject(im);
            var prop = so.FindProperty("virtualJoystick");
            if (prop != null)
            {
                prop.objectReferenceValue = joystick;
                so.ApplyModifiedProperties();
            }
        }
        else if (im == null)
        {
            Debug.LogWarning("Setup Touch Controls: no InputManager in the scene — the joystick won't drive " +
                             "movement until one exists with its virtualJoystick field pointing at it.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = canvasGO;

        Debug.Log("Setup Touch Controls: joystick + Shoot/Pass/Check/Ability1/Ability2/Switch buttons ready. " +
                  "Save the scene, press Play, and drive them with the mouse (touch stand-in). Keyboard still works. " +
                  "Assign each ability's TestAbilityController.touchSlot (Ability1/Ability2) so the ability buttons fire.");
    }

    private static VirtualJoystick BuildJoystick(Transform parent, Sprite circle)
    {
        // Background (the touch area + the VirtualJoystick component).
        GameObject bgGO = FindOrCreate(parent, "VirtualJoystick");
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = bgRT.anchorMax = new Vector2(0f, 0f); // bottom-left
        bgRT.pivot = new Vector2(0.5f, 0.5f);
        bgRT.anchoredPosition = new Vector2(230f, 230f);
        bgRT.sizeDelta = new Vector2(300f, 300f);

        Image bgImg = bgGO.GetComponent<Image>();
        if (bgImg == null) bgImg = Undo.AddComponent<Image>(bgGO);
        bgImg.sprite = circle;
        bgImg.color = new Color(1f, 1f, 1f, 0.25f);
        bgImg.raycastTarget = true;

        // Handle (visual only — don't let it eat the raycast; the bg handles all pointer events).
        GameObject handleGO = FindOrCreate(bgGO.transform, "Handle");
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = handleRT.anchorMax = new Vector2(0.5f, 0.5f);
        handleRT.pivot = new Vector2(0.5f, 0.5f);
        handleRT.anchoredPosition = Vector2.zero;
        handleRT.sizeDelta = new Vector2(130f, 130f);

        Image handleImg = handleGO.GetComponent<Image>();
        if (handleImg == null) handleImg = Undo.AddComponent<Image>(handleGO);
        handleImg.sprite = circle;
        handleImg.color = new Color(1f, 1f, 1f, 0.55f);
        handleImg.raycastTarget = false;

        VirtualJoystick vj = bgGO.GetComponent<VirtualJoystick>();
        if (vj == null) vj = Undo.AddComponent<VirtualJoystick>(bgGO);
        var so = new SerializedObject(vj);
        so.FindProperty("background").objectReferenceValue = bgRT;
        so.FindProperty("handle").objectReferenceValue = handleRT;
        so.FindProperty("handleRange").floatValue = 85f; // matches (bg 300 - handle 130)/2 ≈ 85
        so.ApplyModifiedProperties();
        return vj;
    }

    private static void MakeButton(Transform parent, string name, string label, TouchActionButton.Action action,
        Vector2 anchoredPos, float size, Color theme, Sprite circle, TMP_FontAsset font)
    {
        GameObject go = FindOrCreate(parent, name);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); // bottom-right
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);

        Image img = go.GetComponent<Image>();
        if (img == null) img = Undo.AddComponent<Image>(go);
        img.sprite = circle;
        img.raycastTarget = true;

        TouchActionButton tab = go.GetComponent<TouchActionButton>();
        if (tab == null) tab = Undo.AddComponent<TouchActionButton>(go);
        tab.action = action;
        // Per-button theme colors (TouchActionButton tints the Image on press; set its serialized
        // normal/pressed colors so each button keeps its identity instead of a flat white).
        var so = new SerializedObject(tab);
        so.FindProperty("normalColor").colorValue = new Color(theme.r, theme.g, theme.b, 0.60f);
        so.FindProperty("pressedColor").colorValue = new Color(
            Mathf.Min(1f, theme.r + 0.15f), Mathf.Min(1f, theme.g + 0.15f), Mathf.Min(1f, theme.b + 0.15f), 0.95f);
        so.ApplyModifiedProperties();
        img.color = new Color(theme.r, theme.g, theme.b, 0.60f); // initial (Awake re-applies at runtime)

        // Label (centered, non-raycast so it doesn't intercept — events still bubble to the button anyway).
        GameObject labelGO = FindOrCreate(go.transform, "Label");
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGO.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = Undo.AddComponent<TextMeshProUGUI>(labelGO);
        if (font != null) tmp.font = font;
        tmp.text = label;
        tmp.fontSize = size * 0.28f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
    }

    private static GameObject FindOrCreate(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }
}

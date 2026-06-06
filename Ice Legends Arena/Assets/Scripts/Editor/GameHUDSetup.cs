using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// STAGE 2 of promoting TestMovement to the real gameplay scene: build the real GameManager-driven
/// HUD (the event-based <see cref="HUDManager"/>) in the current scene, replacing the throwaway
/// OnGUI <c>TestMatchHUD</c>. Creates a Screen-Space Canvas with score / timer / goal-celebration
/// TextMeshPro labels, adds HUDManager, and wires its serialized fields. HUDManager subscribes to
/// GameManager's OnScoreChanged / OnTimerChanged / OnGoalScored / OnMatchStateChanged at runtime.
///
/// Scope is deliberately the GameManager-driven essentials. The pause menu, ability buttons, and
/// virtual joystick from the full Gameplay2.5D Canvas depend on InputManager / PlayerManager / the
/// ability system, which arrive in Stage 3 — so they're intentionally left out here.
///
/// After running, save the GameHUD as a prefab (Prefabs/UI/GameHUD.prefab) so the future sandbox and
/// production scenes share one source of truth (the prefab plan).
/// </summary>
public static class GameHUDSetup
{
    private const string FontPath =
        "Ice Legends Arena/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Ice Legends/Setup Game HUD")]
    public static void SetupGameHUD()
    {
        // --- Canvas (reuse if a GameHUD already exists so this is re-runnable). ---
        GameObject canvasGO = GameObject.Find("GameHUD");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("GameHUD", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create GameHUD");
            Canvas canvas = Undo.AddComponent<Canvas>(canvasGO);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(canvasGO);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Undo.AddComponent<GraphicRaycaster>(canvasGO);
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            Debug.LogWarning($"Setup Game HUD: couldn't load TMP font at '{FontPath}' — labels may " +
                             "render with no font until one is assigned in the Inspector.");

        // --- Labels. ---
        TextMeshProUGUI scoreText = MakeLabel(canvasGO.transform, "ScoreText", "0 - 0", 54, font,
            new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(600f, 80f));

        TextMeshProUGUI timerText = MakeLabel(canvasGO.transform, "TimerText", "5:00", 34, font,
            new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(400f, 56f));

        TextMeshProUGUI goalText = MakeLabel(canvasGO.transform, "GoalCelebrationText", "GOAL!", 110, font,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1000f, 240f));
        goalText.fontStyle = FontStyles.Bold;
        goalText.color = Color.green;
        goalText.gameObject.SetActive(false); // HUDManager shows it on a goal

        // --- HUDManager (wire the serialized TMP fields). ---
        HUDManager hud = canvasGO.GetComponent<HUDManager>();
        if (hud == null) hud = Undo.AddComponent<HUDManager>(canvasGO);
        var so = new SerializedObject(hud);
        so.FindProperty("scoreText").objectReferenceValue = scoreText;
        so.FindProperty("timerText").objectReferenceValue = timerText;
        so.FindProperty("goalCelebrationText").objectReferenceValue = goalText;
        so.ApplyModifiedProperties();

        // --- Retire the throwaway OnGUI HUD so we don't double-draw the score. ---
        int removed = 0;
        foreach (TestMatchHUD t in Object.FindObjectsByType<TestMatchHUD>(FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(t);
            removed++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = canvasGO;

        Debug.Log($"Setup Game HUD: GameHUD ready (real HUDManager wired to GameManager events)" +
                  $"{(removed > 0 ? $", removed {removed} TestMatchHUD" : "")}. Press Play to see the " +
                  "score/timer update. Save the scene — and consider saving GameHUD as a prefab.");
    }

    private static TextMeshProUGUI MakeLabel(Transform parent, string name, string text, float size,
        TMP_FontAsset font, Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject
                                         : new GameObject(name, typeof(RectTransform));
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(go, "Create HUD Label");
            go.transform.SetParent(parent, false);
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = Undo.AddComponent<TextMeshProUGUI>(go);
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = false;
        return tmp;
    }
}

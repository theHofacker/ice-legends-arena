using UnityEngine;

/// <summary>
/// Minimal on-screen score/timer/state readout driven by <see cref="GameManager"/>, for the test
/// scene now that it's being promoted to the real gameplay scene. The old standalone score lived on
/// <c>TestGoalTrigger</c> (its static counter + OnGUI); once the goal line is swapped to the real
/// <see cref="GoalTrigger"/> (which scores through GameManager), this gives us the same at-a-glance
/// readout while we test the net port. Throwaway — delete once the real GameHUD is brought over.
/// </summary>
public class TestMatchHUD : MonoBehaviour
{
    [Tooltip("Font size for the score line.")]
    public int scoreFontSize = 28;

    private void OnGUI()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        var score = new GUIStyle(GUI.skin.label)
        {
            fontSize = scoreFontSize,
            alignment = TextAnchor.UpperCenter,
            fontStyle = FontStyle.Bold
        };
        score.normal.textColor = Color.white;
        GUI.Label(new Rect(0f, 10f, Screen.width, 40f),
            $"PLAYER  {gm.PlayerScore} – {gm.OpponentScore}  OPPONENT", score);

        var info = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.UpperCenter
        };
        info.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        float t = Mathf.Max(0f, gm.TimeRemaining);
        string clock = $"{Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}";
        GUI.Label(new Rect(0f, 50f, Screen.width, 28f),
            $"{clock}   [{gm.CurrentState}]{(gm.IsOvertime ? "  OT" : "")}", info);
    }
}

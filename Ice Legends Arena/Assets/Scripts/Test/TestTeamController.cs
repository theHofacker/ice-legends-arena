using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single source of truth for WHICH of my-team's skaters the human is controlling, and the switching
/// between them (Stage 3 of promoting TestMovement). Before this, "the human" was implicit — the puck,
/// teammates, opponent and HUD all did <c>FindFirstObjectByType&lt;TestPlayerController&gt;()</c>. That
/// can't express "control moved to another skater," so this manager owns the active skater explicitly
/// and everyone queries <see cref="ActiveSkater"/>.
///
/// Mechanism: every switchable skater carries BOTH a <see cref="TestPlayerController"/> (human) and a
/// <see cref="TestTeammateController"/> (AI). Exactly one is <c>enabled</c> at a time; switching just
/// flips the pair (and the <see cref="TestShotAimer"/>). Components persist, so each skater keeps its
/// inspector tuning across switches — no runtime AddComponent / value-copy. Run
/// <c>Ice Legends → Setup Switchable Team</c> to give every my-team skater the dual loadout.
/// </summary>
public class TestTeamController : MonoBehaviour
{
    public static TestTeamController Instance { get; private set; }

    [Header("Switching")]
    [Tooltip("Key to cycle control to the NEAREST teammate (e.g. on defense). Passing the puck also " +
             "auto-switches to the receiver.")]
    public Key switchKey = Key.Tab;

    [Header("Active Indicator")]
    [Tooltip("Optional marker placed under the skater you control so it's obvious who's active. " +
             "Auto-created (a flat yellow disc) if left empty.")]
    public GameObject activeIndicator;

    [Tooltip("Height above the ice for the indicator.")]
    [Range(0f, 0.5f)] public float indicatorHeight = 0.03f;

    private readonly List<GameObject> team = new List<GameObject>();
    private TestPuckController puck;

    /// <summary>The transform of the skater the human currently controls.</summary>
    public Transform ActiveSkater { get; private set; }

    /// <summary>The human controller on the active skater.</summary>
    public TestPlayerController ActiveController { get; private set; }

    /// <summary>Fired after control moves to a different skater.</summary>
    public event Action OnActiveChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // A switchable skater is anything carrying BOTH controllers (include inactive so a skater that
        // starts with its human controller disabled is still discovered).
        foreach (TestPlayerController pc in FindObjectsByType<TestPlayerController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pc.GetComponent<TestTeammateController>() != null && !team.Contains(pc.gameObject))
                team.Add(pc.gameObject);
        }

        puck = FindFirstObjectByType<TestPuckController>();
    }

    private void Start()
    {
        if (team.Count == 0)
        {
            Debug.LogWarning("TestTeamController: no switchable skaters found (each needs BOTH " +
                             "TestPlayerController + TestTeammateController). Run Ice Legends → Setup Switchable Team.");
            return;
        }

        // Initial active = whichever skater already has its human controller enabled, else the first.
        GameObject initial = null;
        foreach (GameObject go in team)
        {
            TestPlayerController pc = go.GetComponent<TestPlayerController>();
            if (pc != null && pc.enabled) { initial = go; break; }
        }
        SwitchTo(initial != null ? initial : team[0]);
    }

    private void Update()
    {
        bool keyPressed = Keyboard.current != null && Keyboard.current[switchKey].wasPressedThisFrame;
        bool touchPressed = InputManager.Instance != null && InputManager.Instance.Switch.Down;
        if (keyPressed || touchPressed)
            SwitchToNearestTeammate();

        if (activeIndicator != null && ActiveSkater != null)
        {
            Vector3 p = ActiveSkater.position;
            p.y = indicatorHeight;
            activeIndicator.transform.position = p;
        }
    }

    /// <summary>Take control of the nearest OTHER team skater (manual cycle, e.g. on defense).</summary>
    public void SwitchToNearestTeammate()
    {
        if (ActiveSkater == null) return;

        GameObject best = null;
        float bestDist = float.PositiveInfinity;
        foreach (GameObject go in team)
        {
            if (go == null || go.transform == ActiveSkater) continue;
            float d = PhysicsHelper.DistanceXZ(ActiveSkater.position, go.transform.position);
            if (d < bestDist) { bestDist = d; best = go; }
        }
        if (best != null) SwitchTo(best);
    }

    /// <summary>Called by the passer right after a pass so control follows the puck to the receiver.</summary>
    public void OnPassedTo(GameObject receiver)
    {
        if (receiver != null && team.Contains(receiver) && receiver.transform != ActiveSkater)
            SwitchTo(receiver);
    }

    /// <summary>
    /// Make <paramref name="skater"/> the human-controlled one and every other team member AI. If the
    /// puck was being carried (human possession), it's released to loose first so it stays with the
    /// skater you're leaving (who becomes an AI teammate and re-grabs it) instead of teleporting to the
    /// new controllee.
    /// </summary>
    public void SwitchTo(GameObject skater)
    {
        if (skater == null) return;

        // Release the puck BEFORE repointing so its collision-restore targets the old carrier.
        if (puck != null && puck.IsPossessed) puck.ReleasePossession();

        foreach (GameObject go in team)
        {
            if (go == null) continue;
            bool active = (go == skater);

            TestPlayerController pc = go.GetComponent<TestPlayerController>();
            TestTeammateController tc = go.GetComponent<TestTeammateController>();
            TestShotAimer aim = go.GetComponent<TestShotAimer>();

            if (pc != null) pc.enabled = active;
            if (tc != null) tc.enabled = !active;
            if (aim != null) aim.enabled = active; // no aim line / cone for AI skaters
        }

        ActiveSkater = skater.transform;
        ActiveController = skater.GetComponent<TestPlayerController>();
        EnsureIndicator();
        OnActiveChanged?.Invoke();
        Debug.Log($"[TeamControl] Now controlling {skater.name}");
    }

    private void EnsureIndicator()
    {
        if (activeIndicator != null) return;

        activeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        activeIndicator.name = "ActiveSkaterIndicator";

        Collider col = activeIndicator.GetComponent<Collider>();
        if (col != null) Destroy(col);

        activeIndicator.transform.localScale = new Vector3(1.3f, 0.02f, 1.3f); // flat disc
        Renderer rend = activeIndicator.GetComponent<Renderer>();
        if (rend != null) rend.material.color = new Color(1f, 0.9f, 0.2f, 1f); // bright yellow
    }
}

using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Per-frame telemetry for the test opponent. Writes a CSV to
/// Application.persistentDataPath/Logs/opponent_*.csv and renders an
/// on-screen state HUD. Add to the opponent GameObject; that's it.
///
/// Drop the CSV in the repo for Claude to read. Pair with a screen
/// recording for visual context.
/// </summary>
[RequireComponent(typeof(TestOpponentController))]
[RequireComponent(typeof(Rigidbody))]
public class OpponentDebugLogger : MonoBehaviour
{
    [Header("CSV Logging")]
    [Tooltip("Write per-frame telemetry to a CSV file")]
    public bool writeCsv = true;

    [Tooltip("Folder name (created at the project root, next to Assets/) where CSVs are written")]
    public string outputFolderName = "DebugCaptures";

    [Tooltip("How often to log a row (seconds). 0 = every Update.")]
    [Range(0f, 0.5f)]
    public float logInterval = 0.05f;

    [Tooltip("Flush to disk every N rows (lower = safer, slower)")]
    [Range(1, 200)]
    public int flushEvery = 30;

    [Header("On-Screen HUD")]
    public bool showHud = true;

    [Range(8, 32)]
    public int hudFontSize = 14;

    public Vector2 hudScreenPos = new Vector2(10, 10);

    [Header("State Change Logging")]
    [Tooltip("Print to Console when high-level state changes (chasing/possessing/stunned)")]
    public bool logStateChanges = true;

    private TestOpponentController opp;
    private Rigidbody rb;
    private Transform puck;
    private Rigidbody puckRb;
    private Transform player;
    private Transform attackGoal;

    // Animator + Humanoid bone refs for visibility into what the character is
    // actually doing visually — script-side stun says STUNNED but if the animator
    // re-entered HeavyHit five times we'd see normalizedTime snap back to 0
    // repeatedly. hipY/footY tell us whether FootIK is doing its job.
    private Animator anim;
    private Transform hipBone;
    private Transform lFootBone;
    private Transform rFootBone;

    // Cached lookup table for friendly state names. Keep in sync with the states
    // HockeyAnimatorBuilder creates. Stored as full-path-hash → name so we can
    // print the actual current state even when speed/Bake-into-Pose tweak things.
    private static readonly string[] KnownStateNames = {
        "Idle", "Skating", "SkatingWithPuck", "Shoot",
        "LightCheck", "MediumCheck", "HeavyCheck",
        "LightHit", "MediumHit", "HeavyHit", "GetUp",
        "BoardHit", "BoardGetUp",
        "Celebration", "Block", "Charging"
    };

    // Tracks normalizedTime to detect a state re-entering itself (NT drops back
    // toward 0 while staying in the same state). That's the "big circle on the
    // ice" pattern — animator state didn't change but the clip restarted.
    private string prevAnimState = "";
    private float prevAnimNT = 0f;
    private int dbgReentries = 0;

    private StreamWriter writer;
    private string csvPath;
    private float nextLogTime;
    private int rowsSinceFlush;
    private int frameIndex;

    private string lastState = "";
    private float lastStateChangeTime;

    // Event-detection state — lets us spot SHOT, POSSESSED, BODY_CHECK, etc.
    // by watching transitions in the opponent's private fields.
    private bool prevHasPuck;
    private bool prevStunned;
    private float prevCheckTimer;
    private float prevPosCD;

    // Reflection-cached private fields from TestOpponentController
    private System.Reflection.FieldInfo fHasPuck;
    private System.Reflection.FieldInfo fIsStunned;
    private System.Reflection.FieldInfo fStunTimer;
    private System.Reflection.FieldInfo fCheckTimer;
    private System.Reflection.FieldInfo fPossessionCooldown;

    private void Awake()
    {
        opp = GetComponent<TestOpponentController>();
        rb = GetComponent<Rigidbody>();

        var t = typeof(TestOpponentController);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        fHasPuck = t.GetField("hasPuck", flags);
        fIsStunned = t.GetField("isStunned", flags);
        fStunTimer = t.GetField("stunTimer", flags);
        fCheckTimer = t.GetField("checkTimer", flags);
        fPossessionCooldown = t.GetField("possessionCooldown", flags);
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Puck");
        if (p != null)
        {
            puck = p.transform;
            puckRb = p.GetComponent<Rigidbody>();
        }

        var pl = FindFirstObjectByType<TestPlayerController>();
        if (pl != null) player = pl.transform;

        // Don't read opp.attackGoal here — the opponent finds its goal in its
        // own Start() and there's no execution-order guarantee. ResolveGoal()
        // re-reads it lazily until it returns non-null.

        anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            hipBone   = anim.GetBoneTransform(HumanBodyBones.Hips);
            lFootBone = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            rFootBone = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        if (writeCsv) OpenCsv();
    }

    private void ResolveGoal()
    {
        if (attackGoal != null) return;
        if (opp != null && opp.attackGoal != null) attackGoal = opp.attackGoal;
    }

    private void OpenCsv()
    {
        // Application.dataPath = ".../Ice Legends Arena/Assets"
        // Project root is its parent — write the folder next to Assets/ so it
        // sits inside the Unity project but outside the asset database.
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string dir = Path.Combine(projectRoot, outputFolderName);
        Directory.CreateDirectory(dir);
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        csvPath = Path.Combine(dir, $"opponent_{stamp}.csv");
        writer = new StreamWriter(csvPath, false, Encoding.UTF8);
        writer.WriteLine(
            "frame,time,event,state,hasPuck,isStunned,stunTimer,checkTimer,posCD," +
            "px,py,pz,vx,vy,vz,speed," +
            "puckX,puckZ,puckSpeed,distToPuck," +
            "playerX,playerZ,distToPlayer," +
            "goalX,goalZ,distToGoal," +
            "yaw," +
            "animState,animNT,animXfading,animNextState,animNextNT," +
            "hipY,lFootY,rFootY," +
            "gotHitAttempts,gotHitFires,animReentries");
        Debug.Log($"OpponentDebugLogger: writing CSV → {csvPath}");
    }

    private void Update()
    {
        frameIndex++;
        ResolveGoal();

        string state = ComputeState();
        if (logStateChanges && state != lastState)
        {
            float dwell = Time.time - lastStateChangeTime;
            Debug.Log($"[Opp] {lastState} → {state}  (after {dwell:F2}s)");
            lastState = state;
            lastStateChangeTime = Time.time;
        }

        DetectAndLogEvents();

        if (writeCsv && writer != null && Time.time >= nextLogTime)
        {
            WriteRow(state, "");
            nextLogTime = Time.time + logInterval;
        }
    }

    /// <summary>
    /// Watches the opponent's private fields for transitions and emits explicit
    /// event rows in the CSV. Lets us see causes (SHOT, POSSESSED, BODY_CHECK,
    /// STUNNED) in the timeline, not just the resulting state.
    /// </summary>
    private void DetectAndLogEvents()
    {
        bool hasPuck = fHasPuck != null && (bool)fHasPuck.GetValue(opp);
        bool stunned = fIsStunned != null && (bool)fIsStunned.GetValue(opp);
        float checkT = fCheckTimer != null ? (float)fCheckTimer.GetValue(opp) : 0f;
        float posCD  = fPossessionCooldown != null ? (float)fPossessionCooldown.GetValue(opp) : 0f;

        // Possession edge: 0 → 1 = grabbed puck
        if (hasPuck && !prevHasPuck) EmitEvent("POSSESSED");

        // Lost-puck edge: 1 → 0. Distinguish shot vs steal via posCD.
        // ShootPuck() sets posCD = 1.5; LosePuck() sets posCD = 0.5.
        if (!hasPuck && prevHasPuck)
        {
            EmitEvent(posCD > 1.0f ? "SHOT" : "LOST_PUCK");
        }

        // Body check given: checkTimer just got reset (jumped up)
        if (checkT > prevCheckTimer + 0.5f) EmitEvent("BODY_CHECK_GIVEN");

        // Stun edge: 0 → 1 = got hit
        if (stunned && !prevStunned) EmitEvent("STUNNED");

        prevHasPuck = hasPuck;
        prevStunned = stunned;
        prevCheckTimer = checkT;
        prevPosCD = posCD;
    }

    private void EmitEvent(string label)
    {
        Debug.Log($"[Opp Event] {label} @ t={Time.time:F2}");
        if (writeCsv && writer != null)
        {
            WriteRow(ComputeState(), label);
            writer.Flush();
            rowsSinceFlush = 0;
        }
    }

    private string ComputeState()
    {
        bool stunned = fIsStunned != null && (bool)fIsStunned.GetValue(opp);
        bool hasPuck = fHasPuck != null && (bool)fHasPuck.GetValue(opp);
        if (stunned) return "STUNNED";
        if (hasPuck) return "SKATING_WITH_PUCK";

        if (player != null && puck != null)
        {
            float distToPlayer = PhysicsHelper.DistanceXZ(transform.position, player.position);
            var puckCtrl = puck.GetComponent<TestPuckController>();
            bool playerHasPuck = puckCtrl != null && puckCtrl.IsPossessed;
            if (playerHasPuck && distToPlayer < opp.checkRange) return "BODY_CHECKING";
        }
        return "CHASING_PUCK";
    }

    private void WriteRow(string state, string evt)
    {
        Vector3 pos = transform.position;
        Vector3 vel = rb.linearVelocity;
        float speed = new Vector2(vel.x, vel.z).magnitude;

        Vector3 puckPos = puck != null ? puck.position : Vector3.zero;
        float puckSpeed = puckRb != null ? new Vector2(puckRb.linearVelocity.x, puckRb.linearVelocity.z).magnitude : 0f;
        float distPuck = puck != null ? PhysicsHelper.DistanceXZ(pos, puckPos) : -1f;

        Vector3 plPos = player != null ? player.position : Vector3.zero;
        float distPlayer = player != null ? PhysicsHelper.DistanceXZ(pos, plPos) : -1f;

        Vector3 goalPos = attackGoal != null ? attackGoal.position : Vector3.zero;
        float distGoal = attackGoal != null ? PhysicsHelper.DistanceXZ(pos, goalPos) : -1f;

        float yaw = transform.eulerAngles.y;

        bool hasPuck = fHasPuck != null && (bool)fHasPuck.GetValue(opp);
        bool stunned = fIsStunned != null && (bool)fIsStunned.GetValue(opp);
        float stunT = fStunTimer != null ? (float)fStunTimer.GetValue(opp) : 0f;
        float checkT = fCheckTimer != null ? (float)fCheckTimer.GetValue(opp) : 0f;
        float posCD = fPossessionCooldown != null ? (float)fPossessionCooldown.GetValue(opp) : 0f;

        // Animator snapshot. Detect re-entries (NT dropped while staying in the
        // same state) — those would prove HeavyHit is restarting from frame 0.
        string animState = "-";
        float animNT = 0f;
        bool animXfading = false;
        string animNextState = "-";
        float animNextNT = 0f;
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
            animState = ResolveStateName(info);
            animNT = info.normalizedTime;
            animXfading = anim.IsInTransition(0);
            if (animXfading)
            {
                AnimatorStateInfo nextInfo = anim.GetNextAnimatorStateInfo(0);
                animNextState = ResolveStateName(nextInfo);
                animNextNT = nextInfo.normalizedTime;
            }
            if (animState == prevAnimState && animNT < prevAnimNT - 0.1f) dbgReentries++;
            prevAnimState = animState;
            prevAnimNT = animNT;
        }

        float hipY = hipBone != null ? hipBone.position.y : 0f;
        float lFootY = lFootBone != null ? lFootBone.position.y : 0f;
        float rFootY = rFootBone != null ? rFootBone.position.y : 0f;

        int gotHitAttempts = opp != null ? opp.dbgGotHitAttempts : 0;
        int gotHitFires = opp != null ? opp.dbgGotHitFires : 0;

        writer.WriteLine(
            $"{frameIndex},{Time.time:F3},{evt},{state},{(hasPuck ? 1 : 0)},{(stunned ? 1 : 0)},{stunT:F2},{checkT:F2},{posCD:F2}," +
            $"{pos.x:F2},{pos.y:F2},{pos.z:F2},{vel.x:F2},{vel.y:F2},{vel.z:F2},{speed:F2}," +
            $"{puckPos.x:F2},{puckPos.z:F2},{puckSpeed:F2},{distPuck:F2}," +
            $"{plPos.x:F2},{plPos.z:F2},{distPlayer:F2}," +
            $"{goalPos.x:F2},{goalPos.z:F2},{distGoal:F2}," +
            $"{yaw:F1}," +
            $"{animState},{animNT:F2},{(animXfading ? 1 : 0)},{animNextState},{animNextNT:F2}," +
            $"{hipY:F3},{lFootY:F3},{rFootY:F3}," +
            $"{gotHitAttempts},{gotHitFires},{dbgReentries}");

        rowsSinceFlush++;
        if (rowsSinceFlush >= flushEvery)
        {
            writer.Flush();
            rowsSinceFlush = 0;
        }
    }

    /// <summary>
    /// Maps an AnimatorStateInfo to the human-readable state name it represents,
    /// by hashing each known state name and comparing against fullPathHash. Falls
    /// back to "?(hash)" so we never return a misleading match.
    /// </summary>
    private static string ResolveStateName(AnimatorStateInfo info)
    {
        foreach (string n in KnownStateNames)
        {
            if (info.IsName(n)) return n;
        }
        return $"?({info.shortNameHash})";
    }

    private GUIStyle hudStyle;

    private void OnGUI()
    {
        if (!showHud) return;

        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label);
            hudStyle.fontSize = hudFontSize;
            hudStyle.normal.textColor = Color.white;
        }

        Vector3 vel = rb.linearVelocity;
        float speed = new Vector2(vel.x, vel.z).magnitude;
        float distPuck = puck != null ? PhysicsHelper.DistanceXZ(transform.position, puck.position) : -1f;
        float distPlayer = player != null ? PhysicsHelper.DistanceXZ(transform.position, player.position) : -1f;
        float distGoal = attackGoal != null ? PhysicsHelper.DistanceXZ(transform.position, attackGoal.position) : -1f;

        float stunT = fStunTimer != null ? (float)fStunTimer.GetValue(opp) : 0f;
        float checkT = fCheckTimer != null ? (float)fCheckTimer.GetValue(opp) : 0f;

        // Animator snapshot for HUD (live state + clip progress) — same source as the CSV
        string animState = "-";
        float animNT = 0f;
        bool animXfading = false;
        string animNextState = "-";
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
            animState = ResolveStateName(info);
            animNT = info.normalizedTime;
            animXfading = anim.IsInTransition(0);
            if (animXfading) animNextState = ResolveStateName(anim.GetNextAnimatorStateInfo(0));
        }

        float hipY = hipBone != null ? hipBone.position.y : 0f;
        float lFootY = lFootBone != null ? lFootBone.position.y : 0f;
        float rFootY = rFootBone != null ? rFootBone.position.y : 0f;
        int gotHitAttempts = opp != null ? opp.dbgGotHitAttempts : 0;
        int gotHitFires = opp != null ? opp.dbgGotHitFires : 0;

        var sb = new StringBuilder();
        sb.AppendLine("== OPPONENT DEBUG ==");
        sb.AppendLine($"State:    {lastState}");
        sb.AppendLine($"Anim:     {animState} NT={animNT:F2}{(animXfading ? $" → {animNextState}" : "")}");
        sb.AppendLine($"Hip Y:    {hipY:F2}    Feet Y: L={lFootY:F2} R={rFootY:F2}");
        sb.AppendLine($"GotHit:   fires={gotHitFires} attempts={gotHitAttempts}  reentries={dbgReentries}");
        sb.AppendLine($"Pos:      ({transform.position.x:F1}, {transform.position.z:F1})");
        sb.AppendLine($"Vel:      ({vel.x:F1}, {vel.z:F1})  speed={speed:F1}");
        sb.AppendLine($"Yaw:      {transform.eulerAngles.y:F0}°");
        sb.AppendLine($"DistPuck: {distPuck:F1}    DistPlayer: {distPlayer:F1}");
        sb.AppendLine($"DistGoal: {distGoal:F1}");
        sb.AppendLine($"checkCD:  {checkT:F2}    stunT: {stunT:F2}");
        if (writeCsv) sb.AppendLine($"CSV: {Path.GetFileName(csvPath)}");

        // Background box for readability
        Vector2 size = hudStyle.CalcSize(new GUIContent(sb.ToString()));
        Rect bg = new Rect(hudScreenPos.x - 4, hudScreenPos.y - 4, 360, size.y * 11 + 8);
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(bg, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(hudScreenPos.x, hudScreenPos.y, 420, 420), sb.ToString(), hudStyle);
    }

    private void OnDestroy()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
            Debug.Log($"OpponentDebugLogger: closed CSV → {csvPath}");
        }
    }

    private void OnApplicationQuit()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }
}

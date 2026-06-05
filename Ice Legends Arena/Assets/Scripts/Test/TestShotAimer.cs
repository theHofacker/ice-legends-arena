using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shot aiming for the 1v1 test scene. Decouples shot direction from skating direction so you
/// can pick a corner instead of only firing straight down your last movement vector.
///
/// Model (facing-anchored cone + net magnetism):
///   * On shot-button PRESS, <see cref="BeginAim"/> freezes an aim AXIS (the player's facing at
///     that instant) and an ORIGIN. The cone is aimAxis ± <see cref="aimConeHalfAngle"/> on the ice
///     plane — that's "how far you can manually aim." Because the axis is frozen at press, a sharp
///     approach angle leaves the net occupying a narrow slice of your cone, so corner-picking is
///     genuinely harder from the boards than from the slot. No special-casing; it's just geometry.
///   * While held, the drag input (mouse-on-ice on laptop) maps to an angle inside the cone.
///   * Assist: the opponent net's two posts subtend an angular window from your origin. Your raw
///     aim is CLAMPED into that window and blended back by <see cref="assistStrength"/> — so a
///     near-miss gets pulled onto the net, but aim that's already between the posts is left alone
///     (you keep full corner control). strength 0 = raw manual aim, 1 = basically can't miss the net.
///
/// Loft stays driven by the charge meter (TestPuckController) — drag is horizontal aim only here.
///
/// LATER (mobile): swap the one input source in <see cref="GetDesiredAimDirection"/> from
/// mouse-on-ice to touch-drag-from-the-shoot-button. The cone + assist math is identical.
/// </summary>
public class TestShotAimer : MonoBehaviour
{
    [Header("Aim Cone")]
    [Tooltip("Half-angle (degrees) of the manual aim cone, measured from the facing direction " +
             "captured when you pressed the shot button. This is how far off your skating line you " +
             "can manually aim a shot/pass.")]
    [Range(5f, 90f)]
    public float aimConeHalfAngle = 45f;

    [Header("Aim Assist")]
    [Tooltip("How strongly aim snaps toward the net. 0 = fully raw manual aim (skill ceiling). " +
             "1 = any aim within reach lands between the posts. Tune this on the laptop, then " +
             "re-tune on mobile — this slider IS the answer to 'how much assist do we need'.")]
    [Range(0f, 1f)]
    public float assistStrength = 0.5f;

    [Tooltip("Extra degrees beyond the net posts within which the magnetism still engages. A raw " +
             "aim this far outside a post still gets pulled toward it; further out is left fully raw " +
             "so you can deliberately aim wide (e.g. a pass or dump).")]
    [Range(0f, 30f)]
    public float assistMargin = 12f;

    [Header("References")]
    [Tooltip("Camera used to project the mouse onto the ice for laptop aiming. Auto-finds the main " +
             "camera if left empty.")]
    public Camera aimCamera;

    [Tooltip("The opponent's net to magnetize toward. Auto-finds the TestGoalTrigger whose " +
             "isPlayerNet is false if left empty. If no net is found, assist is simply off " +
             "(pure manual cone).")]
    public TestGoalTrigger targetNet;

    [Header("Indicator")]
    [Tooltip("Draw a runtime aim line + cone edges in the Game view while aiming.")]
    public bool showIndicator = true;

    [Tooltip("Length (m) of the drawn aim/cone lines.")]
    [Range(2f, 30f)]
    public float indicatorLength = 12f;

    [Tooltip("Height above the ice the indicator lines are drawn at.")]
    [Range(0.02f, 1f)]
    public float indicatorHeight = 0.06f;

    [Header("Debug")]
    public bool logAim = false;

    // Captured at press, held for the whole charge.
    private Vector3 aimAxis = Vector3.forward;
    private Vector3 aimOrigin;
    private bool isAiming;

    // Latest computed aim direction (XZ, normalized). Read by TestPlayerController at release.
    private Vector3 currentAimDir = Vector3.forward;

    private Collider netCollider;

    // Runtime indicator lines.
    private LineRenderer aimLine;
    private LineRenderer edgeLeft;
    private LineRenderer edgeRight;

    /// <summary>True while the shot button is held and an aim is being resolved.</summary>
    public bool IsAiming => isAiming;

    /// <summary>The current aimed shot direction (XZ-flat, normalized). Falls back to the frozen
    /// axis when not actively aiming.</summary>
    public Vector3 AimDirection => isAiming ? currentAimDir : aimAxis;

    private void Start()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        if (aimCamera == null) aimCamera = FindFirstObjectByType<Camera>();

        ResolveNet();
        BuildIndicator();
        SetIndicatorVisible(false);
    }

    /// <summary>Finds the opponent net (isPlayerNet == false) and caches its collider for post reads.</summary>
    private void ResolveNet()
    {
        if (targetNet == null)
        {
            foreach (TestGoalTrigger g in FindObjectsByType<TestGoalTrigger>(FindObjectsSortMode.None))
            {
                if (!g.isPlayerNet) { targetNet = g; break; }
            }
        }
        netCollider = targetNet != null ? targetNet.GetComponent<Collider>() : null;
    }

    /// <summary>
    /// Begin aiming. Freezes the cone axis to the supplied facing direction and the origin to the
    /// shot launch point. Called by TestPlayerController on shot-button press.
    /// </summary>
    public void BeginAim(Vector3 facingDir, Vector3 origin)
    {
        aimAxis = PhysicsHelper.FlattenY(facingDir);
        if (aimAxis.sqrMagnitude < 0.0001f) aimAxis = Vector3.forward;
        aimAxis.Normalize();

        aimOrigin = origin;
        currentAimDir = aimAxis;
        isAiming = true;
    }

    /// <summary>Stop aiming (shot fired or cancelled). Hides the indicator; AimDirection stays
    /// readable as the last resolved direction until the next BeginAim.</summary>
    public void EndAim()
    {
        isAiming = false;
        SetIndicatorVisible(false);
    }

    private void Update()
    {
        if (!isAiming) return;

        // 1) Desired direction from the input (mouse-on-ice for laptop testing).
        Vector3 desired = GetDesiredAimDirection();

        // 2) Clamp into the manual cone (signed angle off the frozen axis).
        float rawSigned = PhysicsHelper.SignedAngleXZ(aimAxis, desired);
        rawSigned = Mathf.Clamp(rawSigned, -aimConeHalfAngle, aimConeHalfAngle);

        // 3) Net magnetism: pull the aim onto the post window, blended by assistStrength.
        float finalSigned = ApplyAssist(rawSigned);

        currentAimDir = RotateAroundY(aimAxis, finalSigned);

        if (logAim)
            Debug.Log($"[Aim] raw={rawSigned:F1}° final={finalSigned:F1}° dir=({currentAimDir.x:F2},{currentAimDir.z:F2})");
    }

    private void LateUpdate()
    {
        if (showIndicator && isAiming)
        {
            SetIndicatorVisible(true);
            UpdateIndicator();
        }
        else
        {
            SetIndicatorVisible(false);
        }
    }

    /// <summary>
    /// THE input source. Laptop: raycast the mouse onto the ice plane (y=0) and aim from the origin
    /// toward that point. Swap this body for touch-drag-from-the-shoot-button on mobile; everything
    /// downstream (cone clamp + assist) is unchanged.
    /// </summary>
    private Vector3 GetDesiredAimDirection()
    {
        if (aimCamera == null || Mouse.current == null) return currentAimDir;

        Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane ice = new Plane(Vector3.up, Vector3.zero);
        if (ice.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            Vector3 dir = PhysicsHelper.DirectionXZ(aimOrigin, hit);
            // Too close to the origin → unstable direction; keep the last good aim.
            if (dir.sqrMagnitude > 0.0001f && PhysicsHelper.DistanceXZ(aimOrigin, hit) > 0.3f)
                return dir;
        }
        return currentAimDir;
    }

    /// <summary>
    /// Net magnetism. The two posts subtend [lo, hi] degrees off the aim axis. If the raw aim is
    /// within that window (plus margin), blend it toward the clamped-into-window target by
    /// assistStrength. Inside the window the target equals the raw aim, so corner-picking is
    /// untouched; assist only acts as you stray toward/over a post.
    /// </summary>
    private float ApplyAssist(float rawSigned)
    {
        if (assistStrength <= 0f || netCollider == null) return rawSigned;

        Bounds b = netCollider.bounds;
        float postZ = b.center.z;
        Vector3 leftPost = new Vector3(b.min.x, 0f, postZ);
        Vector3 rightPost = new Vector3(b.max.x, 0f, postZ);

        float angA = PhysicsHelper.SignedAngleXZ(aimAxis, PhysicsHelper.DirectionXZ(aimOrigin, leftPost));
        float angB = PhysicsHelper.SignedAngleXZ(aimAxis, PhysicsHelper.DirectionXZ(aimOrigin, rightPost));
        float lo = Mathf.Min(angA, angB);
        float hi = Mathf.Max(angA, angB);

        // Net behind the player / outside reach → don't engage.
        if (rawSigned < lo - assistMargin || rawSigned > hi + assistMargin) return rawSigned;

        float target = Mathf.Clamp(rawSigned, lo, hi);
        return Mathf.Lerp(rawSigned, target, assistStrength);
    }

    /// <summary>
    /// Rotates an XZ direction by a signed angle expressed in PhysicsHelper.SignedAngleXZ's
    /// convention (Vector2 (x,z), CCW-positive). Unity's +Y Euler is CW from above, hence the negation.
    /// </summary>
    private static Vector3 RotateAroundY(Vector3 dir, float signedAngle)
    {
        return Quaternion.Euler(0f, -signedAngle, 0f) * dir;
    }

    // ---- Indicator -------------------------------------------------------

    private void BuildIndicator()
    {
        if (!showIndicator) return;

        Shader sh = Shader.Find("Sprites/Default");
        Material mat = sh != null ? new Material(sh) : null;

        aimLine = MakeLine("AimLine", mat, new Color(1f, 0.95f, 0.3f, 0.95f), 0.10f);   // bright yellow
        edgeLeft = MakeLine("AimEdgeL", mat, new Color(0.4f, 0.8f, 1f, 0.35f), 0.05f);  // faint cyan
        edgeRight = MakeLine("AimEdgeR", mat, new Color(0.4f, 0.8f, 1f, 0.35f), 0.05f);
    }

    private LineRenderer MakeLine(string lineName, Material mat, Color color, float width)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.widthMultiplier = width;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.View;
        if (mat != null) lr.material = mat;
        lr.startColor = color;
        lr.endColor = color;
        return lr;
    }

    private void UpdateIndicator()
    {
        Vector3 origin = aimOrigin + Vector3.up * indicatorHeight;

        SetLine(aimLine, origin, currentAimDir);
        SetLine(edgeLeft, origin, RotateAroundY(aimAxis, -aimConeHalfAngle));
        SetLine(edgeRight, origin, RotateAroundY(aimAxis, aimConeHalfAngle));
    }

    private void SetLine(LineRenderer lr, Vector3 origin, Vector3 dir)
    {
        if (lr == null) return;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin + dir.normalized * indicatorLength);
    }

    private void SetIndicatorVisible(bool visible)
    {
        if (aimLine != null) aimLine.enabled = visible;
        if (edgeLeft != null) edgeLeft.enabled = visible;
        if (edgeRight != null) edgeRight.enabled = visible;
    }
}

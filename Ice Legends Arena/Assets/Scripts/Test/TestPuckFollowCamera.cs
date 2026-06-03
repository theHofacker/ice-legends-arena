using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Broadcast-style camera for the test scene. Follows the puck on the XZ
/// gameplay plane while keeping a fixed elevation and angle.
///
/// Axes (locked-in for 3D migration):
///   X = rink WIDTH  (left/right)
///   Z = rink LENGTH (goal-to-goal)
///   Y = height
///
/// Add this to the Main Camera in TestMovement.unity. Auto-finds the puck
/// by tag.
/// </summary>
public class TestPuckFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Auto-found by Puck tag if left empty")]
    public Transform puckTransform;

    [Header("Camera Pose")]
    [Tooltip("Camera height above the ice")]
    [Range(8f, 50f)]
    public float height = 20f;

    [Tooltip("How far back the camera sits from its look-at point along -Z")]
    [Range(5f, 40f)]
    public float backOffsetZ = 22f;

    [Tooltip("How far the look-at point sits in front of the puck along +Z")]
    [Range(0f, 15f)]
    public float lookAheadZ = 5f;

    [Header("Follow Tightness")]
    [Tooltip("0 = camera doesn't move with puck on X, 1 = full follow")]
    [Range(0f, 1f)]
    public float followX = 0.7f;

    [Tooltip("0 = camera doesn't move with puck on Z, 1 = full follow. 1 feels best with the " +
             "directional follow — the camera fully tracks the puck to either goal with no " +
             "center-pull lag.")]
    [Range(0f, 1f)]
    public float followZ = 1f;

    [Tooltip("Lerp speed for camera position smoothing")]
    [Range(1f, 20f)]
    public float smoothSpeed = 6f;

    [Header("Predictive Lead")]
    public bool predictiveFollow = true;

    [Range(0f, 1.5f)]
    public float predictionAmount = 0.3f;

    [Header("Directional Follow")]
    [Tooltip("When ON, the camera swings to sit BEHIND the direction of play and look the way " +
             "you're heading — so skating toward your own goal brings it into view instead of " +
             "leaving it in a blind spot behind a fixed forward-facing camera. OFF (default) = a " +
             "fixed broadcast orientation that always looks toward +Z (the opponent goal) and just " +
             "slides forward/back with the puck — no rotation, ever.")]
    public bool directionAware = false;

    [Tooltip("Half-width (m) of the dead zone around center ice. The camera only re-orients " +
             "toward a goal once the puck is more than this far into that half, so carrying the " +
             "puck across the red line — or a hard shot / board ricochet near center — doesn't " +
             "flip-flop the facing. The flip is driven by which rink-half the puck is in, NOT by " +
             "puck velocity, which is what keeps a bouncing trick-shot puck from spinning the cam.")]
    [FormerlySerializedAs("flipVelocityThreshold")]
    [Range(0f, 15f)]
    public float flipDeadZoneZ = 3f;

    [Tooltip("How fast the camera swings around when play direction flips (higher = snappier). " +
             "It eases through an overhead angle during the swing, so keep it gentle for comfort.")]
    [Range(0.5f, 8f)]
    public float faceTurnSpeed = 2.5f;

    [Header("Bounds (XZ Plane)")]
    public bool constrainToBounds = true;
    public Vector2 minBounds = new Vector2(-12f, -22f); // (xMin, zMin)
    public Vector2 maxBounds = new Vector2(12f, 22f);   // (xMax, zMax)

    private Rigidbody puckRb;

    // Smoothed play-direction sign along Z: +1 = facing +Z (toward opponent goal, the original
    // behavior), -1 = facing -Z (toward own goal). Lerps between the two so the swing is gradual.
    private float faceSign = 1f;

    private void Start()
    {
        if (puckTransform == null)
        {
            GameObject puck = GameObject.FindGameObjectWithTag("Puck");
            if (puck != null)
            {
                puckTransform = puck.transform;
                puckRb = puck.GetComponent<Rigidbody>();
            }
            else
            {
                Debug.LogWarning("TestPuckFollowCamera: no GameObject tagged 'Puck' found.");
            }
        }
        else
        {
            puckRb = puckTransform.GetComponent<Rigidbody>();
        }
    }

    private void LateUpdate()
    {
        if (puckTransform == null) return;

        Vector3 puckPos = puckTransform.position;
        float targetX = puckPos.x;
        float targetZ = puckPos.z;

        if (predictiveFollow && puckRb != null)
        {
            Vector3 v = puckRb.linearVelocity;
            targetX += v.x * predictionAmount;
            targetZ += v.z * predictionAmount;
        }

        if (constrainToBounds)
        {
            targetX = Mathf.Clamp(targetX, minBounds.x, maxBounds.x);
            targetZ = Mathf.Clamp(targetZ, minBounds.y, maxBounds.y);
        }

        // Slack: camera tracks puck but stays loose for broadcast feel
        float anchorX = targetX * followX;
        float anchorZ = targetZ * followZ;

        // Direction-aware facing. Decide which goal the play is oriented toward from the puck's
        // rink-half — NOT its instantaneous velocity. A hard shot or board ricochet reverses puck
        // velocity for a moment without changing which end the play is actually in, so reading
        // POSITION keeps a bouncing trick-shot puck (deep behind a goal) from spinning the camera.
        // A dead zone around center ice (flipDeadZoneZ) gives hysteresis so carrying the puck
        // across the red line doesn't flip-flop the facing. faceSign: +1 = look toward +Z (opponent
        // goal, the original behavior), -1 = look toward -Z (own goal); faceTurnSpeed eases the
        // swing so it never snaps. (Possessed pucks report zero velocity anyway — the old velocity
        // read only ever saw loose pucks, i.e. exactly the shots/ricochets that shouldn't flip it.)
        if (directionAware)
        {
            if (puckPos.z > flipDeadZoneZ)
                faceSign = Mathf.MoveTowards(faceSign, 1f, faceTurnSpeed * Time.deltaTime);
            else if (puckPos.z < -flipDeadZoneZ)
                faceSign = Mathf.MoveTowards(faceSign, -1f, faceTurnSpeed * Time.deltaTime);
            // Inside the dead zone: hold the current facing.
        }
        else
        {
            faceSign = 1f; // disabled → original fixed orientation
        }

        // Rotate the back-offset / look-ahead around Y from the +Z facing (yaw 0) to the -Z
        // facing (yaw 180) as faceSign goes +1 → -1. Going AROUND the side this way keeps the
        // camera at a constant radius and never looks straight down (which would gimbal-snap a
        // naive sign flip). At faceSign = +1 this is identical to the old fixed camera.
        float yaw = (1f - faceSign) * 0.5f * 180f;
        Quaternion faceRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 anchor = new Vector3(anchorX, 0f, anchorZ);
        Vector3 desiredCamPos = anchor + faceRot * new Vector3(0f, 0f, -backOffsetZ) + Vector3.up * height;
        Vector3 lookAtPoint   = anchor + faceRot * new Vector3(0f, 0f, lookAheadZ);

        transform.position = Vector3.Lerp(transform.position, desiredCamPos, smoothSpeed * Time.deltaTime);
        transform.LookAt(lookAtPoint, Vector3.up);
    }

    private void OnDrawGizmosSelected()
    {
        if (!constrainToBounds) return;
        Gizmos.color = Color.yellow;
        Vector3 c1 = new Vector3(minBounds.x, 0f, minBounds.y);
        Vector3 c2 = new Vector3(minBounds.x, 0f, maxBounds.y);
        Vector3 c3 = new Vector3(maxBounds.x, 0f, maxBounds.y);
        Vector3 c4 = new Vector3(maxBounds.x, 0f, minBounds.y);
        Gizmos.DrawLine(c1, c2);
        Gizmos.DrawLine(c2, c3);
        Gizmos.DrawLine(c3, c4);
        Gizmos.DrawLine(c4, c1);
    }
}

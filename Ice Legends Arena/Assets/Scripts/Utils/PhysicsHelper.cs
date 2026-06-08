using UnityEngine;

/// <summary>
/// Utility methods for the 2D-to-3D physics migration.
/// Coordinate system: X = rink length, Z = rink width (was Y), Y = height above ice.
/// </summary>
public static class PhysicsHelper
{
    /// <summary>
    /// Converts 2D joystick input (x,y) to 3D world direction.
    /// Rink: X = width (left/right), Z = length (goal-to-goal).
    /// Camera is at negative Z looking toward +Z, so:
    ///   Joystick right (input.x +) = world +X (right on screen)
    ///   Joystick up (input.y +) = world +Z (away from camera = up on screen)
    /// </summary>
    public static Vector3 InputToWorld(Vector2 input)
    {
        return new Vector3(input.x, 0f, input.y);
    }

    /// <summary>
    /// Distance between two points on the XZ plane, ignoring Y (height).
    /// Use this instead of Vector3.Distance when height shouldn't affect proximity checks.
    /// </summary>
    public static float DistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>
    /// Direction from one point to another on the XZ plane (Y zeroed and normalized).
    /// </summary>
    public static Vector3 DirectionXZ(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        dir.y = 0f;
        return dir.normalized;
    }

    /// <summary>
    /// Projects a Vector3 onto the XZ plane (zeroes out Y).
    /// </summary>
    public static Vector3 FlattenY(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    /// <summary>
    /// Returns the XZ velocity magnitude, ignoring any vertical component.
    /// </summary>
    public static float SpeedXZ(Vector3 velocity)
    {
        return new Vector2(velocity.x, velocity.z).magnitude;
    }

    /// <summary>
    /// Converts a 2D formation offset (x,y) to 3D world position.
    /// In formation data: x = rink length offset, y = rink width offset.
    /// In world: Z = rink length, X = rink width.
    /// So: Vector2(length, width) → Vector3(width, 0, length).
    /// </summary>
    public static Vector3 ToWorldPosition(Vector2 oldPos)
    {
        return new Vector3(oldPos.y, 0f, oldPos.x);
    }

    /// <summary>
    /// Reverse of ToWorldPosition: world Vector3 → formation Vector2.
    /// World X (width) → Vector2.y, World Z (length) → Vector2.x.
    /// </summary>
    public static Vector2 ToFlatVector(Vector3 worldPos)
    {
        return new Vector2(worldPos.z, worldPos.x);
    }

    /// <summary>
    /// Random direction on the XZ plane (replaces Random.insideUnitCircle mapped to XZ).
    /// </summary>
    public static Vector3 RandomDirectionXZ()
    {
        Vector2 r = Random.insideUnitCircle;
        return new Vector3(r.x, 0f, r.y).normalized;
    }

    /// <summary>
    /// Signed angle between two directions on the XZ plane (around Y axis).
    /// Replaces Vector2.SignedAngle for 3D directions.
    /// </summary>
    public static float SignedAngleXZ(Vector3 from, Vector3 to)
    {
        Vector2 from2D = new Vector2(from.x, from.z);
        Vector2 to2D = new Vector2(to.x, to.z);
        return Vector2.SignedAngle(from2D, to2D);
    }

    /// <summary>
    /// Unsigned angle between two directions on the XZ plane.
    /// </summary>
    public static float AngleXZ(Vector3 from, Vector3 to)
    {
        return Mathf.Abs(SignedAngleXZ(from, to));
    }

    /// <summary>
    /// Intercept point on the XZ plane: where to aim a projectile of speed
    /// <paramref name="projectileSpeed"/> fired from <paramref name="shooter"/> so it meets a target
    /// currently at <paramref name="targetPos"/> moving at <paramref name="targetVel"/>. Two cheap
    /// refinement passes — good enough for passing, where the receiver actively catches and damping
    /// makes this a slight under-lead anyway. Falls back to the target's current spot if speed is ~0.
    /// Y is ignored for the math; the returned point keeps <paramref name="targetPos"/>.y.
    /// </summary>
    public static Vector3 LeadPointXZ(Vector3 shooter, Vector3 targetPos, Vector3 targetVel, float projectileSpeed)
    {
        if (projectileSpeed < 0.01f) return targetPos;
        Vector3 vel = FlattenY(targetVel);
        Vector3 lead = targetPos;
        for (int i = 0; i < 2; i++)
        {
            float t = DistanceXZ(shooter, lead) / projectileSpeed;
            lead = targetPos + vel * t;
        }
        lead.y = targetPos.y;
        return lead;
    }
}

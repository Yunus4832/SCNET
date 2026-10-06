namespace Game.Automation;

public static class AutomationViewSteering
{
    public static Vector3 Direction(float yawDegrees, float pitchDegrees)
    {
        if (!float.IsFinite(yawDegrees) || !float.IsFinite(pitchDegrees) || pitchDegrees is < -82 or > 82)
        {
            throw new ArgumentException("Yaw must be finite and pitch must be between -82 and 82 degrees.");
        }

        var yaw = MathUtils.DegToRad(yawDegrees % 360f);
        var pitch = MathUtils.DegToRad(pitchDegrees);
        return new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
    }

    public static Vector2 Calculate(Vector3 current, Vector3 desired)
    {
        var yaw = current.XZ.LengthSquared() > 0.0001f && desired.XZ.LengthSquared() > 0.0001f
            ? Vector2.Angle(current.XZ, desired.XZ)
            : 0f;
        var pitch = MathF.Asin(Math.Clamp(desired.Y, -1f, 1f)) -
                    MathF.Asin(Math.Clamp(current.Y, -1f, 1f));
        return new Vector2(Math.Clamp(yaw * 2f, -1f, 1f), Math.Clamp(pitch * 2f, -1f, 1f));
    }
}

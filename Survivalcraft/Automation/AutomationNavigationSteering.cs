namespace Game.Automation;

public static class AutomationNavigationSteering
{
    public static Vector3 SelectTarget(Vector3 waypoint, Vector3 destination, float range, bool finalWaypoint) =>
        finalWaypoint && Vector3.Distance(waypoint, destination) <= range ? destination : waypoint;

    public static PlayerInput Calculate(Vector3 position, Vector3 forward, Vector3 waypoint)
    {
        var delta = waypoint - position;
        if (delta.XZ.LengthSquared() < 0.0001f || forward.XZ.LengthSquared() < 0.0001f)
        {
            return default;
        }

        var angle = Vector2.Angle(forward.XZ, delta.XZ);
        var movement = new Vector3(0, 0,
            Math.Abs(angle) < 0.5f ? Math.Clamp(delta.XZ.Length(), 0.1f, 0.8f) : 0f);
        return new PlayerInput
        {
            Look = new Vector2(Math.Clamp(angle * 2f, -1f, 1f), 0f),
            Move = movement,
            SneakMove = movement
        };
    }
}

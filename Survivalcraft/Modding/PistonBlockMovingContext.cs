namespace Game.Modding;

public sealed class PistonBlockMovingContext(
    SubsystemPistonBlockBehavior pistons,
    Point3 pistonPoint,
    Point3 point,
    bool isPulling)
{
    public SubsystemPistonBlockBehavior Pistons { get; } = pistons;
    public Point3 PistonPoint { get; } = pistonPoint;
    public Point3 Point { get; } = point;
    public bool IsPulling { get; } = isPulling;
    public bool Cancel { get; set; }
}

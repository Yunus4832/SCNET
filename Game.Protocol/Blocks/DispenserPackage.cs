using Engine.Core;

namespace Game.Protocol.Blocks;

public sealed record DispenserPackage : IProtocolMessage
{
    public byte Flag { get; init; }
    public Point3 Point { get; init; }
    public byte ID => (byte)PackageType.Dispenser;
    public DispenserPackage()
    {
    }
    public DispenserPackage(Point3 point, byte flag)
    {
        Point = point;
        Flag = flag;
    }
}

using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record DispenserPackage : IProtocolMessage
{
    [Key(0)] public byte Flag { get; init; }

    [Key(1)] public Point3 Point { get; init; }

    public DispenserPackage()
    {
    }

    public DispenserPackage(Point3 point, byte flag)
    {
        Point = point;
        Flag = flag;
    }
}

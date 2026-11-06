using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record DispenserSoundPackage : IProtocolMessage
{
    [Key(0)] public Vector3 Position { get; init; }

    [Key(1)] public bool Shot { get; init; }
}

using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record PistonSoundPackage : MovingBlockPackage
{
    [IgnoreMember] public override EventType Type => EventType.PistonSound;

    [Key(2)] public Vector3 Position { get; init; }
}

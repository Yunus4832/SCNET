using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.World;

[MessagePackObject]
public sealed record ExplosionSoundPackage : ExplosionsPackage
{
    [IgnoreMember] public override EventType Type => EventType.Sound;

    [Key(0)] public Vector3 Position { get; init; }

    [Key(1)] public float Level { get; init; }

    [Key(2)] public float Delay { get; init; }
}

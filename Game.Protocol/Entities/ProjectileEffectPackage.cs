using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record ProjectileEffectPackage : ProjectilePackage
{
    [IgnoreMember] public override EventType Type => EventType.Effect;

    [Key(1)] public EffectType Effect { get; init; }

    [Key(2)] public int Value { get; init; }

    [Key(3)] public Vector3 Position { get; init; }
}

using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record BlockBurnEffectPackage : ComponentOnFirePackage
{
    [IgnoreMember] public override EventType Type => EventType.BlockBurnEffect;

    [Key(0)] public Vector3 Position { get; init; }

    [Key(1)] public BlockBurnEffectKind Kind { get; init; }
}

using Engine.Core.Mathematics;

using Game.Protocol.Localization;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CharacterHitEffectPackage : ComponentHealthPackage
{
    [IgnoreMember] public override EventType Type => EventType.HitResult;

    [Key(0)] public Vector3 Position { get; init; }

    [Key(1)] public Vector3 Velocity { get; init; }

    [Key(2)] public Color Color { get; init; }

    [Key(3)] public LocalizedText Text { get; init; } = LocalizedText.Empty;

    [Key(4)] public bool PlayBodyImpact { get; init; }

    [Key(5)] public Vector3 BodyPosition { get; init; }
}

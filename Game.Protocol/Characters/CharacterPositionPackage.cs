using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterPositionPackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.PositionSet;

    [Key(1)] public Vector3 Position { get; init; }

    [Key(2)] public Vector3 Velocity { get; init; }
}

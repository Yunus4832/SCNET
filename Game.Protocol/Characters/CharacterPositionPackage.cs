using Engine.Core.Mathematics;

namespace Game.Protocol.Characters;

public sealed record CharacterPositionPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.PositionSet;

    public Vector3 Position { get; init; }

    public Vector3 Velocity { get; init; }
}

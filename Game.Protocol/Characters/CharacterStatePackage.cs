using Engine.Core.Mathematics;

namespace Game.Protocol.Characters;

public sealed record CharacterStatePackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.BodyUpdate;

    public ChangFlag PackageChangeFlag { get; init; }

    public Vector2 ChildLookAngles { get; init; }

    public Vector2 LookAngles { get; init; }

    public int LadderValue { get; init; }

    public bool Sneaking { get; init; }

    public Vector3 Position { get; init; }

    public Quaternion Rotation { get; init; }

    public Vector3 Velocity { get; init; }
}

using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterStatePackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.BodyUpdate;

    [Key(1)] public ChangFlag PackageChangeFlag { get; init; }

    [Key(2)] public Vector2 ChildLookAngles { get; init; }

    [Key(3)] public Vector2 LookAngles { get; init; }

    [Key(4)] public int LadderValue { get; init; }

    [Key(5)] public bool Sneaking { get; init; }

    [Key(6)] public Vector3 Position { get; init; }

    [Key(7)] public Quaternion Rotation { get; init; }

    [Key(8)] public Vector3 Velocity { get; init; }
}

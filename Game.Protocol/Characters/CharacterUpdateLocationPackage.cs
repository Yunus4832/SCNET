using Game.Protocol.Terrain;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterUpdateLocationPackage : CharacterDataPackage
{
    [IgnoreMember] public override DataType Type => DataType.SetUpdateLocation;

    [Key(0)] public TerrainUpdateLocation UpdateLocation { get; init; }
}

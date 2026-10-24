using Game.Protocol.Terrain;

namespace Game.Protocol.Characters;

public sealed record CharacterDataPackage : IProtocolMessage
{
    public enum DataType
    {
        Create = 0,

        Modify = 1,

        CreationRejected = 2,

        // Tags 4–6 retired: unsent legacy notification modes.
        SetUpdateLocation = 3
    }

    public Guid CreationRequestId { get; init; }

    public CharacterCreationError CreationError { get; init; }

    public byte CharacterSex { get; init; }

    public Guid CharacterId { get; init; }

    public string CharacterName { get; init; } = string.Empty;

    public string SkinName { get; init; } = string.Empty;

    public DataType Type { get; init; }

    public TerrainUpdateLocation UpdateLocation { get; init; }

    public byte ID => (byte)PackageType.CharacterData;

    public CharacterDataPackage()
    {
    }

    public CharacterDataPackage(TerrainUpdateLocation location)
    {
        UpdateLocation = location;
        Type = DataType.SetUpdateLocation;
    }
}

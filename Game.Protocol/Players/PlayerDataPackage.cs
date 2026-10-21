using Game.Protocol.Terrain;

namespace Game.Protocol.Players;

public sealed record PlayerDataPackage : IProtocolMessage
{
    public enum DataType
    {
        Create = 0,

        Modify = 1,

        // Tags 2 and 4–6 retired: unsent legacy player-delete and notification modes.
        SetUpdateLocation = 3
    }

    public byte PlayerClass { get; init; }

    public Guid PlayerGuid { get; init; }

    public string PlayerName { get; init; } = string.Empty;

    public string SkinName { get; init; } = string.Empty;

    public DataType Type { get; init; }

    public TerrainUpdateLocation UpdateLocation { get; init; }

    public byte ID => (byte)PackageType.PlayerData;

    public PlayerDataPackage()
    {
    }

    public PlayerDataPackage(TerrainUpdateLocation location)
    {
        UpdateLocation = location;
        Type = DataType.SetUpdateLocation;
    }
}

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class PlayerListPackage : IPackage
{
    public readonly List<PlayerListEntry> Players = [];

    public byte ID => (byte)PackageType.PlayerList;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public PlayerListPackage()
    {
    }

    public PlayerListPackage(SubsystemPlayers subsystemPlayers)
    {
        subsystemPlayers.RefreshPlayerList();
        Players.AddRange(subsystemPlayers.PlayerList.Values);
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write((ushort)Players.Count);
        foreach (var player in Players)
        {
            writer.Write(player.PlayerGuid);
            writer.Write(player.Name);
            writer.Write(player.IsOnline);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        var count = reader.ReadUInt16();
        for (var i = 0; i < count; i++)
        {
            Players.Add(new PlayerListEntry(
                reader.ReadGuid(),
                reader.ReadString(),
                reader.ReadBoolean()));
        }
    }
}

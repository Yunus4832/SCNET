using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class OnlinePlayerStatePackage : IPackage
{
    public readonly List<OnlinePlayerState> Players = [];

    public byte ID => (byte)PackageType.OnlinePlayerState;




    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public OnlinePlayerStatePackage()
    {
    }

    public OnlinePlayerStatePackage(IEnumerable<OnlinePlayerState> players)
    {
        Players.AddRange(players);
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write((ushort)Players.Count);
        foreach (var player in Players)
        {
            writer.Write(player.PlayerGuid);
            writer.Write(player.Position);
            writer.Write(player.Health);
            writer.Write(player.IsSleeping);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        var count = reader.ReadUInt16();
        for (var i = 0; i < count; i++)
        {
            Players.Add(new OnlinePlayerState(
                reader.ReadGuid(),
                reader.ReadVector3(),
                reader.ReadSingle(),
                reader.ReadBoolean()));
        }
    }
}

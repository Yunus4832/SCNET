using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class OnlinePlayerStatePackage : IPackage
{
    public readonly List<OnlinePlayerState> Players = [];

    public byte ID => (byte)PackageType.OnlinePlayerState;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public OnlinePlayerStatePackage()
    {
    }

    public OnlinePlayerStatePackage(SubsystemPlayers subsystemPlayers)
    {
        foreach (var playerData in subsystemPlayers.PlayersData)
        {
            if (playerData.ComponentPlayer is not { } player)
            {
                continue;
            }

            Players.Add(new OnlinePlayerState(
                playerData.PlayerGUID,
                player.ComponentBody.Position,
                MathUtils.Saturate(player.ComponentHealth.Health),
                player.ComponentSleep.IsSleeping));
        }
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

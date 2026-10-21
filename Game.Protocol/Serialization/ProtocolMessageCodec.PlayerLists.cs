using Game.Protocol.Players;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WritePlayerListPackage(PlayerListPackage message, ProtocolWriter writer)
    {
        if (message.Players.Count > ushort.MaxValue) { throw new ArgumentException("Too many players."); }

        writer.Write((ushort)message.Players.Count);
        foreach (var player in message.Players)
        {
            writer.Write(player.PlayerGuid);
            writer.Write(player.Name);
            writer.Write(player.IsOnline);
        }
    }

    private static PlayerListPackage ReadPlayerListPackage(ProtocolReader reader)
    {
        var count = reader.ReadUInt16();
        EnsureRemaining(reader, count * 19);
        var players = new PlayerListEntry[count];
        for (var i = 0; i < count; i++)
        {
            players[i] = new PlayerListEntry(reader.ReadGuid(), reader.ReadString(), reader.ReadBoolean());
        }

        return new PlayerListPackage { Players = players };
    }

    private static void WriteOnlinePlayerStatePackage(OnlinePlayerStatePackage message, ProtocolWriter writer)
    {
        if (message.Players.Count > ushort.MaxValue) { throw new ArgumentException("Too many players."); }

        writer.Write((ushort)message.Players.Count);
        foreach (var player in message.Players)
        {
            writer.Write(player.PlayerGuid);
            writer.Write(player.Position);
            writer.Write(player.Health);
            writer.Write(player.IsSleeping);
        }
    }

    private static OnlinePlayerStatePackage ReadOnlinePlayerStatePackage(ProtocolReader reader)
    {
        var count = reader.ReadUInt16();
        EnsureRemaining(reader, count * 34);
        var players = new OnlinePlayerState[count];
        for (var i = 0; i < count; i++)
        {
            players[i] = new OnlinePlayerState(
                reader.ReadGuid(),
                reader.ReadVector3(),
                reader.ReadSingle(),
                reader.ReadBoolean()
            );
        }

        return new OnlinePlayerStatePackage { Players = players };
    }
}

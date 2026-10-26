using Game.Protocol.Characters;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WritePlayerListPackage(CharacterListPackage message, ProtocolWriter writer)
    {
        if (message.Characters.Count > ushort.MaxValue) { throw new ArgumentException("Too many players."); }

        writer.Write((ushort)message.Characters.Count);
        foreach (var player in message.Characters)
        {
            writer.Write(player.CharacterId);
            PlayerAddressCodec.Write(writer, player.OwnerAddress);
            writer.Write(player.Name);
            writer.Write(player.IsOnline);
        }
    }

    private static CharacterListPackage ReadPlayerListPackage(ProtocolReader reader)
    {
        var count = reader.ReadUInt16();
        EnsureRemaining(reader, count * 63);
        var players = new CharacterListEntry[count];
        for (var i = 0; i < count; i++)
        {
            players[i] = new CharacterListEntry(
                reader.ReadGuid(),
                PlayerAddressCodec.Read(reader),
                reader.ReadString(),
                reader.ReadBoolean()
            );
        }

        return new CharacterListPackage { Characters = players };
    }

    private static void WriteOnlineCharacterStatePackage(OnlineCharacterStatePackage message, ProtocolWriter writer)
    {
        if (message.Characters.Count > ushort.MaxValue) { throw new ArgumentException("Too many players."); }

        writer.Write((ushort)message.Characters.Count);
        foreach (var player in message.Characters)
        {
            writer.Write(player.CharacterId);
            writer.Write(player.Position);
            writer.Write(player.Health);
            writer.Write(player.IsSleeping);
        }
    }

    private static OnlineCharacterStatePackage ReadOnlineCharacterStatePackage(ProtocolReader reader)
    {
        var count = reader.ReadUInt16();
        EnsureRemaining(reader, count * 34);
        var players = new OnlineCharacterState[count];
        for (var i = 0; i < count; i++)
        {
            players[i] = new OnlineCharacterState(
                reader.ReadGuid(),
                reader.ReadVector3(),
                reader.ReadSingle(),
                reader.ReadBoolean()
            );
        }

        return new OnlineCharacterStatePackage { Characters = players };
    }
}

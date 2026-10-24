using Game.Protocol.Connection;
using Game.Protocol.Identity;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static ClientIdentity ReadClientIdentity(ProtocolReader reader)
    {
        var id = reader.ReadByte();
        var token = reader.ReadGuid();
        var character = reader.ReadGuid();
        var address = id == 0 ? PlayerAddressCodec.ReadOptional(reader) : PlayerAddressCodec.Read(reader);
        var state = reader.ReadEnum<ClientState>();
        if (!Enum.IsDefined(state)) { throw new InvalidDataException("Unknown client connection state."); }

        return new ClientIdentity(id, token, character, address, state);
    }

    private static void WriteClientIdentity(ClientIdentity client, ProtocolWriter writer)
    {
        writer.Write(client.ID);
        writer.Write(client.TokenId);
        writer.Write(client.CharacterId);
        if (client.ID == 0) { PlayerAddressCodec.WriteOptional(writer, client.PlayerAddress); }
        else
        {
            PlayerAddressCodec.Write(
                writer,
                client.PlayerAddress ?? throw new ArgumentException("A subject address is required.")
            );
        }

        if (!Enum.IsDefined(client.State)) { throw new ArgumentException("Unknown client connection state."); }

        writer.WriteEnum(client.State);
    }

    private static ClientPackage ReadClient(ProtocolReader reader)
    {
        var mode = reader.ReadEnum<ClientPackage.EventType>();
        switch (mode)
        {
            case ClientPackage.EventType.Add:
                return new ClientPackage { PackageEventType = mode, Client = ReadClientIdentity(reader) };
            case ClientPackage.EventType.Remove:
                return new ClientPackage(reader.ReadByte());
            case ClientPackage.EventType.StateChange:
                var id = reader.ReadByte();
                var state = reader.ReadEnum<ClientState>();
                if (!Enum.IsDefined(state))
                {
                    throw new InvalidDataException("Unknown client connection state.");
                }

                return new ClientPackage(id, state);
            case ClientPackage.EventType.SyncList:
                var count = reader.ReadByte();
                var clients = new ClientIdentity[count];
                var ids = new HashSet<byte>();
                var subjects = new HashSet<PlayerAddress>();
                for (var i = 0; i < count; i++)
                {
                    clients[i] = ReadClientIdentity(reader);
                    if (!ids.Add(clients[i].ID))
                    {
                        throw new InvalidDataException("Duplicate client ID.");
                    }

                    if (clients[i].PlayerAddress is { } address && !subjects.Add(address))
                    {
                        throw new InvalidDataException("Duplicate subject session.");
                    }
                }

                return new ClientPackage(clients);
            default:
                throw new InvalidDataException("Unknown client message mode.");
        }
    }

    private static void WriteClient(ClientPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.PackageEventType);
        switch (message.PackageEventType)
        {
            case ClientPackage.EventType.Add:
                WriteClientIdentity(message.Client ?? throw new ArgumentException("Missing client identity."), writer);
                break;
            case ClientPackage.EventType.Remove:
                writer.Write(message.Client?.ID ?? throw new ArgumentException("Missing client ID."));
                break;
            case ClientPackage.EventType.StateChange:
                var client = message.Client ?? throw new ArgumentException("Missing client identity.");
                if (!Enum.IsDefined(client.State))
                {
                    throw new ArgumentException("Unknown client state.");
                }

                writer.Write(client.ID);
                writer.WriteEnum(client.State);
                break;
            case ClientPackage.EventType.SyncList:
                if (message.List.Count > byte.MaxValue ||
                    message.List.Select(item => item.ID).Distinct().Count() != message.List.Count ||
                    message.List.Where(item => item.PlayerAddress is not null).GroupBy(item => item.PlayerAddress)
                        .Any(group => group.Count() > 1))
                {
                    throw new ArgumentException("Invalid client identity list.");
                }

                writer.Write((byte)message.List.Count);
                foreach (var item in message.List)
                {
                    WriteClientIdentity(item, writer);
                }

                break;
            default:
                throw new ArgumentException("Unknown client message mode.");
        }
    }
}

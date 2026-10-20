using Game.Protocol.Connection;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static ClientIdentity ReadClientIdentity(ProtocolReader reader) =>
        new(reader.ReadByte(), reader.ReadGuid(), reader.ReadGuid());

    private static void WriteClientIdentity(ClientIdentity client, ProtocolWriter writer)
    {
        writer.Write(client.ID);
        writer.Write(client.TokenId);
        writer.Write(client.GUID);
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
                for (var i = 0; i < count; i++)
                {
                    clients[i] = ReadClientIdentity(reader);
                    if (!ids.Add(clients[i].ID))
                    {
                        throw new InvalidDataException("Duplicate client ID.");
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
                if (message.List.Count > byte.MaxValue || message.List.Select(item => item.ID).Distinct().Count() != message.List.Count)
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

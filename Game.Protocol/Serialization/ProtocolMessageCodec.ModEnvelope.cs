using Game.Protocol.Connection;
using Game.Protocol.Mods;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteModEnvelopePackage(ModEnvelopePackage message, ProtocolWriter writer)
    {
        writer.Write(message.ModId);
        writer.Write(message.MessageType);
        writer.WriteEnum(message.RequiredState);
        writer.WriteBuff(message.Payload);
    }

    private static ModEnvelopePackage ReadModEnvelopePackage(ProtocolReader reader)
    {
        var message = new ModEnvelopePackage
        {
            ModId = reader.ReadString(), MessageType = reader.ReadString(),
            RequiredState = reader.ReadEnum<ClientState>(), Payload = reader.ReadBuff()
        };
        if (!Enum.IsDefined(message.RequiredState))
        {
            throw new InvalidDataException("Unknown mod message connection state.");
        }

        return message;
    }
}

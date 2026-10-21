using Game.Protocol.Blocks;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteDispenserPackage(DispenserPackage message, ProtocolWriter writer)
    {
        writer.Write(message.Point);
        writer.Write(message.Flag);
    }

    private static DispenserPackage ReadDispenserPackage(ProtocolReader reader) => new()
        { Point = reader.ReadPoint3(), Flag = reader.ReadByte() };
}

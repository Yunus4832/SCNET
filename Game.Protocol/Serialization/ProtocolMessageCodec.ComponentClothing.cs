using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteComponentClothingPackage(ComponentClothingPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        switch (message.Type)
        {
            case ComponentClothingPackage.DataType.WhoHas:
            case ComponentClothingPackage.DataType.RequestSkin:
                writer.Write(message.SkinName);
                break;
            case ComponentClothingPackage.DataType.WhoHasReply:
            case ComponentClothingPackage.DataType.ReplySkin:
                writer.Write(message.SkinName);
                writer.Write(message.SkinData.Length);
                writer.Write(message.SkinData);
                break;
        }
    }

    private static ComponentClothingPackage ReadComponentClothingPackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<ComponentClothingPackage.DataType>();
        if (!Enum.IsDefined(type)) { throw new InvalidDataException("Unknown clothing message mode."); }
        var skin = reader.ReadString();
        var data = type is ComponentClothingPackage.DataType.ReplySkin or ComponentClothingPackage.DataType.WhoHasReply ? reader.ReadBuff() : [];
        return new ComponentClothingPackage(skin, type) { SkinData = data };
    }
}

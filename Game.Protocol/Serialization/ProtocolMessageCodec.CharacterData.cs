using Game.Protocol.Characters;
using Game.Protocol.Teams;
using Game.Protocol.Terrain;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteCharacterDataPackage(CharacterDataPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        switch (message.Type)
        {
            case CharacterDataPackage.DataType.Create:
                writer.Write(message.CreationRequestId);
                writer.Write(message.CharacterName);
                writer.Write(message.SkinName);
                writer.Write(message.CharacterSex);
                break;
            case CharacterDataPackage.DataType.CreationRejected:
                writer.Write(message.CreationRequestId);
                writer.WriteEnum(message.CreationError);
                break;
            case CharacterDataPackage.DataType.Modify:
                writer.Write(message.CharacterId);
                writer.Write(message.CharacterName);
                writer.Write(message.SkinName);
                writer.Write(message.CharacterSex);
                break;
            case CharacterDataPackage.DataType.SetUpdateLocation:
                writer.Write(message.UpdateLocation.Center);
                writer.Write((ushort)message.UpdateLocation.ContentDistance);
                writer.Write((ushort)message.UpdateLocation.VisibilityDistance);
                writer.Write(message.UpdateLocation.LastChunksUpdateCenter);
                break;
            default: throw new ArgumentException("Unknown player data mode.");
        }
    }

    private static CharacterDataPackage ReadCharacterDataPackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<CharacterDataPackage.DataType>();
        return type switch
        {
            CharacterDataPackage.DataType.Create => new CharacterDataPackage
            {
                Type = type, CreationRequestId = reader.ReadGuid(), CharacterName = reader.ReadString(),
                SkinName = reader.ReadString(),
                CharacterSex = reader.ReadByte()
            },
            CharacterDataPackage.DataType.CreationRejected => new CharacterDataPackage
            {
                Type = type, CreationRequestId = reader.ReadGuid(),
                CreationError = reader.ReadEnum<CharacterCreationError>()
            },
            CharacterDataPackage.DataType.Modify => new CharacterDataPackage
            {
                Type = type, CharacterId = reader.ReadGuid(), CharacterName = reader.ReadString(),
                SkinName = reader.ReadString(), CharacterSex = reader.ReadByte()
            },
            CharacterDataPackage.DataType.SetUpdateLocation => new CharacterDataPackage(
                new TerrainUpdateLocation
                {
                    Center = reader.ReadVector2(), ContentDistance = reader.ReadUInt16(),
                    VisibilityDistance = reader.ReadUInt16(), LastChunksUpdateCenter = reader.ReadVector2Nullable()
                }
            ),
            _ => throw new InvalidDataException("Unknown player data mode.")
        };
    }
}

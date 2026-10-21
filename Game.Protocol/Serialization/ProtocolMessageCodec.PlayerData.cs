using Game.Protocol.Players;
using Game.Protocol.Terrain;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WritePlayerDataPackage(PlayerDataPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        switch (message.Type)
        {
            case PlayerDataPackage.DataType.Create:
                writer.Write(message.PlayerName);
                writer.Write(message.SkinName);
                writer.Write(message.PlayerClass);
                break;
            case PlayerDataPackage.DataType.Modify:
                writer.Write(message.PlayerGuid);
                writer.Write(message.PlayerName);
                writer.Write(message.SkinName);
                writer.Write(message.PlayerClass);
                break;
            case PlayerDataPackage.DataType.SetUpdateLocation:
                writer.Write(message.UpdateLocation.Center);
                writer.Write((ushort)message.UpdateLocation.ContentDistance);
                writer.Write((ushort)message.UpdateLocation.VisibilityDistance);
                writer.Write(message.UpdateLocation.LastChunksUpdateCenter);
                break;
            default: throw new ArgumentException("Unknown player data mode.");
        }
    }

    private static PlayerDataPackage ReadPlayerDataPackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<PlayerDataPackage.DataType>();
        return type switch
        {
            PlayerDataPackage.DataType.Create => new PlayerDataPackage
            {
                Type = type, PlayerName = reader.ReadString(), SkinName = reader.ReadString(),
                PlayerClass = reader.ReadByte()
            },
            PlayerDataPackage.DataType.Modify => new PlayerDataPackage
            {
                Type = type, PlayerGuid = reader.ReadGuid(), PlayerName = reader.ReadString(),
                SkinName = reader.ReadString(), PlayerClass = reader.ReadByte()
            },
            PlayerDataPackage.DataType.SetUpdateLocation => new PlayerDataPackage(
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

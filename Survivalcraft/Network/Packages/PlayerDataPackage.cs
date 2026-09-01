using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class PlayerDataPackage : IPackage
{
    public enum DataType
    {
        Create,
        Modify,
        Delete,
        SetUpdateLocation,
        CloseTime,
        Bugle,
        Count
    }

    public string BugleContent = string.Empty; //小喇叭内容

    public string BugleTitle = string.Empty; //小喇叭标题

    public PlayerClass PlayerClass;

    public int PlayerCount; //玩家人数

    public Guid PlayerGuid;

    public string PlayerName = string.Empty;

    public string SkinName = string.Empty;

    public DataType Type;

    public TerrainUpdater.UpdateLocation UpdateLocation;

    public ValuesDictionary? Vd;

    public int Visibility;

    public byte ID => (byte)PackageType.PlayerData;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Connected;

    public PlayerDataPackage()
    {
    }

    public PlayerDataPackage(PlayerData playerData, DataType dataType)
    {
        Vd = new ValuesDictionary();
        Type = dataType;
        PlayerName = playerData.Name;
        SkinName = playerData.CharacterSkinName;
        PlayerGuid = playerData.PlayerGUID;
        PlayerClass = playerData.PlayerClass;
        playerData.Save(Vd);
    }

    public PlayerDataPackage(int time, string msg)
    {
        PlayerName = msg;
        Type = DataType.CloseTime;
        Visibility = time;
    }

    public PlayerDataPackage(TerrainUpdater.UpdateLocation updateLocation)
    {
        UpdateLocation = updateLocation;
        Type = DataType.SetUpdateLocation;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        switch (Type)
        {
            case DataType.Create:
                if (Vd != null)
                {
                    var messagePack = Vd.ToMessagePack();
                    writer.WriteBuff(messagePack);
                }

                break;
            case DataType.Modify:
                writer.Write(PlayerGuid);
                writer.Write(PlayerName);
                writer.Write(SkinName);
                writer.WriteEnum(PlayerClass);
                break;
            case DataType.SetUpdateLocation:
                writer.Write(UpdateLocation.Center);
                writer.Write((ushort)UpdateLocation.ContentDistance);
                writer.Write((ushort)UpdateLocation.VisibilityDistance);
                writer.Write(UpdateLocation.LastChunksUpdateCenter);
                break;
            case DataType.CloseTime:
                writer.Write(Visibility);
                writer.Write(PlayerName);
                break;
            case DataType.Bugle:
                writer.Write(BugleTitle);
                writer.Write(BugleContent);
                break;
            case DataType.Count:
                writer.Write(PlayerCount);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<DataType>();
        switch (Type)
        {
            case DataType.Create:
                var messagePack = reader.ReadBuff();
                Vd = new ValuesDictionary();
                Vd.ApplyOverridesUseMessagePack(messagePack);
                break;
            case DataType.Modify:
                PlayerGuid = reader.ReadGuid();
                PlayerName = reader.ReadString();
                SkinName = reader.ReadString();
                PlayerClass = reader.ReadEnum<PlayerClass>();
                break;
            case DataType.SetUpdateLocation:
                UpdateLocation = new TerrainUpdater.UpdateLocation();
                UpdateLocation.Center = reader.ReadVector2();
                UpdateLocation.ContentDistance = reader.ReadUInt16();
                UpdateLocation.VisibilityDistance = reader.ReadUInt16();
                UpdateLocation.LastChunksUpdateCenter = reader.ReadVector2Nullable();
                break;
            case DataType.CloseTime:
                Visibility = reader.ReadInt32();
                PlayerName = reader.ReadString();
                break;
            case DataType.Bugle:
                BugleTitle = reader.ReadString();
                BugleContent = reader.ReadString();
                break;
            case DataType.Count:
                PlayerCount = reader.ReadInt32();
                break;
        }
    }
}

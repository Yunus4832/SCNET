using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

/// <summary>
/// 基础包模板复制
/// </summary>
public class ServerInfoPackage : IPackage
{
    public ushort ClientCount;

    public GameMode GameMode;

    public ushort MaxPlayerCount;

    public string ModServerAddress = string.Empty;

    public bool NeedLogin;

    public bool NeedPasswd;

    public bool RequestInfo;

    /// <summary>
    /// 季节
    /// </summary>
    public Season Season;

    public float TimeOfDay;

    /// <summary>
    /// 季节进度
    /// </summary>
    public float TimeOfSeason;

    public string Version = string.Empty;

    public int Ping;

    public byte ID => (byte)PackageType.ServerInfo;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ServerInfoPackage()
    {
    }


    public ServerInfoPackage(bool requestInfo)
    {
        RequestInfo = requestInfo;
        if (RequestInfo)
        {
            return;
        }


        if (GameManager.Project is null)
        {
            throw new InvalidOperationException("GameManager is not ready");
        }

        var project = GameManager.Project;
        var subsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true)!;
        var subsystemTimeOfDay = project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        var subsystemSeasons = project.FindSubsystem<SubsystemSeasons>(true)!;

        Version = VersionsManager.ProtocolVersion;
        ClientCount = (ushort)CommonLib.Net.ClientCount;
        MaxPlayerCount = subsystemGameInfo.WorldSettings.MaxOnlinePlayerCount;
        GameMode = subsystemGameInfo.WorldSettings.GameMode;
        NeedLogin = subsystemGameInfo.WorldSettings.IsNeedCommunityLogin;
        NeedPasswd = !string.IsNullOrEmpty(subsystemGameInfo.WorldSettings.Password);
        TimeOfDay = subsystemTimeOfDay.CalculateTimeOfDay();
        ModServerAddress = SettingsManager.ModServerAddress;
        Season = subsystemSeasons.Season;
        TimeOfSeason = subsystemSeasons.TimeOfSeason;
    }


    public void ReadData(PackageStreamReader reader)
    {
        RequestInfo = reader.ReadBoolean();
        if (RequestInfo)
        {
            return;
        }

        Version = reader.ReadString();
        ClientCount = reader.ReadUInt16();
        MaxPlayerCount = reader.ReadUInt16();
        GameMode = reader.ReadEnum<GameMode>();
        NeedLogin = reader.ReadBoolean();
        NeedPasswd = reader.ReadBoolean();
        TimeOfDay = reader.ReadSingle();
        // 如果不考虑兼容03.04版本可以删掉try-catch语句
        try
        {
            ModServerAddress = reader.ReadString();
        }
        catch
        {
            ModServerAddress = string.Empty;
        }

        Season = (Season)reader.ReadInt32();
        TimeOfSeason = reader.ReadSingle();
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(RequestInfo);
        if (RequestInfo)
        {
            return;
        }

        writer.Write(Version);
        writer.Write(ClientCount);
        writer.Write(MaxPlayerCount);
        writer.WriteEnum(GameMode);
        writer.Write(NeedLogin);
        writer.Write(NeedPasswd);
        writer.Write(TimeOfDay);
        writer.Write(ModServerAddress);
        writer.Write((int)Season);
        writer.Write(TimeOfSeason);
    }
}

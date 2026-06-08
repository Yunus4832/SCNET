using Game.Network.Enums;
using Game.Network.Serialization;

using static Game.Screens.NetPlayScreen;

namespace Game.Network.Packages;

/// <summary>
/// 基础包模板复制
/// </summary>
public class ServerInfoPackage : IPackage
{
    private ushort _clientCount;

    private GameMode _gameMode;

    private ushort _maxPlayerCount;

    private string _modServerAddress = string.Empty;

    private bool _needLogin;

    private bool _needPasswd;

    private bool _requestInfo;

    /// <summary>
    /// 季节
    /// </summary>
    private Season _season;

    private float _timeOfDay;

    /// <summary>
    /// 季节进度
    /// </summary>
    private float _timeOfSeason;

    private string _version = string.Empty;

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
        _requestInfo = requestInfo;
        if (_requestInfo)
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

        _version = VersionsManager.ProtocolVersion;
        _clientCount = (ushort)CommonLib.Net.ClientCount;
        _maxPlayerCount = subsystemGameInfo.WorldSettings.MaxOnlinePlayerCount;
        _gameMode = subsystemGameInfo.WorldSettings.GameMode;
        _needLogin = subsystemGameInfo.WorldSettings.IsNeedCommunityLogin;
        _needPasswd = !string.IsNullOrEmpty(subsystemGameInfo.WorldSettings.Password);
        _timeOfDay = subsystemTimeOfDay.CalculateTimeOfDay();
        _modServerAddress = SettingsManager.ModServerAddress;
        _season = subsystemSeasons.Season;
        _timeOfSeason = subsystemSeasons.TimeOfSeason;
    }


    public void Handle(NetNode? netNode, bool isServer)
    {
        if (_requestInfo)
        {
            if (From?.IPPoint != null)
            {
                netNode?.SendWriterFromPackage(new ServerInfoPackage(false), From.IPPoint);
            }
        }
        else
        {
            var p = ScreensManager.FindScreen<NetPlayScreen>("NetPlay", true)!;
            var c = new Connect
            {
                State = ConnectState.Avaliable,
                IP = From?.IPPoint?.ToString() ?? string.Empty
            };
            c.Name = c.IP;
            c.GameMode = _gameMode;
            c.HasPassword = _needPasswd;
            c.IsNeedLoginCommunity = _needLogin;
            c.MaxCount = _maxPlayerCount;
            c.PlayerCount = _clientCount;
            c.FromBroadcast = From?.IsLocalRemote ?? false;
            c.FromLocal = false;
            c.FromCommunity = false;
            c.UsedTime = Ping;
            c.Version = _version;
            c.TimeOfDay = _timeOfDay;
            c.ModServerAddress = _modServerAddress;
            c.Season = _season;
            c.TimeOfSeason = _timeOfSeason;
            if (IpToDNS.TryGetValue(c.IP, out var dns))
            {
                c.IP = dns;
            }

            if (DNSToName.TryGetValue(c.IP, out var name))
            {
                c.Name = name;
            }

            if (p.CheckConnectExists(c, out var found))
            {
                found!.State = c.State;
                found.IP = string.IsNullOrEmpty(dns) ? found.IP : c.IP;
                found.Name = string.IsNullOrEmpty(name) ? found.Name : c.Name;
                found.GameMode = c.GameMode;
                found.HasPassword = c.HasPassword;
                found.IsNeedLoginCommunity = c.IsNeedLoginCommunity;
                found.MaxCount = c.MaxCount;
                found.PlayerCount = c.PlayerCount;
                found.FromBroadcast = c.FromBroadcast;
                found.UsedTime = c.UsedTime;
                found.Version = c.Version;
                found.TimeOfDay = c.TimeOfDay;
                found.ModServerAddress = _modServerAddress;
                found.Season = c.Season;
                found.TimeOfSeason = c.TimeOfSeason;
            }
            else
            {
                if (From is not null && From.IsLocalRemote) //局域网
                {
                    if (p.CheckSaveConnectExists(c, out var f))
                    {
                        ModsManager.SaveConnects.Remove(f!);
                    }

                    ModsManager.SaveConnects.Add(c);
                }
            }

            p.UpdateList();
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _requestInfo = reader.ReadBoolean();
        if (_requestInfo)
        {
            return;
        }

        _version = reader.ReadString();
        _clientCount = reader.ReadUInt16();
        _maxPlayerCount = reader.ReadUInt16();
        _gameMode = reader.ReadEnum<GameMode>();
        _needLogin = reader.ReadBoolean();
        _needPasswd = reader.ReadBoolean();
        _timeOfDay = reader.ReadSingle();
        // 如果不考虑兼容03.04版本可以删掉try-catch语句
        try
        {
            _modServerAddress = reader.ReadString();
        }
        catch
        {
            _modServerAddress = string.Empty;
        }

        _season = (Season)reader.ReadInt32();
        _timeOfSeason = reader.ReadSingle();
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_requestInfo);
        if (_requestInfo)
        {
            return;
        }

        writer.Write(_version);
        writer.Write(_clientCount);
        writer.Write(_maxPlayerCount);
        writer.WriteEnum(_gameMode);
        writer.Write(_needLogin);
        writer.Write(_needPasswd);
        writer.Write(_timeOfDay);
        writer.Write(_modServerAddress);
        writer.Write((int)_season);
        writer.Write(_timeOfSeason);
    }
}

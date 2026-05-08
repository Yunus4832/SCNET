using EntitySystem.TemplatesDatabase;

namespace Game.NetWork.Packages;

public class PlayerDataPackage : IPackage
{
    public enum DataType
    {
        Create,
        Modify,
        Delete,
        ClientKnownPlayer,
        AddPlayer,
        SetUpdateLocation,
        CloseTime,
        AddNoMsg,
        RemoveNoMsg,
        Bugle,
        Count
    }

    private string _bugleContent = string.Empty; //小喇叭内容

    private string _bugleTitle = string.Empty; //小喇叭标题

    private PlayerClass _playerClass;

    private int _playerCount; //玩家人数

    private Guid _playerGuid;

    private string _playerName = string.Empty;

    private string _skinName = string.Empty;

    private DataType _type;

    private TerrainUpdater.UpdateLocation _updateLocation;

    private ValuesDictionary? _vd;

    private int _visibility;

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
        _vd = new ValuesDictionary();
        _type = dataType;
        _playerName = playerData.Name;
        _skinName = playerData.CharacterSkinName;
        _playerGuid = playerData.PlayerGUID;
        _playerClass = playerData.PlayerClass;
        playerData.Save(_vd);
    }

    public PlayerDataPackage(Guid guid, bool add)
    {
        _type = add ? DataType.AddNoMsg : DataType.RemoveNoMsg;
        _playerGuid = guid;
    }

    public PlayerDataPackage(int time, string msg)
    {
        _playerName = msg;
        _type = DataType.CloseTime;
        _visibility = time;
    }

    public PlayerDataPackage(TerrainUpdater.UpdateLocation updateLocation)
    {
        _updateLocation = updateLocation;
        _type = DataType.SetUpdateLocation;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_type);
        switch (_type)
        {
            case DataType.AddPlayer:
            case DataType.Create:
                if (_vd != null)
                {
                    var messagePack = _vd.ToMessagePack();
                    writer.WriteBuff(messagePack);
                }

                break;
            case DataType.Modify:
                writer.Write(_playerGuid);
                writer.Write(_playerName);
                writer.Write(_skinName);
                writer.WriteEnum(_playerClass);
                break;
            case DataType.ClientKnownPlayer:
                writer.Write(_playerGuid);
                break;
            case DataType.SetUpdateLocation:
                writer.Write(_updateLocation.Center);
                writer.Write((ushort)_updateLocation.ContentDistance);
                writer.Write((ushort)_updateLocation.VisibilityDistance);
                writer.Write(_updateLocation.LastChunksUpdateCenter);
                break;
            case DataType.CloseTime:
                writer.Write(_visibility);
                writer.Write(_playerName);
                break;
            case DataType.Bugle:
                writer.Write(_bugleTitle);
                writer.Write(_bugleContent);
                break;
            case DataType.Count:
                writer.Write(_playerCount);
                break;
            case DataType.AddNoMsg:
            case DataType.RemoveNoMsg:
                writer.Write(_playerGuid);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _type = reader.ReadEnum<DataType>();
        switch (_type)
        {
            case DataType.AddPlayer:
            case DataType.Create:
                var messagePack = reader.ReadBuff();
                _vd = new ValuesDictionary();
                _vd.ApplyOverridesUseMessagePack(messagePack);
                break;
            case DataType.Modify:
                _playerGuid = reader.ReadGuid();
                _playerName = reader.ReadString();
                _skinName = reader.ReadString();
                _playerClass = reader.ReadEnum<PlayerClass>();
                break;
            case DataType.ClientKnownPlayer:
                _playerGuid = reader.ReadGuid();
                break;
            case DataType.SetUpdateLocation:
                _updateLocation = new TerrainUpdater.UpdateLocation();
                _updateLocation.Center = reader.ReadVector2();
                _updateLocation.ContentDistance = reader.ReadUInt16();
                _updateLocation.VisibilityDistance = reader.ReadUInt16();
                _updateLocation.LastChunksUpdateCenter = reader.ReadVector2Nullable();
                break;
            case DataType.CloseTime:
                _visibility = reader.ReadInt32();
                _playerName = reader.ReadString();
                break;
            case DataType.Bugle:
                _bugleTitle = reader.ReadString();
                _bugleContent = reader.ReadString();
                break;
            case DataType.Count:
                _playerCount = reader.ReadInt32();
                break;
            case DataType.AddNoMsg:
            case DataType.RemoveNoMsg:
                _playerGuid = reader.ReadGuid();
                break;
        }
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        PlayerData? playerData;
        var subsystemPlayers = projectNet.FindSubsystem<SubsystemPlayers>(true)!;
        switch (_type)
        {
            case DataType.Create:
                playerData = new PlayerData(projectNet);
                if (_vd != null)
                {
                    playerData.Load(_vd);
                }

                subsystemPlayers.AddPlayerData(playerData);
                playerData.Name = PlayerData.CreateNewName(playerData.Name);
                //服务器广播给所有客户端，添加玩家
                netNode.QueuePackage(new PlayerDataPackage(playerData, DataType.AddPlayer));
                break;
            case DataType.Modify:
                var playerData2 = subsystemPlayers.FindPlayerData(p => p.PlayerGUID == _playerGuid);
                if (playerData2 != null)
                {
                    var client = From;
                    var name = PlayerData.SanitizeName(_playerName);
                    if (client != null && !string.IsNullOrEmpty(client.Nickname))
                    {
                        playerData2.Name = client.Nickname;
                    }
                    else if (!PlayerData.IsDuplicateName(name))
                    {
                        playerData2.Name = name;
                    }

                    playerData2.CharacterSkinName = _skinName;
                    playerData2.PlayerClass = _playerClass;
                    if (isServer)
                    {
                        netNode.QueuePackage(this);
                    }
                }

                break;
            case DataType.Delete:
                netNode.RemoveClient(From);
                break;
            case DataType.AddPlayer:
                //客户端接收到添加玩家广播
                playerData = new PlayerData(projectNet);
                if (_vd != null)
                {
                    playerData.Load(_vd);
                }

                subsystemPlayers.AddPlayerData(playerData);
                netNode.QueuePackage(new PlayerDataPackage(playerData, DataType.ClientKnownPlayer));
                break;
            case DataType.SetUpdateLocation:
                var player = subsystemPlayers.PlayersData.Find(x => x.Client == From);
                if (player != null)
                {
                    var updater = projectNet.FindSubsystem<SubsystemTerrain>(true)!.TerrainUpdater;
                    updater.SetLastChunksUpdateCenter(player.PlayerIndex, _updateLocation.LastChunksUpdateCenter);
                    updater.SetUpdateLocation(player.PlayerIndex, _updateLocation.Center,
                        _updateLocation.VisibilityDistance, _updateLocation.ContentDistance);
                }

                break;
            case DataType.CloseTime:
                var p3 = projectNet.FindSubsystem<SubsystemPlayers>(true)!.MainPlayer;
                if (p3 != null)
                {
                    p3.ComponentGui.CloseTime = _visibility;
                    DialogsManager.ShowDialog(null,
                        new MessageDialog("服务器关闭提醒", _playerName, LanguageControl.Yes, LanguageControl.No,
                            btn => { DialogsManager.HideAllDialogs(); }));
                }

                break;
            case DataType.Bugle:
                var mainPlayer = projectNet.FindSubsystem<SubsystemPlayers>(true)!.MainPlayer;
                if (mainPlayer != null)
                {
                    if (_playerGuid == Guid.Empty ||
                        (_playerGuid != Guid.Empty && mainPlayer.PlayerData.PlayerGUID == _playerGuid))
                    {
                        DialogsManager.HideAllDialogs();
                        mainPlayer.ComponentHealth.IsInvulnerable = true;
                        _bugleContent = _bugleContent.Replace("[n]", "\n").Replace("[e]", " ");
                        DialogsManager.ShowDialog(
                            null,
                            new MessageDialog(
                                _bugleTitle,
                                _bugleContent,
                                LanguageControl.Ok,
                                string.Empty,
                                _ =>
                                {
                                    DialogsManager.HideAllDialogs();
                                    mainPlayer.ComponentHealth.IsInvulnerable = false;
                                }
                            )
                        );
                    }
                }

                break;
            case DataType.Count:
                var mainPlayer2 = projectNet.FindSubsystem<SubsystemPlayers>(true)!.MainPlayer;
                if (mainPlayer2 != null)
                {
                    var clientPlayerCount = projectNet.FindSubsystem<SubsystemPlayers>(true)!.PlayersData.Count;
                    Log.Information($"隐身测试，服务端人数：{_playerCount}; 客户端人数：{clientPlayerCount}");
                    if (clientPlayerCount != _playerCount)
                    {
                        ScreensManager.SwitchScreen("NetPlay");
                        GameManager.DisposeProject();
                        CommonLib.Net.Stop();
                        DialogsManager.ShowDialog(
                            null,
                            new MessageDialog(
                                "连接异常",
                                "检测到玩家人数异常，请重新连接服务器",
                                LanguageControl.Ok
                            )
                        );
                    }
                }

                break;
            case DataType.AddNoMsg:
                projectNet.FindSubsystem<SubsystemPlayers>(true)!.NoMsgPlayerGuidList.Add(_playerGuid.ToString());
                break;
            case DataType.RemoveNoMsg:
                projectNet.FindSubsystem<SubsystemPlayers>(true)!.NoMsgPlayerGuidList.Remove(_playerGuid.ToString());
                break;
        }
    }
}

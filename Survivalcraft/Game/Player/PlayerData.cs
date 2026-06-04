using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.NetSimulate;
using Game.Network.Packages;

namespace Game;

public partial class PlayerData : IDisposable
{
    public enum SpawnMode
    {
        InitialIntro,
        InitialNoIntro,
        Respawn
    }

    public const string TypeName = "PlayerData";

    private static readonly Regex _validNameRegex = GenValidNameRegex();

    public List<Guid> BlackList = [];

    public Vector3? ClientCachePosition;

    public string GroupKey = string.Empty;

    public bool IsDead;

    public bool IsSetStatus;

    private PlayerClass _playerClass;

    private double? _playerDeathTime;

    private float _progress;

    private SpawnDialog? _spawnDialog;

    private SpawnMode _spawnMode;

    private readonly StateMachine _stateMachine = new();

    public readonly SubsystemGameInfo SubsystemGameInfo;

    private readonly SubsystemSky _subsystemSky;

    private readonly SubsystemTerrain _subsystemTerrain;

    private double _terrainWaitStartTime;

    private bool _gameWidgetInitialized;

    public Guid PlayerGUID { get; set; } = Guid.Empty;

    public bool ReadyToRestart;

    public bool ServerManager;

    public Project Project { get; }

    public Client? Client => CommonLib.Net.GetClientByGUID(PlayerGUID, false);

    public byte ClientId => Client?.ID ?? 0;

    public bool IsMainPlayer { get; private set; }

    public int PlayerIndex { get; set; }

    public SubsystemGameWidgets SubsystemGameWidgets { get; set; }

    public SubsystemPlayers SubsystemPlayers { get; set; }

    public ComponentPlayer? ComponentPlayer { get; set; }

    public GameWidget GameWidget
    {
        get => _gameWidgetInitialized ? field : throw new InvalidOperationException("GameWidget not initialized");
        set
        {
            _gameWidgetInitialized = true;
            field = value;
        }
    } = null!;

    public Vector3 SpawnPosition { get; set; }

    public double FirstSpawnTime { get; set; }

    public double LastSpawnTime { get; set; }

    public int SpawnsCount { get; set; }

    public string Name
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            IsDefaultName = false;
        }
    } = string.Empty;

    public bool IsDefaultName { get; set; }

    public PlayerClass PlayerClass
    {
        get => _playerClass;
        set
        {
            if (SubsystemPlayers.PlayersData.Contains(this))
            {
                throw new InvalidOperationException(LanguageControl.Get(TypeName, 1));
            }

            _playerClass = value;
        }
    }

    public float Level { get; set; }

    public string CharacterSkinName { get; set; } = string.Empty;

    public WidgetInputDevice InputDevice { get; set; }

    public bool IsReadyForPlaying
    {
        get
        {
            if (_stateMachine.CurrentState != "Playing")
            {
                return _stateMachine.CurrentState == "PlayerDead";
            }

            return true;
        }
    }

    public string CurrentState => _stateMachine.CurrentState;

    public void TransitionTo(string state) => _stateMachine.TransitionTo(state);

    /// <summary>
    /// 是否为服主
    /// </summary>
    public bool ServerMaster
    {
        get
        {
            if (CommonLib.WorkType == WorkType.Server &&
                CommonLib.Net.Clients.Count > 0)
            {
                return Client == CommonLib.Net.Clients.FirstOrDefault().Value;
            }

            return false;
        }
    }

    public PlayerData(Project project)
    {
        if (RunMode.Value is RunModeType.Gui)
        {
            PlayerGUID = CommonLib.Net.Self!.GUID;
        }

        Project = project;
        SubsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        SubsystemGameWidgets = project.FindSubsystem<SubsystemGameWidgets>(true)!;
        _subsystemTerrain = project.FindSubsystem<SubsystemTerrain>(true)!;
        SubsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSky = project.FindSubsystem<SubsystemSky>(true)!;
        _playerClass = PlayerClass.Male;
        Level = 1f;
        FirstSpawnTime = -1.0;
        LastSpawnTime = -1.0;
        RandomizeCharacterSkin();
        ResetName();
        InputDevice = WidgetInputDevice.None;
        _stateMachine.AddState(
            "FirstUpdate",
            Actions.Empty,
            delegate
            {
                //说明：没有在线的玩家让其离线
                if ((CommonLib.WorkType != WorkType.Local && CommonLib.Net.GetClientByGUID(PlayerGUID) == null) ||
                    (CommonLib.WorkType == WorkType.Local && !IsMainPlayer))
                {
                    _stateMachine.TransitionTo("MakePlayerOffline");
                }
                else if (ComponentPlayer != null)
                {
                    UpdateSpawnDialog(
                        string.Format(LanguageControl.Get(TypeName, 4), Name, MathUtils.Floor(Level)),
                        string.Empty,
                        0f,
                        true
                    );
                    _stateMachine.TransitionTo("WaitForTerrain");
                }
                else
                {
#if DEBUG
                    Log.Debug($"准备生成玩家 ID: {PlayerGUID}");
#endif
                    _stateMachine.TransitionTo("PrepareSpawn");
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "MakePlayerOffline",
            delegate { AsyncDispatcher.Dispatch(() => SubsystemPlayers.MakePlayerOffline(PlayerGUID, false)); },
            Actions.Empty,
            Actions.Empty
        );
        _stateMachine.AddState(
            "PrepareSpawn",
            delegate
            {
                if (SpawnPosition == Vector3.Zero)
                {
                    if (CommonLib.WorkType != WorkType.Local && SubsystemGameInfo.WorldSettings.RandomSpawnPosition)
                    {
                        SubsystemPlayers.GlobalSpawnPosition = Vector3.Zero;
                    }

                    if (SubsystemPlayers.GlobalSpawnPosition == Vector3.Zero)
                    {
                        var playerData =
                            SubsystemPlayers.PlayersData.FirstOrDefault(pd => pd.SpawnPosition != Vector3.Zero);
                        if (playerData != null && CommonLib.WorkType == WorkType.Local)
                        {
                            SpawnPosition = playerData.SpawnPosition;
                            _spawnMode = SpawnMode.InitialNoIntro;
                        }
                        else
                        {
                            SpawnPosition = _subsystemTerrain.TerrainContentsGenerator.FindCoarseSpawnPosition();
#if DEBUG
                            Log.Information($"生成出生点{SpawnPosition}");
#endif
                            _spawnMode = CommonLib.WorkType == WorkType.Server
                                ? SpawnMode.InitialNoIntro
                                : SpawnMode.InitialIntro;
                        }

                        SubsystemPlayers.GlobalSpawnPosition = SpawnPosition;
                    }
                    else
                    {
                        SpawnPosition = SubsystemPlayers.GlobalSpawnPosition;
                        _spawnMode = SpawnMode.InitialNoIntro;
                    }
                }
                else
                {
                    _spawnMode = SpawnMode.Respawn;
                }

                if (_spawnMode == SpawnMode.Respawn)
                {
                    UpdateSpawnDialog(
                        CommonLib.WorkType != WorkType.Client
                            ? string.Format(LanguageControl.Get(TypeName, 2), Name, MathUtils.Floor(Level))
                            : $"连接{Name}的世界中 (等级 {MathUtils.Floor(Level)})",
                        LanguageControl.Get(TypeName, 3), 0f, true);
                }
                else
                {
                    UpdateSpawnDialog(
                        string.Format(LanguageControl.Get(TypeName, 4), Name, MathUtils.Floor(Level)),
                        string.Empty,
                        0f,
                        true);
                }

                //说明，客户端非主玩家不执行updateLocation
                if (!(CommonLib.WorkType == WorkType.Client && !IsMainPlayer))
                {
                    var initialVisibility = MathUtils.Max(32f, MathUtils.Min(SettingsManager.VisibilityRange, 64f));
                    _subsystemTerrain.TerrainUpdater.SetUpdateLocation(PlayerIndex, SpawnPosition.XZ, initialVisibility,
                        64f);
                }

                _terrainWaitStartTime = Time.FrameStartTime;
            },
            delegate
            {
                if (!Time.PeriodicEvent(0.1, 0.0))
                {
                    return;
                }

                var initialVisibility = MathUtils.Max(32f, MathUtils.Min(SettingsManager.VisibilityRange, 64f));
                var updateProgress2 =
                    _subsystemTerrain.TerrainUpdater.GetUpdateProgress(PlayerIndex, initialVisibility, 64f);
                UpdateSpawnDialog(string.Empty, string.Empty, 0.5f * updateProgress2, false);
                if (updateProgress2 < 1f && Time.FrameStartTime - _terrainWaitStartTime < 15.0)
                {
                    return;
                }

                SpawnPosition = _spawnMode switch
                {
                    SpawnMode.InitialIntro => FindIntroSpawnPosition(SpawnPosition.XZ),
                    SpawnMode.InitialNoIntro => FindNoIntroSpawnPosition(SpawnPosition, false),
                    SpawnMode.Respawn => FindNoIntroSpawnPosition(SpawnPosition, true),
                    _ => throw new InvalidOperationException(LanguageControl.Get(TypeName, 5))
                };

                _stateMachine.TransitionTo("WaitForTerrain");
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "WaitForTerrain",
            delegate
            {
                _terrainWaitStartTime = Time.FrameStartTime;
                var center = ComponentPlayer != null ? ComponentPlayer.ComponentBody.Position.XZ : SpawnPosition.XZ;
                if (ClientCachePosition.HasValue)
                {
                    center = ClientCachePosition.Value.XZ;
                    ClientCachePosition = null;
                }

                //说明，客户端非主玩家不执行UpdateLocation
                if (!(CommonLib.WorkType == WorkType.Client && !IsMainPlayer))
                {
                    _subsystemTerrain.TerrainUpdater.SetUpdateLocation(PlayerIndex, center,
                        MathUtils.Min(SettingsManager.VisibilityRange, 32f), 0f);
                }
            },
            delegate
            {
                if (!Time.PeriodicEvent(0.1, 0.0))
                {
                    return;
                }

                var updateProgress = _subsystemTerrain.TerrainUpdater.GetUpdateProgress(PlayerIndex,
                    MathUtils.Min(SettingsManager.VisibilityRange, 64f), 0f);
                UpdateSpawnDialog(string.Empty, string.Empty, 0.5f + 0.5f * updateProgress, false);
                if ((!(updateProgress >= 1f) || !(Time.FrameStartTime - _terrainWaitStartTime > 2.0)) &&
                    !(Time.FrameStartTime - _terrainWaitStartTime >= 15.0))
                {
                    return;
                }

                if (ComponentPlayer == null || ComponentPlayer.PlayerData.PlayerGUID == Guid.Empty)
                {
                    if (CommonLib.WorkType != WorkType.Client)
                    {
                        SpawnPlayer(SpawnPosition, _spawnMode);
                    }
                    else
                    {
                        _stateMachine.TransitionTo("WaitForPlayerEntity");
                        return;
                    }
                }

                _stateMachine.TransitionTo("Playing");
            },
            Actions.Empty
        );
        //等待服务器返回玩家数据
        _stateMachine.AddState(
            "WaitForPlayerEntity",
            Actions.Empty,
            delegate
            {
                UpdateSpawnDialog("等待服务器响应玩家实体", string.Empty, 0f, false);
                if (!Time.PeriodicEvent(0.1, 0.0))
                {
                    return;
                }

                if (ComponentPlayer != null)
                {
                    _stateMachine.TransitionTo("Playing");
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "Playing",
            delegate
            {
                HideSpawnDialog();
                if (CommonLib.WorkType == WorkType.Client)
                {
                    CommonLib.Net.QueuePackage(new ClientPackage(CommonLib.Net.Self!.ID, ClientState.Playing));
                }
            },
            delegate
            {
                if (ComponentPlayer == null)
                {
                    _stateMachine.TransitionTo("PrepareSpawn");
                }
                else if (_playerDeathTime.HasValue)
                {
                    _stateMachine.TransitionTo("PlayerDead");
                }
                else if (ComponentPlayer.ComponentHealth.Health <= 0f)
                {
                    _playerDeathTime = Time.RealTime;
                }

                //服务器更新客户端玩家的区域
                if (IsMainPlayer || CommonLib.WorkType == WorkType.Client)
                {
                    return;
                }

                if (IsSetStatus)
                {
                    return;
                }

                if (Client != null && Client.State != ClientState.Playing)
                {
                    ComponentPlayer?.ComponentHealth.IsInvulnerable = true;
                }
                else
                {
                    IsSetStatus = true;
                    if (SubsystemGameInfo.WorldSettings.GameMode != GameMode.Creative)
                    {
                        ComponentPlayer?.ComponentHealth.IsInvulnerable = false;
                    }
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "PlayerDead",
            delegate
            {
                IsSetStatus = false;
                ModsManager.HookAction("OnPlayerDead", modLoader =>
                {
                    modLoader.OnPlayerDead(this);
                    return false;
                });
            },
            delegate
            {
                //死掉后也继续更新
                if (CommonLib.WorkType != WorkType.Client && !IsMainPlayer)
                {
                    _ = ComponentPlayer != null ? ComponentPlayer.ComponentBody.Position.XZ : SpawnPosition.XZ;
                }

                if (CommonLib.WorkType == WorkType.Client && !IsMainPlayer)
                {
                    NetRestart();
                    _stateMachine.TransitionTo("PrepareSpawn");
                }
                else if (ComponentPlayer == null)
                {
                    if (!IsDead || !ReadyToRestart)
                    {
                        return;
                    }

                    IsDead = false;
                    ReadyToRestart = false;
                    _stateMachine.TransitionTo("PrepareSpawn");
                }
                else if ((IsMainPlayer && Time.RealTime - _playerDeathTime!.Value > 1.5 &&
                          !DialogsManager.HasDialogs(ComponentPlayer.GuiWidget) &&
                          ComponentPlayer.GameWidget.Input.Any) ||
                         ReadyToRestart)
                {
                    if (CommonLib.WorkType == WorkType.Client && IsMainPlayer)
                    {
                        CommonLib.Net.QueuePackage(new ComponentPlayerPackage(this,
                            ComponentPlayerPackage.PlayerAction.Restart));
                    }

                    IsDead = true;
                    ReadyToRestart = true;
                    NetRestart();
                }
            },
            Actions.Empty
        );
    }


    public void Dispose()
    {
        HideSpawnDialog();
    }

    public void NetRestart()
    {
        if (ComponentPlayer is null)
        {
            return;
        }

        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Cruel)
        {
            //禁用更新，防止一直出现dialog
            _stateMachine.TransitionTo(string.Empty);
            DialogsManager.ShowDialog(
                ComponentPlayer.GuiWidget,
                new MessageDialog(
                    "提示", "你已在残酷模式死亡，不可复活！",
                    LanguageControl.Yes,
                    LanguageControl.No,
                    delegate { CommonLib.Net.StopImmediate(); }
                )
            );
        }
        else if (!SubsystemGameInfo.WorldSettings.IsAdventureRespawnAllowed)
        {
            //禁用更新，防止一直出现dialog
            _stateMachine.TransitionTo(string.Empty);
            DialogsManager.ShowDialog(
                ComponentPlayer.GuiWidget,
                new MessageDialog(
                    "提示", "服务器禁止了玩家重生！",
                    LanguageControl.Yes,
                    LanguageControl.No,
                    delegate { CommonLib.Net.StopImmediate(); }
                )
            );
        }
        else if (SubsystemGameInfo.WorldSettings is { GameMode: GameMode.Adventure, IsAdventureRespawnAllowed: false })
        {
            if (GameManager.WorldInfo != null)
            {
                ScreensManager.SwitchScreen("GameLoading", GameManager.WorldInfo, "AdventureRestart");
            }
        }
        else
        {
            Project.RemoveEntity(ComponentPlayer.Entity, true);
        }
    }

    public void ResetState()
    {
        ComponentPlayer = null!;
        _stateMachine.TransitionTo("FirstUpdate");
    }

    public void SetMain()
    {
        IsMainPlayer = true;
    }

    public void RandomizeCharacterSkin()
    {
        var random = new Random();
        CharacterSkinsManager.UpdateCharacterSkinsList();
        var array = CharacterSkinsManager.ReadOnlyCharacterSkinsNames.Where(n =>
            CharacterSkinsManager.IsBuiltIn(n) && CharacterSkinsManager.GetPlayerClass(n) == _playerClass).ToArray();
        var second = SubsystemPlayers.PlayersData.Select(pd => pd.CharacterSkinName).ToArray();
        var array2 = array.Except(second).ToArray();
        CharacterSkinName = array2.Length != 0
            ? array2[random.Int(0, array2.Length - 1)]
            : array[random.Int(0, array.Length - 1)];
    }

    public void ResetName()
    {
        Name = CharacterSkinsManager.GetDisplayName(CharacterSkinName);
        IsDefaultName = true;
    }

    public static bool VerifyName(string name)
    {
        return name.Length != 0;
    }

    public void Update()
    {
        _stateMachine.Update();
    }

    public void Load(ValuesDictionary valuesDictionary)
    {
        SpawnPosition = valuesDictionary.GetValue("SpawnPosition", Vector3.Zero);
        FirstSpawnTime = valuesDictionary.GetValue("FirstSpawnTime", 0.0);
        LastSpawnTime = valuesDictionary.GetValue("LastSpawnTime", 0.0);
        SpawnsCount = valuesDictionary.GetValue("SpawnsCount", 0);
        Name = valuesDictionary.GetValue("Name", "Walter");
        PlayerGUID = RunMode.Value is RunModeType.HeadlessServer
            ? valuesDictionary.GetValue("PlayerGUID", CommonLib.Net.Self?.GUID ?? Guid.Empty)
            : valuesDictionary.GetValue("PlayerGUID", CommonLib.Net.Self!.GUID);
        PlayerClass = valuesDictionary.GetValue("PlayerClass", PlayerClass.Male);
        Level = valuesDictionary.GetValue("Level", 1f);
        CharacterSkinName =
            valuesDictionary.GetValue("CharacterSkinName", CharacterSkinsManager.ReadOnlyCharacterSkinsNames[0]);
        InputDevice = valuesDictionary.GetValue("InputDevice", InputDevice);
        GroupKey = valuesDictionary.GetValue("GroupKey", GroupKey);
        ServerManager = valuesDictionary.GetValue("ServerManager", ServerManager);
        _stateMachine.TransitionTo("FirstUpdate");
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            IsMainPlayer = CommonLib.Net.Self is not null && PlayerGUID == CommonLib.Net.Self.GUID;
        }
        else
        {
            IsMainPlayer = PlayerGUID == CommonLib.Net.Self!.GUID;
        }

        if (IsMainPlayer && CommonLib.WorkType == WorkType.Server)
        {
            ServerManager = true;
        }

        var client = Client;
        if (client != null && !string.IsNullOrEmpty(client.Nickname))
        {
            Name = client.Nickname;
        }
    }

    public void Save(ValuesDictionary valuesDictionary)
    {
        valuesDictionary.SetValue("SpawnPosition", SpawnPosition);
        valuesDictionary.SetValue("FirstSpawnTime", FirstSpawnTime);
        valuesDictionary.SetValue("LastSpawnTime", LastSpawnTime);
        valuesDictionary.SetValue("SpawnsCount", SpawnsCount);
        valuesDictionary.SetValue("Name", Name);
        valuesDictionary.SetValue("PlayerGUID", PlayerGUID);
        valuesDictionary.SetValue("PlayerClass", PlayerClass);
        valuesDictionary.SetValue("Level", Level);
        valuesDictionary.SetValue("CharacterSkinName", CharacterSkinName);
        valuesDictionary.SetValue("InputDevice", InputDevice);
        valuesDictionary.SetValue("ServerManager", ServerManager);
        valuesDictionary.SetValue("GroupKey", GroupKey);
    }

    public void WaitEntityAdded()
    {
        _stateMachine.TransitionTo("WaitForPlayerEntity");
    }

    public bool IsInGroup(Guid playerGuid)
    {
        if (GroupKey == string.Empty)
        {
            return false;
        }

        return Project.FindSubsystem<SubsystemPlayers>(true)!.ServerGroups.TryGetValue(GroupKey, out var v) &&
               v.Members.Contains(playerGuid);
    }

    public void OnEntityAdded(Entity entity)
    {
        var componentPlayer = entity.FindComponent<ComponentPlayer>();
        if (componentPlayer == null || componentPlayer.PlayerData != this)
        {
            return;
        }

        ComponentPlayer = componentPlayer;
        if (RunMode.Value is RunModeType.Gui)
        {
            GameWidget.ActiveCamera = GameWidget.FindCamera<FppCamera>()!;
            GameWidget.Target = componentPlayer;
        }

        if (FirstSpawnTime < 0.0)
        {
            FirstSpawnTime = SubsystemGameInfo.TotalElapsedGameTime;
        }

        if (CommonLib.WorkType != WorkType.Client || !IsMainPlayer ||
            _stateMachine.CurrentState != "PrepareSpawn")
        {
            return;
        }

        // 如果客户端接收到实体，则立即进入WaitForTerrain，防止卡住
        ClientCachePosition = ComponentPlayer.ComponentBody.Position;
        _stateMachine.TransitionTo("WaitForTerrain");
    }

    public event Action? EntityRemoved;

    public void OnEntityRemoved(Entity entity)
    {
        if (ComponentPlayer == null || entity != ComponentPlayer.Entity)
        {
            return;
        }

        ComponentPlayer = null;
        _playerDeathTime = null;
        EntityRemoved?.Invoke();
    }

    public Vector3 FindIntroSpawnPosition(Vector2 desiredSpawnPosition)
    {
        var vector = Vector2.Zero;
        var num = float.MinValue;
        for (var i = -30; i <= 30; i += 2)
        for (var j = -30; j <= 30; j += 2)
        {
            var num2 = Terrain.ToCell(desiredSpawnPosition.X) + i;
            var num3 = Terrain.ToCell(desiredSpawnPosition.Y) + j;
            var num4 = ScoreIntroSpawnPosition(desiredSpawnPosition, num2, num3);
            if (num4 > num)
            {
                num = num4;
                vector = new Vector2(num2, num3);
            }
        }

        float num5 =
            _subsystemTerrain.Terrain.CalculateTopmostCellHeight(Terrain.ToCell(vector.X), Terrain.ToCell(vector.Y)) +
            1;
        return new Vector3(vector.X + 0.5f, num5 + 0.01f, vector.Y + 0.5f);
    }

    public Vector3 FindNoIntroSpawnPosition(Vector3 desiredSpawnPosition, bool respawn)
    {
        var vector = Vector3.Zero;
        var num = float.MinValue;
        for (var i = -8; i <= 8; i++)
        for (var j = -8; j <= 8; j++)
        for (var k = -8; k <= 8; k++)
        {
            var num2 = Terrain.ToCell(desiredSpawnPosition.X) + i;
            var num3 = Terrain.ToCell(desiredSpawnPosition.Y) + j;
            var num4 = Terrain.ToCell(desiredSpawnPosition.Z) + k;
            var num5 = ScoreNoIntroSpawnPosition(desiredSpawnPosition, num2, num3, num4);
            if (num5 > num)
            {
                num = num5;
                vector = new Vector3(num2, num3, num4);
            }
        }

        return new Vector3(vector.X + 0.5f, vector.Y + 0.01f, vector.Z + 0.5f);
    }

    public float ScoreIntroSpawnPosition(Vector2 desiredSpawnPosition, int x, int z)
    {
        var num = -0.01f * Vector2.Distance(new Vector2(x, z), desiredSpawnPosition);
        var num2 = _subsystemTerrain.Terrain.CalculateTopmostCellHeight(x, z);
        if (num2 < 64 || num2 > 85)
        {
            num -= 5f;
        }

        if (_subsystemTerrain.Terrain.GetSeasonalTemperature(x, z) < 8)
        {
            num -= 5f;
        }

        var cellContents = _subsystemTerrain.Terrain.GetCellContents(x, num2, z);
        if (BlocksManager.Blocks[cellContents].Transparent)
        {
            num -= 5f;
        }

        for (var i = x - 1; i <= x + 1; i++)
        for (var j = z - 1; j <= z + 1; j++)
        {
            if (_subsystemTerrain.Terrain.GetCellContents(i, num2 + 2, j) != 0)
            {
                num -= 1f;
            }
        }

        var vector = ComponentIntro.FindOceanDirection(_subsystemTerrain.TerrainContentsGenerator, new Vector2(x, z));
        var vector2 = new Vector3(x, num2 + 1.5f, z);
        for (var k = -1; k <= 1; k++)
        {
            var end = vector2 + new Vector3(30f * vector.X, 5f * k, 30f * vector.Y);
            var terrainRaycastResult = _subsystemTerrain.Raycast(vector2, end, false, true,
                (value, _) => Terrain.ExtractContents(value) != 0);
            if (!terrainRaycastResult.HasValue)
            {
                continue;
            }

            var cellFace = terrainRaycastResult.Value.CellFace;
            var cellContents2 = _subsystemTerrain.Terrain.GetCellContents(cellFace.X, cellFace.Y, cellFace.Z);
            if (cellContents2 != 18 && cellContents2 != 0)
            {
                num -= 2f;
            }
        }

        return num;
    }

    public float ScoreNoIntroSpawnPosition(Vector3 desiredSpawnPosition, int x, int y, int z)
    {
        var num = -0.01f * Vector3.Distance(new Vector3(x, y, z), desiredSpawnPosition);
        if (y < 1 || y >= 255)
        {
            num -= 100f;
        }

        var obj = BlocksManager.Blocks[_subsystemTerrain.Terrain.GetCellContents(x, y - 1, z)];
        var block = BlocksManager.Blocks[_subsystemTerrain.Terrain.GetCellContents(x, y, z)];
        var block2 = BlocksManager.Blocks[_subsystemTerrain.Terrain.GetCellContents(x, y + 1, z)];
        if (obj.Transparent)
        {
            num -= 10f;
        }

        if (!obj.Collidable)
        {
            num -= 10f;
        }

        if (block.Collidable)
        {
            num -= 10f;
        }

        if (block2.Collidable)
        {
            num -= 10f;
        }

        foreach (var playersDatum in SubsystemPlayers.PlayersData)
        {
            if (playersDatum != this && Vector3.DistanceSquared(playersDatum.SpawnPosition, new Vector3(x, y, z)) <
                MathUtils.Sqr(2))
            {
                num -= 1f;
            }
        }

        return num;
    }

    public bool CheckIsPointInWater(Point3 p)
    {
        var result = true;
        for (var i = p.X - 1; i < p.X + 1; i++)
        for (var j = p.Z - 1; j < p.Z + 1; j++)
        for (var num = p.Y; num > 0; num--)
        {
            var cellContents = _subsystemTerrain.Terrain.GetCellContents(p.X, num, p.Z);
            var block = BlocksManager.Blocks[cellContents];
            if (block.Collidable)
            {
                return false;
            }

            if (block is WaterBlock)
            {
                break;
            }
        }

        return result;
    }

    public void SpawnPlayer(Vector3 position, SpawnMode spawnMode)
    {
        ComponentMount? componentMount = null;
        if (spawnMode != SpawnMode.Respawn && CheckIsPointInWater(Terrain.ToCell(position)))
        {
            var entity = DatabaseManager.CreateEntity(Project, "Boat", true)!;
            entity.FindComponent<ComponentBody>(true)!.Position = position;
            entity.FindComponent<ComponentBody>(true)!.Rotation =
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathUtils.DegToRad(45f));
            componentMount = entity.FindComponent<ComponentMount>(true);
            Project.AddEntity(entity);
            position.Y += entity.FindComponent<ComponentBody>(true)!.BoxSize.Y;
        }

        var value = "";
        var value2 = "";
        var value3 = "";
        var value4 = "";
        var value5 = 0;
        var value6 = 0;
        var dirtCount = 0; // 泥土块的数量
        var stoneCount = 0; // 石头的数量

        if (spawnMode != SpawnMode.Respawn)
        {
            if (PlayerClass == PlayerClass.Female)
            {
                if (CharacterSkinsManager.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("2"))
                {
                    value = "";
                    value2 = MakeClothingValue(37, 2);
                    value3 = MakeClothingValue(16, 14);
                    value4 = MakeClothingValue(26, 6) + ";" + MakeClothingValue(27, 0);
                }
                else if (CharacterSkinsManager.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("3"))
                {
                    value = MakeClothingValue(31, 0);
                    value2 = MakeClothingValue(13, 7) + ";" + MakeClothingValue(5, 0);
                    value3 = MakeClothingValue(17, 15);
                    value4 = MakeClothingValue(29, 0);
                }
                else if (CharacterSkinsManager.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("4"))
                {
                    value = MakeClothingValue(30, 7);
                    value2 = MakeClothingValue(14, 6);
                    value3 = MakeClothingValue(25, 7);
                    value4 = MakeClothingValue(26, 6) + ";" + MakeClothingValue(8, 0);
                }
                else
                {
                    value = MakeClothingValue(30, 12);
                    value2 = MakeClothingValue(37, 3) + ";" + MakeClothingValue(1, 3);
                    value3 = MakeClothingValue(0, 12);
                    value4 = MakeClothingValue(26, 6) + ";" + MakeClothingValue(29, 0);
                }
            }
            else if (CharacterSkinsManager.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("2"))
            {
                value = "";
                value2 = MakeClothingValue(13, 0) + ";" + MakeClothingValue(5, 0);
                value3 = MakeClothingValue(25, 8);
                value4 = MakeClothingValue(26, 6) + ";" + MakeClothingValue(29, 0);
            }
            else if (CharacterSkinsManager.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("3"))
            {
                value = MakeClothingValue(32, 0);
                value2 = MakeClothingValue(37, 5);
                value3 = MakeClothingValue(0, 15);
                value4 = MakeClothingValue(26, 6) + ";" + MakeClothingValue(8, 0);
            }
            else if (CharacterSkinsManager.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("4"))
            {
                value = MakeClothingValue(31, 0);
                value2 = MakeClothingValue(15, 14);
                value3 = MakeClothingValue(0, 0);
                value4 = MakeClothingValue(26, 6) + ";" + MakeClothingValue(8, 0);
            }
            else
            {
                value = MakeClothingValue(32, 0);
                value2 = MakeClothingValue(37, 0) + ";" + MakeClothingValue(1, 9);
                value3 = MakeClothingValue(0, 12);
                value4 = MakeClothingValue(26, 6) + ";" + MakeClothingValue(29, 0);
            }

            value5 = SubsystemGameInfo.WorldSettings.GameMode <= GameMode.Survival ? 1 : 0;
        }

        var overrides = new ValuesDictionary
        {
            {
                "Player",
                new ValuesDictionary
                {
                    { "PlayerIndex", PlayerIndex },
                    { "PlayerGuid", PlayerGUID }
                }
            },
            {
                "Intro",
                new ValuesDictionary
                {
                    { "PlayIntro", spawnMode == SpawnMode.InitialIntro }
                }
            },
            {
                "Clothing",
                new ValuesDictionary
                {
                    {
                        "Clothes",
                        new ValuesDictionary
                        {
                            { "Feet", value4 },
                            { "Legs", value3 },
                            { "Torso", value2 },
                            { "Head", value }
                        }
                    }
                }
            },
            {
                "Inventory",
                new ValuesDictionary
                {
                    {
                        "Slots",
                        new ValuesDictionary
                        {
                            {
                                "Slot1",
                                new ValuesDictionary { { "Contents", 162 }, { "Count", value5 } } //鱼
                            },
                            {
                                "Slot2",
                                new ValuesDictionary { { "Contents", 114862 }, { "Count", value6 } } // 小麦种子
                            },
                            {
                                "Slot3",
                                new ValuesDictionary { { "Contents", 114861 }, { "Count", value6 } } // 南瓜
                            },
                            {
                                "Slot4",
                                new ValuesDictionary { { "Contents", 98477 }, { "Count", value6 } } // 棉花
                            },
                            {
                                "Slot5",
                                new ValuesDictionary { { "Contents", 119 }, { "Count", value6 } } // 橡树树苗
                            },
                            {
                                "Slot6",
                                new ValuesDictionary { { "Contents", 2 }, { "Count", dirtCount } } // 泥土块
                            },
                            {
                                "Slot12",
                                new ValuesDictionary { { "Contents", 5 }, { "Count", stoneCount } } // 石头
                            }
                        }
                    }
                }
            }
        };

        var v = ComponentIntro.FindOceanDirection(_subsystemTerrain.TerrainContentsGenerator, position.XZ);
        var entityTemplateName = PlayerClass == PlayerClass.Male ? "MalePlayer" : "FemalePlayer";
        var entity2 = DatabaseManager.CreateEntity(Project, entityTemplateName, overrides, true)!;
        var body = entity2.FindComponent<ComponentBody>(true)!;
        body.Position = position;
        body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, Vector2.Angle(v, -Vector2.UnitY));
        if (CommonLib.WorkType != WorkType.Local)
        {
            body.NetPosition = new NetPosition(body.Position);
            body.NetRotation = new NetRotation(body.Rotation);
        }

        Project.AddEntity(entity2);
        if (componentMount != null)
        {
            entity2.FindComponent<ComponentRider>(true)!.StartMounting(componentMount);
        }

        LastSpawnTime = SubsystemGameInfo.TotalElapsedGameTime;
        ModsManager.HookAction("OnPlayerSpawned",
            modLoader => modLoader.OnPlayerSpawned(spawnMode, entity2.FindComponent<ComponentPlayer>(true)!, position));
    }

    public string GetEntityTemplateName()
    {
        return PlayerClass == PlayerClass.Female ? "FemalePlayer" : "MalePlayer";
    }

    public void UpdateSpawnDialog(string largeMessage, string smallMessage, float progress, bool resetProgress)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        if (resetProgress)
        {
            _progress = 0f;
        }

        _progress = MathUtils.Max(progress, _progress);
        if (_spawnDialog == null)
        {
            _spawnDialog = new SpawnDialog();
            DialogsManager.ShowDialog(GameWidget.GuiWidget, _spawnDialog);
        }

        _spawnDialog.TimeOfYear = SubsystemGameInfo.WorldSettings.TimeOfYear;
        _spawnDialog.LargeMessage = largeMessage;
        _spawnDialog.SmallMessage = smallMessage;
        _spawnDialog.Progress = _progress;
    }

    public void HideSpawnDialog()
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        if (_spawnDialog == null)
        {
            return;
        }

        DialogsManager.HideDialog(_spawnDialog);
        _spawnDialog = null;
    }

    public static string MakeClothingValue(int index, int color)
    {
        return Terrain
            .MakeBlockValue(203, 0, ClothingBlock.SetClothingIndex(ClothingBlock.SetClothingColor(0, color), index))
            .ToString(CultureInfo.InvariantCulture);
    }

    public static string SanitizeName(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // 删除所有非法字符
        var sanitized = new StringBuilder();
        foreach (var c in input)
        {
            if (_validNameRegex.IsMatch(c.ToString()))
            {
                sanitized.Append(c);
            }
        }

        // 处理空值情况
        return sanitized.Length > 0
            ? sanitized.ToString()
            : throw new InvalidOperationException("Invalid input");
    }

    public static bool IsDuplicateName(string name)
    {
        if (GameManager.Project is null)
        {
            throw new InvalidOperationException("GameManager.Project is not initialized");
        }

        var subsystemPlayers = GameManager.Project.FindSubsystem<SubsystemPlayers>(true)!;
        return subsystemPlayers.PlayersData.Any(playerData => playerData.Name == name);
    }

    public static string CreateNewName(string originName)
    {
        if (string.IsNullOrEmpty(originName))
        {
            originName = string.Empty;
        }

        // 生成4位大写的随机16进制数 (0000~FFFF)
        var randomHex = new System.Random().Next(0, 65536).ToString("X4");

        return $"{originName.Trim()}_{randomHex}";
    }

    [GeneratedRegex(@"^[\u4E00-\u9FA5A-Za-z0-9_-]+$", RegexOptions.Compiled)]
    private static partial Regex GenValidNameRegex();
}

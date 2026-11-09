using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Interpolation;
using Game.Protocol.Identity;

namespace Game.Characters;

public partial class CharacterData : IDisposable
{
    public enum SpawnMode
    {
        InitialIntro = 0,

        InitialNoIntro = 1,

        Respawn = 2
    }

    public const string TypeName = nameof(CharacterData);

    private static readonly Regex _validNameRegex = GenValidNameRegex();

    public bool IsDead;

    public bool IsSetStatus;

    private CharacterSex _characterSex;

    private double? _playerDeathTime;

    private SpawnMode _spawnMode;

    /// <summary>Temporary authority preparation; it never replaces the approved peer interest.</summary>
    private TerrainSpawnPreparation? _spawnPreparation;

    private double? _spawnPreparationHandoffDeadline;

    private readonly StateMachine _stateMachine = new();

    public readonly SubsystemGameInfo SubsystemGameInfo;

    private readonly SubsystemSky _subsystemSky;

    private readonly SubsystemTerrain _subsystemTerrain;

    private double _terrainWaitStartTime;

    public Guid CharacterId { get; set; } = Guid.NewGuid();

    public PlayerAddress OwnerAddress { get; internal set; }

    public bool ReadyToRestart;

    public event Action<CharacterData>? Disposed;

    public Project Project { get; }

    public WorldConnection? ControllerConnection => WorldConnections.For(Project).FindCharacter(CharacterId);

    public byte ClientId => ControllerConnection?.ID ?? 0;

    public bool IsLocalCharacter { get; private set; }

    public int CharacterIndex { get; set; }

    public SubsystemCharacters SubsystemCharacters { get; set; }

    public ComponentCharacter? ComponentCharacter { get; set; }

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

    public CharacterSex CharacterSex
    {
        get => _characterSex;
        set
        {
            if (value == _characterSex)
            {
                return;
            }

            if (SubsystemCharacters.Characters.Contains(this))
            {
                throw new InvalidOperationException("Character sex cannot change after joining the roster.");
            }

            _characterSex = value;
        }
    }

    public float Level { get; set; }

    public string CharacterSkinName { get; set; } = string.Empty;

    public bool IsSimulationReady
    {
        get
        {
            if (!WorldRuntimeContext.For(Project).IsAuthority) { return ComponentCharacter != null; }

            if (_stateMachine.CurrentState != "Playing")
            {
                return _stateMachine.CurrentState == "PlayerDead";
            }

            return true;
        }
    }

    public string CurrentState => WorldRuntimeContext.For(Project).IsAuthority
        ? _stateMachine.CurrentState
        : ComponentCharacter is null
            ? "AwaitingEntity"
            : "Installed";

    public void TransitionTo(string state)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            throw new InvalidOperationException("Only the authority runs character spawn states.");
        }

        if (state is not ("PrepareSpawn" or "WaitForTerrain" or "Playing"))
        {
            _spawnPreparation?.Dispose();
            _spawnPreparation = null;
            _spawnPreparationHandoffDeadline = null;
        }

        _stateMachine.TransitionTo(state);
    }

    /// <summary>Creates an authority-owned character with its immutable player identity.</summary>
    public static CharacterData CreateAuthoritative(Project project, PlayerAddress owner)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!WorldRuntimeContext.IsProjectActive(project) || !WorldRuntimeContext.For(project).IsAuthority)
        {
            throw new InvalidOperationException("Character ownership is assigned only by an active authority world.");
        }

        if (owner == default) { throw new ArgumentException("A character owner is required.", nameof(owner)); }

        return new CharacterData(project) { OwnerAddress = owner };
    }

    public CharacterData(Project project)
    {
        Project = project;
        if (!WorldRuntimeContext.For(Project).IsAuthority &&
            WorldConnections.For(Project).Self?.PlayerAddress is { } owner)
        {
            OwnerAddress = owner;
        }

        SubsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
        _subsystemTerrain = project.FindSubsystem<SubsystemTerrain>(true)!;
        SubsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSky = project.FindSubsystem<SubsystemSky>(true)!;
        _characterSex = CharacterSex.Male;
        Level = 1f;
        FirstSpawnTime = -1.0;
        LastSpawnTime = -1.0;
        RandomizeCharacterSkin();
        ResetName(BuiltInCharacterSkins.GetDefaultName(CharacterSkinName));
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        _stateMachine.AddState(
            "FirstUpdate",
            Actions.Empty,
            delegate
            {
                //说明：没有在线的玩家让其离线
                if (ControllerConnection is null)
                {
                    _stateMachine.TransitionTo("MakeCharacterOffline");
                }
                else if (ComponentCharacter != null)
                {
                    _stateMachine.TransitionTo("WaitForTerrain");
                }
                else
                {
                    Log.Debug($"准备生成玩家 ID: {CharacterId}");
                    _stateMachine.TransitionTo("PrepareSpawn");
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "MakeCharacterOffline",
            delegate { SubsystemCharacters.QueueCharacterOffline(this); },
            Actions.Empty,
            Actions.Empty
        );
        _stateMachine.AddState(
            "PrepareSpawn",
            delegate
            {
                if (SpawnPosition == Vector3.Zero)
                {
                    if (SubsystemGameInfo.WorldSettings.RandomSpawnPosition)
                    {
                        SubsystemCharacters.GlobalSpawnPosition = Vector3.Zero;
                    }

                    if (SubsystemCharacters.GlobalSpawnPosition == Vector3.Zero)
                    {
                        var characterData =
                            SubsystemCharacters.Characters.FirstOrDefault(pd => pd.SpawnPosition != Vector3.Zero);
                        if (characterData != null && !SubsystemGameInfo.WorldSettings.RandomSpawnPosition)
                        {
                            SpawnPosition = characterData.SpawnPosition;
                            _spawnMode = SpawnMode.InitialNoIntro;
                        }
                        else
                        {
                            SpawnPosition = _subsystemTerrain.TerrainContentsGenerator.FindCoarseSpawnPosition();
                            Log.Debug($"生成出生点{SpawnPosition}");
                            _spawnMode = SpawnMode.InitialIntro;
                        }

                        SubsystemCharacters.GlobalSpawnPosition = SpawnPosition;
                    }
                    else
                    {
                        SpawnPosition = SubsystemCharacters.GlobalSpawnPosition;
                        _spawnMode = SpawnMode.InitialNoIntro;
                    }
                }
                else
                {
                    _spawnMode = SpawnMode.Respawn;
                }

                _spawnPreparation ??=
                    _subsystemTerrain.Demands.BeginSpawnPreparation(CharacterId, SpawnPosition.XZ, 64f);
                _spawnPreparation.Update(SpawnPosition.XZ, 64f);

                _terrainWaitStartTime = Time.FrameStartTime;
            },
            delegate
            {
                if (!Time.PeriodicEvent(0.1, 0.0))
                {
                    return;
                }

                var initialVisibility = 64f;
                var updateProgress2 =
                    _spawnPreparation!.GetProgress(initialVisibility, 64f);

                if (updateProgress2 < 1f && Time.FrameStartTime - _terrainWaitStartTime < 15.0)
                {
                    return;
                }

                SpawnPosition = _spawnMode switch
                {
                    SpawnMode.InitialIntro => FindIntroSpawnPosition(SpawnPosition.XZ),
                    SpawnMode.InitialNoIntro => FindNoIntroSpawnPosition(SpawnPosition, false),
                    SpawnMode.Respawn => FindNoIntroSpawnPosition(SpawnPosition, true),
                    _ => throw new InvalidOperationException("Unsupported character spawn mode.")
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
                var center = ComponentCharacter != null
                    ? ComponentCharacter.ComponentBody.Position.XZ
                    : SpawnPosition.XZ;

                _spawnPreparation ??= _subsystemTerrain.Demands.BeginSpawnPreparation(CharacterId, center, 64f);
                _spawnPreparation.Update(center, 64f);
            },
            delegate
            {
                if (!Time.PeriodicEvent(0.1, 0.0))
                {
                    return;
                }

                var updateProgress = _spawnPreparation!.GetProgress(32f, 0f);

                if ((!(updateProgress >= 1f) || !(Time.FrameStartTime - _terrainWaitStartTime > 2.0)) &&
                    !(Time.FrameStartTime - _terrainWaitStartTime >= 15.0))
                {
                    return;
                }

                if (ComponentCharacter == null || ComponentCharacter.CharacterData.CharacterId == Guid.Empty)
                {
                    SpawnPlayer(SpawnPosition, _spawnMode);
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
                if (!Time.PeriodicEvent(0.1, 0.0))
                {
                    return;
                }

                if (ComponentCharacter != null)
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
                InitializeSpawnMarks();
                TryReleaseSpawnPreparation(Time.FrameStartTime);
            },
            delegate
            {
                if (ComponentCharacter == null)
                {
                    _stateMachine.TransitionTo("PrepareSpawn");
                }
                else if (_playerDeathTime.HasValue)
                {
                    _stateMachine.TransitionTo("PlayerDead");
                }
                else if (ComponentCharacter.ComponentHealth.Health <= 0f)
                {
                    var dyingContext = new PlayerDyingContext(this, ComponentCharacter);
                    WorldRuleHooks.For(Project).Invoke(dyingContext);
                    if (!WorldRuntimeContext.IsProjectActive(dyingContext.Project)) { return; }

                    if (dyingContext.Cancel)
                    {
                        ComponentCharacter.ComponentHealth.Health = MathUtils.Clamp(dyingContext.Health, 0f, 1f);
                        if (dyingContext.CauseOfDeath != LocalizedText.Empty)
                        {
                            ComponentCharacter.ComponentHealth.CauseOfDeath = dyingContext.CauseOfDeath;
                        }
                    }
                    else
                    {
                        _playerDeathTime = Time.RealTime;
                    }
                }

                //服务器更新客户端玩家的区域
                if (IsLocalCharacter || !WorldRuntimeContext.For(Project).IsAuthority)
                {
                    return;
                }

                if (IsSetStatus)
                {
                    return;
                }

                if (ControllerConnection != null && ControllerConnection.State != ClientState.Playing)
                {
                    ComponentCharacter?.ComponentHealth.IsInvulnerable = true;
                }
                else
                {
                    IsSetStatus = true;
                    if (SubsystemGameInfo.WorldSettings.GameMode != GameMode.Creative)
                    {
                        ComponentCharacter?.ComponentHealth.IsInvulnerable = false;
                    }
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "PlayerDead",
            () =>
            {
                IsSetStatus = false;
                HandleDeath();
            },
            () =>
            {
                if (ComponentCharacter == null)
                {
                    if (!IsDead || !ReadyToRestart) { return; }

                    IsDead = false;
                    ReadyToRestart = false;
                    _stateMachine.TransitionTo("PrepareSpawn");
                }
                else if (ReadyToRestart)
                {
                    IsDead = true;
                    NetRestart();
                }
            },
            Actions.Empty
        );
    }

    public void Dispose()
    {
        Disposed?.Invoke(this);
        _spawnPreparation?.Dispose();
        _spawnPreparation = null;
        _spawnPreparationHandoffDeadline = null;
    }

    public void NetRestart()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            throw new InvalidOperationException("Only the authority can restart a character.");
        }

        if (ComponentCharacter is null) { return; }

        var requestKind = ResolveRespawnRequestKind();
        var context = new PlayerRespawnRequestedContext(this, ComponentCharacter, requestKind);
        WorldRuleHooks.For(Project).Invoke(context);
        if (!WorldRuntimeContext.IsProjectActive(Project) || context.Cancel)
        {
            ReadyToRestart = false;
            return;
        }

        if (requestKind != PlayerRespawnRequestKind.StandardRespawn)
        {
            ReadyToRestart = false;
            return;
        }

        Project.RemoveEntity(ComponentCharacter.Entity, true);
    }

    public void ResetState()
    {
        _spawnPreparation?.Dispose();
        _spawnPreparation = null;
        _spawnPreparationHandoffDeadline = null;
        ComponentCharacter = null!;
        if (WorldRuntimeContext.For(Project).IsAuthority) { _stateMachine.TransitionTo("FirstUpdate"); }
    }

    public void ResumePlaying(float health)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            throw new InvalidOperationException("Character simulation belongs to the authority.");
        }

        if (ComponentCharacter == null)
        {
            throw new InvalidOperationException("Player entity is not available.");
        }

        ComponentCharacter.ComponentHealth.Health = MathUtils.Clamp(health, 0f, 1f);
        _playerDeathTime = null;
        IsDead = false;
        ReadyToRestart = false;
        _stateMachine.TransitionTo("Playing");
    }

    public void SetMain()
    {
        var self = WorldConnections.For(Project).Self;
        if (self is null || self.PlayerAddress != OwnerAddress)
        {
            throw new InvalidOperationException("The local session does not own this character.");
        }

        self.SetControlledCharacter(CharacterId);
        IsLocalCharacter = true;
    }

    public void RandomizeCharacterSkin()
    {
        var random = new Random();
        var array = BuiltInCharacterSkins.Definitions.Where(definition => definition.Sex == _characterSex)
            .Select(definition => definition.Name).ToArray();
        var second = SubsystemCharacters.Characters.Select(pd => pd.CharacterSkinName).ToArray();
        var array2 = array.Except(second).ToArray();
        CharacterSkinName = array2.Length != 0
            ? array2[random.Int(0, array2.Length - 1)]
            : array[random.Int(0, array.Length - 1)];
    }

    public void ResetName(string defaultName)
    {
        Name = defaultName;
        IsDefaultName = true;
    }

    public static bool VerifyName(string name)
    {
        return name.Length != 0;
    }

    public void Update()
    {
        // 客户端的远端玩家仅是复制元数据，不能驱动本地出生/死亡状态机。
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        _stateMachine.Update();
        if (_stateMachine.CurrentState == "Playing") { TryReleaseSpawnPreparation(Time.FrameStartTime); }
    }

    internal void TryReleaseSpawnPreparation(double now)
    {
        if (!double.IsFinite(now)) { throw new ArgumentOutOfRangeException(nameof(now)); }

        var preparation = _spawnPreparation;
        var component = ComponentCharacter;
        if (preparation is null || component is null) { return; }

        var deadline = _spawnPreparationHandoffDeadline.GetValueOrDefault(now + 15.0);
        _spawnPreparationHandoffDeadline = deadline;
        // Entity installation precedes the first camera request in a new connection.
        // Retain the preparation only until a persistent peer interest covers the spawn's CPU neighborhood.
        if (_subsystemTerrain.Demands.IsLocationCovered(
                CharacterIndex,
                component.ComponentBody.Position.XZ,
                32f
            ))
        {
            preparation.Dispose();
            _spawnPreparation = null;
            _spawnPreparationHandoffDeadline = null;
        }
        else if (now >= deadline)
        {
            Log.Warning(
                $"Spawn preparation handoff timed out for character {CharacterId}; releasing temporary terrain interest."
            );
            preparation.Dispose();
            _spawnPreparation = null;
            _spawnPreparationHandoffDeadline = null;
        }
    }

    internal void InitializeSpawnMarks()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority || SpawnPosition == Vector3.Zero)
        {
            return;
        }

        if (WorldConnectionQuery.GetController(Project, CharacterId) is { } address)
        {
            Project.FindSubsystem<SubsystemPositionMarks>(true)!.GetPrivateMarks(address)
                .SetIfMissing("spawn", SpawnPosition);
        }

        // GlobalSpawnPosition is only a coarse anchor. Keep the first resolved spawn as an ordinary world mark.
        Project.FindSubsystem<SubsystemPositionMarks>(true)!.PublicMarks.SetIfMissing("spawn", SpawnPosition);
    }

    public void Load(ValuesDictionary valuesDictionary)
    {
        SpawnPosition = valuesDictionary.GetValue("SpawnPosition", Vector3.Zero);
        FirstSpawnTime = valuesDictionary.GetValue("FirstSpawnTime", 0.0);
        LastSpawnTime = valuesDictionary.GetValue("LastSpawnTime", 0.0);
        SpawnsCount = valuesDictionary.GetValue("SpawnsCount", 0);
        Name = valuesDictionary.GetValue("Name", "Walter");
        CharacterId = valuesDictionary.GetValue<Guid>("CharacterId");
        OwnerAddress = new PlayerAddress(valuesDictionary.GetValue<string>("OwnerAddress"));
        if (CharacterId == Guid.Empty ||
            WorldRuntimeContext.For(Project).IsAuthority &&
            Project.FindSubsystem<SubsystemPlayers>(true)!.Store.Find(OwnerAddress)?.CharacterId != CharacterId)
        {
            throw new InvalidDataException("Character ownership does not match the world subject record.");
        }

        WorldConnectionControl.BindInitialCharacter(Project, OwnerAddress, CharacterId);

        CharacterSex = valuesDictionary.GetValue("CharacterSex", CharacterSex.Male);
        Level = valuesDictionary.GetValue("Level", 1f);
        CharacterSkinName =
            valuesDictionary.GetValue("CharacterSkinName", BuiltInCharacterSkins.Definitions[0].Name);
        if (WorldRuntimeContext.For(Project).IsAuthority) { _stateMachine.TransitionTo("FirstUpdate"); }

        IsLocalCharacter = WorldConnectionQuery.IsLocalCharacter(Project, CharacterId);
    }

    public void ApplyReplicaState(ValuesDictionary valuesDictionary)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            throw new InvalidOperationException("Authority character state is not installed from replicas.");
        }

        if (valuesDictionary.GetValue<Guid>("CharacterId") != CharacterId ||
            valuesDictionary.GetValue<string>("OwnerAddress") != OwnerAddress.Value)
        {
            throw new InvalidDataException("Replicated character identity cannot change.");
        }

        Load(valuesDictionary);
    }

    public void Save(ValuesDictionary valuesDictionary)
    {
        valuesDictionary.SetValue("SpawnPosition", SpawnPosition);
        valuesDictionary.SetValue("FirstSpawnTime", FirstSpawnTime);
        valuesDictionary.SetValue("LastSpawnTime", LastSpawnTime);
        valuesDictionary.SetValue("SpawnsCount", SpawnsCount);
        valuesDictionary.SetValue("Name", Name);
        valuesDictionary.SetValue("CharacterId", CharacterId);
        valuesDictionary.SetValue("OwnerAddress", OwnerAddress.Value);
        valuesDictionary.SetValue("CharacterSex", CharacterSex);
        valuesDictionary.SetValue("Level", Level);
        valuesDictionary.SetValue("CharacterSkinName", CharacterSkinName);
    }

    public void WaitEntityAdded()
    {
        if (WorldRuntimeContext.For(Project).IsAuthority) { _stateMachine.TransitionTo("WaitForPlayerEntity"); }
    }

    public event Action<ComponentCharacter>? EntityAdded;

    public void OnEntityAdded(Entity entity)
    {
        var component = entity.FindComponent<ComponentCharacter>();
        if (component == null || component.CharacterData != this) { return; }

        ComponentCharacter = component;
        if (WorldRuntimeContext.For(Project).IsAuthority && FirstSpawnTime < 0.0)
        {
            FirstSpawnTime = SubsystemGameInfo.TotalElapsedGameTime;
        }

        WorldRuleHooks.For(Project).Invoke(new PlayerSpawnedContext(this, component, _spawnMode));
        if (WorldRuntimeContext.IsProjectActive(Project)) { EntityAdded?.Invoke(component); }
    }

    public event Action? EntityRemoved;

    public void OnEntityRemoved(Entity entity)
    {
        if (ComponentCharacter == null || entity != ComponentCharacter.Entity)
        {
            return;
        }

        ComponentCharacter = null;
        _playerDeathTime = null;
        EntityRemoved?.Invoke();
    }

    public Vector3 FindIntroSpawnPosition(Vector2 desiredSpawnPosition)
    {
        var vector = Vector2.Zero;
        var num = float.MinValue;
        for (var i = -30; i <= 30; i += 2)
        {
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
        {
            for (var j = -8; j <= 8; j++)
            {
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
        if (_subsystemTerrain.Catalog.Blocks[cellContents].Transparent)
        {
            num -= 5f;
        }

        for (var i = x - 1; i <= x + 1; i++)
        {
            for (var j = z - 1; j <= z + 1; j++)
            {
                if (_subsystemTerrain.Terrain.GetCellContents(i, num2 + 2, j) != 0)
                {
                    num -= 1f;
                }
            }
        }

        var vector = ComponentIntro.FindOceanDirection(_subsystemTerrain.Environment, new Vector2(x, z));
        var vector2 = new Vector3(x, num2 + 1.5f, z);
        for (var k = -1; k <= 1; k++)
        {
            var end = vector2 + new Vector3(30f * vector.X, 5f * k, 30f * vector.Y);
            var terrainRaycastResult = _subsystemTerrain.Raycast(
                vector2,
                end,
                false,
                true,
                (value, _) => Terrain.ExtractContents(value) != 0
            );
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

        var obj = _subsystemTerrain.Catalog
            .Blocks[_subsystemTerrain.Terrain.GetCellContents(x, y - 1, z)];
        var block = _subsystemTerrain.Catalog
            .Blocks[_subsystemTerrain.Terrain.GetCellContents(x, y, z)];
        var block2 = _subsystemTerrain.Catalog
            .Blocks[_subsystemTerrain.Terrain.GetCellContents(x, y + 1, z)];
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

        foreach (var playersDatum in SubsystemCharacters.Characters)
        {
            if (playersDatum != this &&
                Vector3.DistanceSquared(playersDatum.SpawnPosition, new Vector3(x, y, z)) <
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
        {
            for (var j = p.Z - 1; j < p.Z + 1; j++)
            {
                for (var num = p.Y; num > 0; num--)
                {
                    var cellContents = _subsystemTerrain.Terrain.GetCellContents(p.X, num, p.Z);
                    var block = _subsystemTerrain.Catalog.Blocks[cellContents];
                    if (block.Collidable)
                    {
                        return false;
                    }

                    if (block is WaterBlock)
                    {
                        break;
                    }
                }
            }
        }

        return result;
    }

    public void SpawnPlayer(Vector3 position, SpawnMode spawnMode)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            throw new InvalidOperationException("Character simulation belongs to the authority.");
        }

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
            if (CharacterSex == CharacterSex.Female)
            {
                if (BuiltInCharacterSkins.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("2"))
                {
                    value = "";
                    value2 = MakeClothingValue(37, 2);
                    value3 = MakeClothingValue(16, 14);
                    value4 = MakeClothingValue(26, 6) + ";" + MakeClothingValue(27, 0);
                }
                else if (BuiltInCharacterSkins.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("3"))
                {
                    value = MakeClothingValue(31, 0);
                    value2 = MakeClothingValue(13, 7) + ";" + MakeClothingValue(5, 0);
                    value3 = MakeClothingValue(17, 15);
                    value4 = MakeClothingValue(29, 0);
                }
                else if (BuiltInCharacterSkins.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("4"))
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
            else if (BuiltInCharacterSkins.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("2"))
            {
                value = "";
                value2 = MakeClothingValue(13, 0) + ";" + MakeClothingValue(5, 0);
                value3 = MakeClothingValue(25, 8);
                value4 = MakeClothingValue(26, 6) + ";" + MakeClothingValue(29, 0);
            }
            else if (BuiltInCharacterSkins.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("3"))
            {
                value = MakeClothingValue(32, 0);
                value2 = MakeClothingValue(37, 5);
                value3 = MakeClothingValue(0, 15);
                value4 = MakeClothingValue(26, 6) + ";" + MakeClothingValue(8, 0);
            }
            else if (BuiltInCharacterSkins.IsBuiltIn(CharacterSkinName) && CharacterSkinName.Contains("4"))
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
                "Character",
                new ValuesDictionary
                {
                    { "CharacterId", CharacterId }
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

        var v = ComponentIntro.FindOceanDirection(_subsystemTerrain.Environment, position.XZ);
        var entityTemplateName = CharacterSex == CharacterSex.Male ? "MaleCharacter" : "FemaleCharacter";
        var entity2 = DatabaseManager.CreateEntity(Project, entityTemplateName, overrides, true)!;
        var body = entity2.FindComponent<ComponentBody>(true)!;
        body.Position = position;
        body.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, Vector2.Angle(v, -Vector2.UnitY));
        body.NetPosition = new NetPosition(body.Position);
        body.NetRotation = new NetRotation(body.Rotation);

        LastSpawnTime = SubsystemGameInfo.TotalElapsedGameTime;
        Project.AddEntity(entity2);
        if (componentMount != null)
        {
            entity2.FindComponent<ComponentRider>(true)!.StartMounting(componentMount);
        }
    }

    public string GetEntityTemplateName()
    {
        return CharacterSex == CharacterSex.Female ? "FemaleCharacter" : "MaleCharacter";
    }

    private void HandleDeath()
    {
        if (ComponentCharacter != null)
        {
            var cause = ComponentCharacter.ComponentHealth.CauseOfDeath;
            if (cause == LocalizedText.Empty) { cause = new LocalizedText(TypeName, "12", "died"); }

            Project.FindSubsystem<SubsystemMessages>(true)!.Service.Publish(
                GameMessage.System(
                    [new MessageSegment($"{Name} "), new MessageSegment(cause, MessageTextStyle.Error)],
                    GameMessageTone.Warning,
                    GameMessagePresentation.Default | GameMessagePresentation.Toast
                )
            );
        }

        Level = MathUtils.Max(MathUtils.Floor(Level / 2f), 1f);
        NetworkSender.SendGlobal(Project, CharacterPackageBuilder.Capture(this, 0, false, Level));
    }

    private PlayerRespawnRequestKind ResolveRespawnRequestKind()
    {
        if (SubsystemGameInfo.WorldSettings.GameMode == GameMode.Cruel)
        {
            return PlayerRespawnRequestKind.CruelGameOver;
        }

        if (!SubsystemGameInfo.WorldSettings.IsAdventureRespawnAllowed)
        {
            return PlayerRespawnRequestKind.RespawnForbidden;
        }

        if (SubsystemGameInfo.WorldSettings is
            { GameMode: GameMode.Adventure, IsAdventureRespawnAllowed: false })
        {
            return PlayerRespawnRequestKind.AdventureRestart;
        }

        return PlayerRespawnRequestKind.StandardRespawn;
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

    [GeneratedRegex(@"^[\u4E00-\u9FA5A-Za-z0-9_-]+$", RegexOptions.Compiled)]
    private static partial Regex GenValidNameRegex();
}

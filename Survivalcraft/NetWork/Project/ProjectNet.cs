using Engine.Serialization;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork.Packages;

namespace Game.NetWork;

public class ProjectNet : Project
{
    public const string ServerVersion = "0.0.0.1";

    public static bool IsReleaseVersion = true;

    public static SubsystemPlayers SubsystemPlayers = null!;

    public static SubsystemTerrain SubsystemTerrain = null!;

    public static SubsystemPlantBlockBehavior SubsystemPlantBlockBehavior = null!;

    public static SubsystemGrassBlockBehavior SubsystemGrassBlockBehavior = null!;

    public static SubsystemPickables SubsystemPickables = null!;

    public static SubsystemTimeOfDay SubsystemTimeOfDay = null!;

    public static SubsystemBlockEntities SubsystemBlockEntities = null!;

    public static SubsystemSky SubsystemSky = null!;

    public static SubsystemAudio SubsystemAudio = null!;

    public static SubsystemCreatureSpawn SubsystemCreatureSpawn = null!;

    public static SubsystemSpawn SubsystemSpawn = null!;

    public static SubsystemGameInfo SubsystemGameInfo = null!;

    public static SubsystemBodies SubsystemBodies = null!;

    public static SubsystemTime SubsystemTime = null!;

    public static SubsystemElectricity SubsystemElectricity = null!;

    public static SubsystemBlockBehaviors SubsystemBlockBehaviors = null!;

    public static SubsystemParticles SubsystemParticles = null!;

    public static SubsystemAnimatedTextures SubsystemAnimatedTextures = null!;

    public static SubsystemExplosions SubsystemExplosions = null!;

    public static SubsystemProjectiles SubsystemProjectiles = null!;

    public static SubsystemInventories Inventories = null!;

    public static SubsystemSeasons SubsystemSeasons = null!;

    public static ProjectNet Project = null!;

    private readonly Dictionary<string, Subsystem> _dictionary = new();

    private readonly Dictionary<int, Entity> _entityMaps = new();

    public bool SendToClientMode = false;

    public ProjectNet(
        GameDatabase gameDatabase,
        ProjectData projectData
    )
    {
        try
        {
            _dictionary.Clear();
            GameDatabase = gameDatabase;
            ProjectTemplate = projectData.ValuesDictionary.DatabaseObject;
            foreach (var item in from x in projectData.ValuesDictionary.Values
                     select x as ValuesDictionary
                     into x
                     where x is { DatabaseObject: not null } &&
                           x.DatabaseObject.Type == gameDatabase.MemberSubsystemTemplateType
                     select x)
            {
                var value = item.GetValue<bool>("IsOptional");
                var value2 = item.GetValue<string>("Class");
                var type = TypeCache.FindType(value2, false, !value);
                if (type == null)
                {
                    continue;
                }

                object obj;
                try
                {
                    obj = Activator.CreateInstance(type)!;
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException ?? ex;
                }

                if (obj is not Subsystem subsystem)
                {
                    throw new InvalidOperationException(
                        $"Type \"{value2}\" cannot be used as a subsystem because it does not inherit from Subsystem class.");
                }

                subsystem.Initialize(this, item);
                _dictionary.Add(item.DatabaseObject.Name, subsystem);
                subsystems.Add(subsystem);
            }

            SubsystemPlayers = FindSubsystem<SubsystemPlayers>(true)!;
            SubsystemTerrain = FindSubsystem<SubsystemTerrain>(true)!;
            SubsystemPlantBlockBehavior = FindSubsystem<SubsystemPlantBlockBehavior>(true)!;
            SubsystemGrassBlockBehavior = FindSubsystem<SubsystemGrassBlockBehavior>(true)!;
            SubsystemPickables = FindSubsystem<SubsystemPickables>(true)!;
            SubsystemTimeOfDay = FindSubsystem<SubsystemTimeOfDay>(true)!;
            SubsystemSky = FindSubsystem<SubsystemSky>(true)!;
            SubsystemAudio = FindSubsystem<SubsystemAudio>(true)!;
            SubsystemCreatureSpawn = FindSubsystem<SubsystemCreatureSpawn>(true)!;
            SubsystemSpawn = FindSubsystem<SubsystemSpawn>(true)!;
            SubsystemGameInfo = FindSubsystem<SubsystemGameInfo>(true)!;
            SubsystemBodies = FindSubsystem<SubsystemBodies>(true)!;
            SubsystemTime = FindSubsystem<SubsystemTime>(true)!;
            SubsystemBlockEntities = FindSubsystem<SubsystemBlockEntities>(true)!;
            SubsystemElectricity = FindSubsystem<SubsystemElectricity>(true)!;
            SubsystemBlockBehaviors = FindSubsystem<SubsystemBlockBehaviors>(true)!;
            SubsystemParticles = FindSubsystem<SubsystemParticles>(true)!;
            SubsystemExplosions = FindSubsystem<SubsystemExplosions>(true)!;
            SubsystemAnimatedTextures = FindSubsystem<SubsystemAnimatedTextures>(true)!;
            SubsystemProjectiles = FindSubsystem<SubsystemProjectiles>(true)!;
            Inventories = FindSubsystem<SubsystemInventories>(true)!;
            SubsystemSeasons = FindSubsystem<SubsystemSeasons>(true)!;
            if (CommonLib.WorkType == WorkType.Client)
            {
                BeforeEntityAdded += (_, arg) => { _entityMaps[arg.Entity.EntityId] = arg.Entity; };
                EntityRemoved += (_, arg) => { _entityMaps.Remove(arg.Entity.EntityId); };
                CommonLib.Net.OnClientStateChanged += c =>
                {
                    switch (c.State)
                    {
                        case ClientState.NotConnected:
                        {
                            var playerData = SubsystemPlayers.PlayersData.Find(pd => pd.PlayerGUID == c.GUID);
                            if (playerData is { IsMainPlayer: false })
                            {
                                SubsystemPlayers.MakePlayerOffline(playerData.PlayerGUID);
                            }

                            break;
                        }
                    }
                };
                SettingsManager.VisibilityRange = Math.Min(256, SettingsManager.VisibilityRange);
            }
            else
            {
                //非客户端生成EntityID序号
                BeforeEntityAdded += (_, arg) =>
                {
                    if (arg.Entity.EntityId == 0 || _entityMaps.ContainsKey(arg.Entity.EntityId))
                    {
                        GenerateEntityId(arg.Entity);
                    }
                    else
                    {
                        _entityMaps.Add(arg.Entity.EntityId, arg.Entity);
                    }
                };
                EntityRemoved += (_, arg) => { _entityMaps.Remove(arg.Entity.EntityId); };
                if (CommonLib.WorkType == WorkType.Server)
                {
                    EntityAdded += (_, arg) => { CommonLib.Net.QueuePackage(new EntityPackage(arg.Entity)); };
                    EntityRemoved += (_, arg) =>
                    {
                        CommonLib.Net.QueuePackage(new EntityPackage(arg.Entity.EntityId));
                    };
                    CommonLib.Net.OnClientStateChanged += GrantClient;
                    CommonLib.Net.OnClientStateChanged += OnClientStateChanged;
                }
            }

            Project = this;
            Load(_dictionary, projectData);

            if (CommonLib.WorkType == WorkType.Client)
            {
                CommonLib.Net.QueuePackage(new ClientPackage(CommonLib.Net.Self!.ID, ClientState.ProjectLoaded));
            }

            //开启包处理
            CommonLib.Net.TurnOnPackageHanlde(this);
        }
        catch
        {
            try
            {
                Dispose();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                if (CommonLib.WorkType == WorkType.Server)
                {
                    CommonLib.Net.Stop();
                }
            }

            throw;
        }
    }

    public override void AddEntity(Entity entity)
    {
#if DEBUG
        if (CommonLib.WorkType == WorkType.Client)
        {
            Log.Information($"添加实体{entity.EntityId}");
        }
#endif
        base.AddEntity(entity);
    }

    public override void RemoveEntity(Entity entity, bool disposeEntity)
    {
#if DEBUG
        if (CommonLib.WorkType == WorkType.Client)
        {
            Log.Information($"移出实体{entity.EntityId}");
        }
#endif
        base.RemoveEntity(entity, disposeEntity);
    }

    public override void GenerateEntityId(Entity entity)
    {
        for (ushort i = 1; i < ushort.MaxValue; i++)
        {
            if (!_entityMaps.ContainsKey(i))
            {
                entity.EntityId = i;
                _entityMaps.Add(i, entity);
                return;
            }
        }
    }

    public override bool FindEntityById(ushort id, Action<Entity>? action = null)
    {
        if (_entityMaps.TryGetValue(id, out var entity))
        {
            action?.Invoke(entity);
            return true;
        }
#if DEBUG
        else
        {
            Log.Information($"没有找到对应实体{id}");
        }
#endif
        return false;
    }

    /// <summary>
    /// 客户端连接状态变更
    /// </summary>
    /// <param name="c"></param>
    private void OnClientStateChanged(Client c)
    {
#if DEBUG
        Log.Information($"Client {c.ID} State is {c.State}");

#endif
        c.SetProject(this);
        if (CommonLib.WorkType == WorkType.Server)
        {
            CommonLib.Net.QueuePackage(new ClientPackage(c.ID, c.State) { Except = c });
            switch (c.State)
            {
                case ClientState.Connected:
                {
                    byte[]? textureData = null;
                    if (SubsystemPlayers.MakePlayerOnline(c.GUID, out var playerData, out var entity))
                    {
                        c.CachePlayerEntity = entity!;
                        CommonLib.Net.QueuePackage(
                            new PlayerDataPackage(playerData!, PlayerDataPackage.DataType.AddPlayer) { Except = c });
                    }

                    if (!string.IsNullOrEmpty(SubsystemGameInfo.WorldSettings.BlocksTextureName))
                    {
                        using var s = Storage.OpenFile(
                            BlocksTexturesManager.GetFileName(SubsystemGameInfo.WorldSettings.BlocksTextureName),
                            OpenFileMode.Read);
                        textureData = ModsManager.StreamToBytes(s);
                    }

                    var data = CommonLib.GetNowProject(this);
                    if (data == null)
                    {
                        Log.Error($"Failed to get project data for client {c.ID}, disconnecting...");
                        c.Peer?.Disconnect();
                        break;
                    }
                    CommonLib.Net.QueuePackage(new ProjectPackage(textureData, data) { To = c });
                    break;
                }
                case ClientState.NotConnected:
                {
                    SubsystemPlayers.MakePlayerOffline(c.GUID);
                    GC.Collect();
                    break;
                }
                case ClientState.ProjectLoaded:
                {
                    var sendList = entityDictionary.Keys.ToList();
                    //向客户端广播实体列表
                    CommonLib.Net.QueuePackage(new EntityPackage(sendList) { To = c });
                    //如果有实体，添加到Project中并广播给其它客户端
                    if (c.CachePlayerEntity != null)
                    {
                        AddEntity(c.CachePlayerEntity);
                        c.CachePlayerEntity = null;
                    }

                    break;
                }
                case ClientState.LoadTerrain:
                    GC.Collect();
                    //进入无敌状态
                    if (c.PlayerData is { ComponentPlayer: not null })
                    {
                        if (FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode != GameMode.Creative)
                        {
                            c.PlayerData.ComponentPlayer.ComponentHealth.IsInvulnerable = true;
                        }
                    }

                    break;
                case ClientState.Playing:
                    if (c.PlayerData.ComponentPlayer != null)
                    {
                        if (FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode != GameMode.Creative)
                        {
                            c.PlayerData.ComponentPlayer.ComponentHealth.IsInvulnerable = false;
                        }
                    }

                    break;
            }
        }
        else
        {
            switch (c.State)
            {
                case ClientState.NotConnected:
                    SubsystemPlayers.MakePlayerOffline(c.GUID);
                    GC.Collect();
                    break;
            }
        }
    }

    /// <summary>
    /// 验证客户端
    /// </summary>
    /// <param name="client"></param>
    private void GrantClient(Client client)
    {
        if (client.State != ClientState.Playing)
        {
            return;
        }

        if (SubsystemPlayers.BlackPlayerGuidList.ContainsKey(client.GUID.ToString()))
        {
            CommonLib.Net.RemoveClientImmediate(client, "你被禁止加入该服务器");
        }
    }

    public FurnitureInventoryPanel? FindFurnitureInventorPanel(Client client)
    {
        var player = SubsystemPlayers.PlayersData.Find(x => x.Client == client);
        if (player is not { ComponentPlayer: not null })
        {
            return null;
        }

        var creativeWidget = new CreativeInventoryWidget(player.ComponentPlayer.Entity);
        return creativeWidget.FurnitureInventoryPanel;

    }

    public void PlayerAction(byte cid, Action<ComponentPlayer> action)
    {
        var guid = CommonLib.Net.GetClientByID(cid)?.GUID;
        if (guid != null)
        {
            PlayerAction(guid, action);
        }
    }

    private void PlayerAction(Guid? guid, Action<ComponentPlayer> action)
    {
        var pl = SubsystemPlayers.ComponentPlayers.FirstOrDefault(pll => pll.PlayerData.PlayerGUID == guid);
        if (pl != null)
        {
            action(pl);
        }
    }

    public override List<Entity> LoadEntities(EntityDataList entityDataList)
    {
        var list = new List<Entity>();
        var dictionary = new Dictionary<int, Entity>();
        var idToEntityMap = new IdToEntityMap(dictionary);
        var tmpList = new List<EntityData>();
        var toRemovePlayer = new List<EntityData>();
        foreach (var entitiesDatum in entityDataList.EntitiesData)
        {
            try
            {
                if (entitiesDatum.ValuesDictionary.ContainsKey("Player"))
                {
                    var playerValue = entitiesDatum.ValuesDictionary.GetValue<ValuesDictionary>("Player");
                    if (playerValue.ContainsKey("PlayerGuid"))
                    {
                        var playerGuid = playerValue.GetValue<Guid>("PlayerGuid");
                        var playerData = SubsystemPlayers.PlayersData.Find(d => d.PlayerGUID == playerGuid);
                        if (playerData == null)
                        {
                            toRemovePlayer.Add(entitiesDatum);
                            continue;
                        }
                    }
                }

                var entity = new Entity(this, entitiesDatum.ValuesDictionary);
                list.Add(entity);
                if (entitiesDatum.Id != 0)
                {
                    entity.EntityId = (ushort)entitiesDatum.Id;
                    dictionary.Add(entitiesDatum.Id, entity);
                    tmpList.Add(entitiesDatum);
                }
            }
            catch (Exception innerException)
            {
                throw new Exception(
                    $"Error creating entity from template \"{entitiesDatum.ValuesDictionary.DatabaseObject.Name}\".",
                    innerException);
            }
        }

        foreach (var rp in toRemovePlayer)
        {
            entityDataList.EntitiesData.Remove(rp);
        }

        var entitiesToRemove = new List<Entity>();
        var num = 0;
        foreach (var entitiesDatum2 in tmpList)
        {
            try
            {
                list[num].PublicLoadEntity(entitiesDatum2.ValuesDictionary, idToEntityMap);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load entity {entitiesDatum2.Id}, will skip it. Error: {ex.Message}");
                entitiesToRemove.Add(list[num]);
            }
            num++;
        }

        foreach (var entity in entitiesToRemove)
        {
            list.Remove(entity);
            dictionary.Remove(entity.EntityId);
        }

        return list;
    }

    public override EntityDataList SaveEntities(IEnumerable<Entity> entities)
    {
        var dictionary = DetermineNotOwnedEntities(entities);
        var dictionary2 = new Dictionary<Entity, int>();
        var entityToIdMap = new EntityToIdMap(dictionary2);
        foreach (var key in dictionary.Keys)
        {
            dictionary2.Add(key, key.EntityId);
        }

        var entityDataList = new EntityDataList
        {
            EntitiesData = new List<EntityData>(dictionary.Keys.Count)
        };
        foreach (var key2 in dictionary.Keys)
        {
            var entityData = new EntityData
            {
                Id = entityToIdMap.FindId(key2),
                ValuesDictionary = new ValuesDictionary
                {
                    DatabaseObject = key2.ValuesDictionary.DatabaseObject
                }
            };
            key2.InternalSaveEntity(entityData.ValuesDictionary, entityToIdMap);
            entityDataList.EntitiesData.Add(entityData);
        }

        return entityDataList;
    }

    /// <summary>
    /// Entity包的加载方法
    /// </summary>
    /// <param name="entityDataList"></param>
    /// <returns></returns>
    public List<Entity> LoadEntitiesAll(EntityDataList entityDataList)
    {
        //List<Entity> list = new List<Entity>(entityDataList.EntitiesData.Count);
        var list = new List<Entity>();
        var dictionary = new Dictionary<int, Entity>();
        var idToEntityMap = new IdToEntityMap(dictionary);
        var tmpList = new List<EntityData>();
        var toRemovePlayer = new List<EntityData>();
        foreach (var entitiesDatum in entityDataList.EntitiesData)
        {
            try
            {
                if (entitiesDatum.ValuesDictionary.ContainsKey("Player"))
                {
                    var palyerValue = entitiesDatum.ValuesDictionary.GetValue<ValuesDictionary>("Player");
                    if (palyerValue.ContainsKey("PlayerGuid"))
                    {
                        var playerGuid = palyerValue.GetValue("PlayerGuid", CommonLib.Net.Self!.GUID);
                        var playerData = SubsystemPlayers.PlayersData.Find(d => d.PlayerGUID == playerGuid);
                        if (playerData == null)
                        {
                            toRemovePlayer.Add(entitiesDatum);
                            continue;
                        }
                    }
                }

                var entity = new Entity(this, entitiesDatum.ValuesDictionary);
                list.Add(entity);
                entity.EntityId = (ushort)entitiesDatum.Id;
                dictionary.Add(entitiesDatum.Id, entity);
                tmpList.Add(entitiesDatum);
            }
            catch (Exception innerException)
            {
                throw new Exception(
                    $"Error creating entity from template \"{entitiesDatum.ValuesDictionary.DatabaseObject.Name}\".",
                    innerException);
            }
        }

        foreach (var rp in toRemovePlayer)
        {
            entityDataList.EntitiesData.Remove(rp);
        }

        var entitiesToRemove = new List<Entity>();
        var num = 0;
        foreach (var entitiesDatum2 in tmpList)
        {
            try
            {
                list[num].PublicLoadEntity(entitiesDatum2.ValuesDictionary, idToEntityMap);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load entity {entitiesDatum2.Id}, will skip it. Error: {ex.Message}");
                entitiesToRemove.Add(list[num]);
            }
            num++;
        }

        foreach (var entity in entitiesToRemove)
        {
            list.Remove(entity);
            dictionary.Remove(entity.EntityId);
        }

        return list;
    }

    /// <summary>
    /// Entity包的保存方法
    /// </summary>
    /// <param name="entities"></param>
    /// <returns></returns>
    public EntityDataList SaveEntitiesAll(IEnumerable<Entity> entities)
    {
        var dictionary2 = new Dictionary<Entity, int>();
        var entityToIdMap = new EntityToIdMap(dictionary2);
        var enumerable = entities as Entity[] ?? entities.ToArray();
        foreach (var key in enumerable)
        {
            dictionary2.Add(key, key.EntityId);
        }

        var entityDataList = new EntityDataList
        {
            EntitiesData = new List<EntityData>(_dictionary.Keys.Count)
        };
        foreach (var key2 in enumerable)
        {
            var entityData = new EntityData
            {
                Id = entityToIdMap.FindId(key2),
                ValuesDictionary = new ValuesDictionary
                {
                    DatabaseObject = key2.ValuesDictionary.DatabaseObject
                }
            };
            key2.InternalSaveEntity(entityData.ValuesDictionary, entityToIdMap);
            entityDataList.EntitiesData.Add(entityData);
        }

        return entityDataList;
    }

    private void Load(Dictionary<string, Subsystem> dictionary, ProjectData projectData)
    {
        var loadedSubsystems = new Dictionary<Subsystem, bool>();
        foreach (var value3 in dictionary.Values)
        {
            LoadSubsystem(value3, dictionary, loadedSubsystems, 0);
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        var entities = LoadEntities(projectData.EntityDataList);
        AddEntities(entities);
    }
}

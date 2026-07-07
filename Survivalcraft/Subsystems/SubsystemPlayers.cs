using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemPlayers : Subsystem, IUpdateable
{
    private readonly Dictionary<Guid, EntityData> _offlinePlayerEntities = new();

    private readonly List<PlayerData> _toRemove = [];

    private readonly Dictionary<int, PlayerData> _usedIndies = new();

    public readonly Dictionary<string, string> BlackPlayerGuidList = new();

    private readonly List<ComponentPlayer> _componentPlayers = [];

    public int NextPlayerIndex;

    private readonly List<PlayerData> _playersData = [];

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemGameWidgets _subsystemGameWidgets = null!;

    private SubsystemTime _subsystemTime = null!;

    public int MaxGroup = 100; //最大队伍数量

    public readonly List<string> NoMsgPlayerGuidList = [];

    public string PlayerEntitiesDir = string.Empty;

    public readonly Dictionary<string, Group> ServerGroups = new();

    public bool ShouldEnterCreate;

    public ReadOnlyList<PlayerData> PlayersData => new(_playersData);

    public ReadOnlyList<ComponentPlayer> ComponentPlayers => new(_componentPlayers);

    public Vector3 GlobalSpawnPosition { get; set; }

    private PlayerData MainPlayerData
    {
        get
        {
            field ??= PlayersData.FirstOrDefault(p => p.IsMainPlayer) ??
                      throw new InvalidOperationException("PlayersData is empty, MainPlayerData not found");
            return field;
        }
    } = null!;

    public ComponentPlayer? MainPlayer => MainPlayerData?.ComponentPlayer;

    public UpdateOrder UpdateOrder => UpdateOrder.SubsystemPlayers;

    public void Update(float dt)
    {
        if (RunMode.Value is RunModeType.Gui &&
            (_playersData.Count == 0 ||
             _playersData.All(p => !p.IsMainPlayer)))
        {
            ScreensManager.SwitchScreen("Player", PlayerScreen.Mode.Initial, Project);
        }

        foreach (var playersDatum in _playersData) // 断开连接离线
        {
            if (CommonLib.WorkType == WorkType.Server && playersDatum.Client is null)
            {
                if (RunMode.Value is RunModeType.Gui)
                {
                    _toRemove.Add(playersDatum);
                }
            }
            else
            {
                playersDatum.Update();
            }
        }

        foreach (var playerData in _toRemove)
        {
            // Startup/recovery cleanup for stale disconnected players should not emit "退出游戏" broadcast.
            MakePlayerOffline(playerData.PlayerGUID, false);
        }

        _toRemove.Clear();
    }

    public event Action<PlayerData>? PlayerAdded;
    public event Action<PlayerData>? PlayerRemoved;

    public bool IsPlayer(Entity entity)
    {
        return _componentPlayers.Any(componentPlayer => entity == componentPlayer.Entity);
    }

    public ComponentPlayer? FindNearestPlayer(Vector3 position)
    {
        ComponentPlayer? result = null;
        var num = float.MaxValue;
        foreach (var componentPlayer in ComponentPlayers)
        {
            var num2 = Vector3.DistanceSquared(componentPlayer.ComponentBody.Position, position);
            if (!(num2 < num))
            {
                continue;
            }

            num = num2;
            result = componentPlayer;
        }

        return result;
    }

    public void FindUnusedIndex(PlayerData playerData)
    {
        for (var i = 0; i < int.MaxValue; i++)
        {
            if (!_usedIndies.TryAdd(i, playerData))
            {
                continue;
            }

            playerData.PlayerIndex = i;
            return;
        }
    }

    public void AddPlayerData(PlayerData playerData)
    {
        if (_playersData.Contains(playerData) || _playersData.Any(pd => pd.PlayerGUID == playerData.PlayerGUID))
        {
            throw new InvalidOperationException("Player already added.");
        }

        _playersData.Add(playerData);

        FindUnusedIndex(playerData);

        PlayerAdded?.Invoke(playerData);

        if (string.IsNullOrEmpty(playerData.CurrentState))
        {
            playerData.TransitionTo("FirstUpdate");
        }

        if (CommonLib.WorkType == WorkType.Server)
        {
            var client = playerData.Client;
            if (client is not null && !string.IsNullOrEmpty(client.Nickname))
            {
                playerData.Name = client.Nickname;
            }


            if (client is not null)
            {
                _subsystemGameWidgets.AddMessage(playerData.Name + " 加入游戏");
            }
        }

        if (SettingsManager.Current.AutoGarbageCollect)
        {
            GC.Collect();
        }

        _subsystemTerrain.TerrainUpdater.SetLastChunksUpdateCenter(-1, null);
    }

    public void RemovePlayerData(PlayerData playerData, bool disposeEntity = true)
    {
        if (!_playersData.Contains(playerData))
        {
            throw new InvalidOperationException("Player does not exist.");
        }

        _playersData.Remove(playerData);
        _usedIndies.Remove(playerData.PlayerIndex);
        if (playerData.ComponentPlayer != null)
        {
            try
            {
                Project.RemoveEntity(playerData.ComponentPlayer.Entity, disposeEntity);
            }
            catch
            {
                // ignored
            }
        }

        PlayerRemoved?.Invoke(playerData);
        playerData.Dispose();
        if (SettingsManager.Current.AutoGarbageCollect)
        {
            GC.Collect();
        }

        _subsystemTerrain.TerrainUpdater.SetLastChunksUpdateCenter(-1, null);
    }

    public override void Dispose()
    {
        foreach (var playersDatum in _playersData)
        {
            playersDatum.Dispose();
        }
    }

    public void FindPlayerByClient(Client client, Action<ComponentPlayer> action)
    {
        var playerData = _playersData.Find(p => p.PlayerGUID == client.GUID);
        if (playerData is { ComponentPlayer: not null })
        {
            action.Invoke(playerData.ComponentPlayer);
        }
    }

    public void FindPlayerByClientId(byte id, Action<ComponentPlayer> action)
    {
        var playerData = _playersData.Find(p => p.ClientId == id);
        if (playerData is { ComponentPlayer: not null })
        {
            action.Invoke(playerData.ComponentPlayer);
        }
    }

    public void CreateGroup(PlayerData main, string name)
    {
        var key = main.PlayerGUID.ToString();
        if (ServerGroups.TryGetValue(key, out var v))
        {
            return;
        }

        v = new Group { Name = name };
        ServerGroups.Add(key, v);
        main.GroupKey = key;
        v.Members.Add(main.PlayerGUID);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameWidgets = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        NextPlayerIndex = valuesDictionary.GetValue<int>("NextPlayerIndex");
        GlobalSpawnPosition = valuesDictionary.GetValue<Vector3>("GlobalSpawnPosition");
        var blackPlayers = valuesDictionary.GetValue("BlackPlayerGuidList", new ValuesDictionary());
        foreach (var item in blackPlayers)
        {
            BlackPlayerGuidList[item.Key] = (string)item.Value;
        }

        var noMsgPlayers = valuesDictionary.GetValue("NoMsgPlayerGuidList", new ValuesDictionary());
        foreach (string item in noMsgPlayers.Values)
        {
            NoMsgPlayerGuidList.Add(item);
        }

        foreach (ValuesDictionary item in valuesDictionary.GetValue("Players", new ValuesDictionary()).Values)
        {
            var playerData = new PlayerData(Project);
            playerData.Load(item);
            AddPlayerData(playerData);
        }

        foreach (var item in valuesDictionary.GetValue("ServerGroups", new ValuesDictionary()))
        {
            var group = new Group();
            if (item.Value is not ValuesDictionary vd)
            {
                continue;
            }

            group.Name = vd.GetValue("Name", group.Name);
            foreach (Guid item2 in vd.GetValue<ValuesDictionary>("Members").Values)
            {
                group.Members.Add(item2);
            }

            ServerGroups.Add(item.Key, group);
        }

        foreach (var item in valuesDictionary.GetValue("OfflinePlayerEntities", new ValuesDictionary()))
        {
            if (Guid.TryParseExact(item.Key, "N", out var res))
            {
                var entityData = new EntityData(Project.GameDatabase, (ValuesDictionary)item.Value);
                _offlinePlayerEntities.Add(res, entityData);
            }
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var onlinePlayersListVd = new ValuesDictionary();
        var num = 0;
        foreach (var playersDatum in _playersData)
        {
            if (Project.SendToClientMode &&
                RunMode.Value is RunModeType.HeadlessServer &&
                playersDatum.Client == null)
            {
                continue;
            }

            var valuesDictionary3 = new ValuesDictionary();
            playersDatum.Save(valuesDictionary3);
            onlinePlayersListVd.SetValue(num++.ToString(), valuesDictionary3);
        }

        var blackPlayers = new ValuesDictionary();
        foreach (var playerGuidPair in BlackPlayerGuidList)
        {
            blackPlayers.SetValue(playerGuidPair.Key, playerGuidPair.Value);
        }

        valuesDictionary.SetValue("BlackPlayerGuidList", blackPlayers);
        var noMsgPlayers = new ValuesDictionary();
        for (var i = 0; i < NoMsgPlayerGuidList.Count; i++)
        {
            blackPlayers.SetValue(i.ToString(), NoMsgPlayerGuidList[i]);
        }

        valuesDictionary.SetValue("NoMsgPlayerGuidList", noMsgPlayers);
        valuesDictionary.SetValue("GlobalSpawnPosition", GlobalSpawnPosition);
        valuesDictionary.SetValue("Players", onlinePlayersListVd);
        var offline = new ValuesDictionary();
        valuesDictionary.SetValue("OfflinePlayerEntities", offline);
        var groupValues = new ValuesDictionary();
        valuesDictionary.SetValue("ServerGroups", groupValues);
        foreach (var obj in ServerGroups)
        {
            var vd = new ValuesDictionary();
            var ml = new ValuesDictionary();
            groupValues.SetValue(obj.Key, vd);
            vd.SetValue("Name", obj.Value.Name);
            vd.SetValue("Members", ml);
            var dd = 0;
            foreach (var obj2 in obj.Value.Members)
            {
                ml.SetValue(dd++.ToString(), obj2);
            }
        }

        //同步Project情况下不保存_offlinePlayerEntities数据
        if (Project is { SendToClientMode: true })
        {
            return;
        }
    }

    public override void OnEntityAdded(Entity entity)
    {
        foreach (var playersDatum in _playersData)
        {
            playersDatum.OnEntityAdded(entity);
        }

        UpdateComponentPlayers();
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var playersDatum in _playersData)
        {
            playersDatum.OnEntityRemoved(entity);
        }

        UpdateComponentPlayers();
    }

    private void UpdateComponentPlayers()
    {
        _componentPlayers.Clear();
        foreach (var playersDatum in _playersData)
        {
            if (playersDatum.ComponentPlayer != null &&
                (RunMode.Value is not RunModeType.HeadlessServer || playersDatum.Client != null))
            {
                _componentPlayers.Add(playersDatum.ComponentPlayer);
            }
        }
    }

    /// <summary>
    /// 将实例保存为离线数据
    /// </summary>
    /// <param name="playerGuid"></param>
    /// <param name="showMsg">是否广播退出消息</param>
    public void MakePlayerOffline(Guid playerGuid, bool showMsg = true)
    {
        var pd = _playersData.Find(p => p.PlayerGUID == playerGuid);
        if (pd == null)
        {
            return;
        }

        var updater = _subsystemTerrain.TerrainUpdater;
        if (pd.Client != null)
        {
            updater.WaitChunkList.Remove(pd.Client);
        }

        var componentPlayer = pd.ComponentPlayer;
        if (componentPlayer == null && _offlinePlayerEntities.TryGetValue(playerGuid, out var entityDataPlayer))
        {
            var list = new EntityDataList
            {
                EntitiesData =
                [
                    entityDataPlayer
                ]
            };
            _offlinePlayerEntities.Remove(playerGuid);
            var entityList = GameManager.Project?.InitializeAndLoadEntities(list) ?? [];
            GameManager.Project?.AttachEntities(entityList, true);
            var entity = entityList.Count != 0 ? entityList[0] : null;
            componentPlayer = entity?.FindComponent<ComponentPlayer>();
        }

        if (componentPlayer != null)
        {
            Project.RemoveEntity(componentPlayer.Entity, true);
            if (showMsg && CommonLib.WorkType == WorkType.Server)
            {
                _subsystemGameWidgets.AddMessage(pd.Name + " 退出游戏");
            }

            if (CommonLib.WorkType == WorkType.Server)
            {
                var list = Project.SaveEntities([componentPlayer.Entity]);
                var dict = new ValuesDictionary();
                var vDict = new ValuesDictionary();
                var pDict = new ValuesDictionary();
                list.EntitiesData[0].Save(vDict);
                pd.Save(pDict);
                var entityFileDir = Storage.CombinePaths(_subsystemGameInfo.DirectoryName, "PlayerEntities/");
                if (!Storage.DirectoryExists(entityFileDir))
                {
                    Storage.CreateDirectory(entityFileDir);
                }

                dict.SetValue("Entity", vDict);
                dict.SetValue("Data", pDict);
                using var stream = Storage.OpenFile(Storage.CombinePaths(entityFileDir, $"{pd.PlayerGUID}.json"),
                    OpenFileMode.Create);
                var streamWriter = new StreamWriter(stream);
                streamWriter.Write(dict.ToJsonText());
                streamWriter.Flush();
                streamWriter.Dispose();
            }
        }

        _subsystemTerrain.TerrainUpdater.RemoveUpdateLocation(pd.PlayerIndex);
        RemovePlayerData(pd);
    }

    private bool CheckPlayerDataExists(Guid playerGuid, out string entityFilePath)
    {
        var entityFileDir = Storage.CombinePaths(_subsystemGameInfo.DirectoryName, "PlayerEntities/");
        entityFilePath = Storage.CombinePaths(entityFileDir, $"{playerGuid}.json");
        if (!Storage.FileExists(entityFilePath))
        {
            entityFilePath = Storage.CombinePaths(entityFileDir, $"{playerGuid}.dat");
        }

        return Storage.FileExists(entityFilePath);
    }

    /// <summary>
    /// 读取离线数据到实例，此步骤会将PlayerData添加到在线列表中，但状态是等待玩家实体
    /// </summary>
    /// <param name="playerGuid">客户端GUID</param>
    /// <param name="playerData">对应的玩家数据</param>
    /// <param name="entity">对应的玩家实体数据</param>
    /// <returns></returns>
    public bool MakePlayerOnline(Guid playerGuid, out PlayerData? playerData, out Entity? entity)
    {
        if (CheckPlayerDataExists(playerGuid, out var entityFilePath) && CommonLib.WorkType == WorkType.Server)
        {
            var existing = _playersData.Find(p => p.PlayerGUID == playerGuid);
            var dict = new ValuesDictionary();
            using (var s = Storage.OpenFile(entityFilePath, OpenFileMode.Read))
            {
                if (entityFilePath.EndsWith(".json"))
                {
                    var reader = new StreamReader(s);
                    var jsonText = reader.ReadToEnd();
                    reader.Dispose();
                    dict.ApplyOverridesUseJson(jsonText, out var data);
                }
                else if (entityFilePath.EndsWith(".dat"))
                {
                    var d = new byte[s.Length];
                    s.ReadExactly(d, 0, d.Length);
                    dict.ApplyOverridesUseMessagePack(d);
                    Storage.DeleteFile(entityFilePath);
                }
            }

            if (existing != null)
            {
                playerData = existing;
            }
            else
            {
                playerData = new PlayerData(Project);
                playerData.Load(dict.GetValue<ValuesDictionary>("Data"));
                AddPlayerData(playerData);
            }

            playerData.WaitEntityAdded();
            //如果队伍不存在了自动退出组队
            if (!ServerGroups.ContainsKey(playerData.GroupKey))
            {
                playerData.GroupKey = string.Empty;
            }

            var entityData = new EntityData(Project.GameDatabase, dict.GetValue<ValuesDictionary>("Entity"));
            var list = new EntityDataList
            {
                EntitiesData =
                [
                    entityData
                ]
            };
            var entityList = GameManager.Project?.InitializeAndLoadEntities(list) ?? [];
            GameManager.Project?.AttachEntities(entityList, true);
            if (entityList.Count > 0)
            {
                entity = entityList[0];
                var componentPlayer = entity.FindComponent<ComponentPlayer>();
                if (componentPlayer != null && componentPlayer.PlayerData.PlayerGUID != playerGuid)
                {
                    throw new Exception("缓存PlayerData与实际PlayerData不对应?");
                }
            }
            else
            {
                entity = null;
            }

            return true;
        }

        playerData = null;
        entity = null;
        return false;
    }

    public void AddNoMsgList(PlayerData playerData)
    {
        var guid = playerData.PlayerGUID.ToString();
        if (NoMsgPlayerGuidList.Contains(guid))
        {
            return;
        }

        NoMsgPlayerGuidList.Add(guid);
        DialogsManager.Alert($"已成功将{playerData.Name}禁言");
        CommonLib.Net.QueuePackage(
            new MessagePackage(
                string.Empty,
                "你已被管理员禁言",
                0,
                [playerData.ClientId]
            )
        );
        CommonLib.Net.QueuePackage(new PlayerDataPackage(playerData.PlayerGUID, true));
    }

    public void RemoveNoMsgList(PlayerData playerData)
    {
        var guid = playerData.PlayerGUID.ToString();
        if (!NoMsgPlayerGuidList.Contains(guid))
        {
            return;
        }

        NoMsgPlayerGuidList.Remove(guid);
        DialogsManager.Alert($"已成功将{playerData.Name}解除禁言");
        CommonLib.Net.QueuePackage(
            new MessagePackage(
                string.Empty,
                "你已被管理员解除禁言",
                0,
                [playerData.ClientId]
            )
        );
        CommonLib.Net.QueuePackage(new PlayerDataPackage(playerData.PlayerGUID, false));
    }

    public void AddBlackList(PlayerData playerData)
    {
        var guid = playerData.PlayerGUID.ToString();
        if (BlackPlayerGuidList.ContainsKey(guid))
        {
            return;
        }

        BlackPlayerGuidList.Add(guid, playerData.Name);
        CommonLib.Net.RemoveClientImmediate(playerData.Client!, "服务器管理已将你拉入黑名单");
        DialogsManager.Alert($"已成功将{playerData.Name}拉入黑名单");
    }

    public PlayerData? FindPlayerData(Func<PlayerData, bool> f)
    {
        return _playersData.FirstOrDefault(f);
    }

    public static bool IsMainPlayer(Entity e)
    {
        var player = e.FindComponent<ComponentPlayer>();
        return player is { PlayerData.IsMainPlayer: true };
    }

    public class Group
    {
        /// <summary>
        /// 队伍成员
        /// </summary>
        public readonly List<Guid> Members = [];

        /// <summary>
        /// 队伍名称
        /// </summary>
        public string Name = string.Empty;
    }
}

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Messaging;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public partial class SubsystemPlayers : Subsystem, IUpdateable
{
    private sealed record OfflinePlayerData(ValuesDictionary PlayerData, ValuesDictionary EntityData);

    private readonly Dictionary<Guid, OfflinePlayerData> _offlinePlayers = new();

    private readonly Dictionary<Guid, OnlinePlayerState> _onlinePlayerStates = new();

    private readonly Dictionary<Guid, PlayerListEntry> _playerList = new();

    private readonly List<PlayerData> _toRemove = [];

    private readonly Dictionary<int, PlayerData> _usedIndies = new();

    public readonly Dictionary<string, string> BlackPlayerGuidList = new();

    private readonly List<ComponentPlayer> _componentPlayers = [];

    private readonly List<PlayerData> _playersData = [];

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemGameWidgets _subsystemGameWidgets = null!;

    private SubsystemTime _subsystemTime = null!;

    public readonly Dictionary<string, Group> ServerGroups = new();

    public ReadOnlyList<PlayerData> PlayersData => new(_playersData);

    public ReadOnlyList<ComponentPlayer> ComponentPlayers => new(_componentPlayers);

    public IReadOnlyDictionary<Guid, OnlinePlayerState> OnlinePlayerStates => _onlinePlayerStates;

    public IReadOnlyDictionary<Guid, PlayerListEntry> PlayerList => _playerList;

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
            MakePlayerOffline(playerData.PlayerGUID, false);
        }

        _toRemove.Clear();

        if (CommonLib.WorkType == WorkType.Server && Time.PeriodicEvent(0.25, 0.0))
        {
            CommonLib.Net.QueuePackage(new OnlinePlayerStatePackage(this));
        }
    }

    public event Action<PlayerData>? PlayerAdded;
    public event Action<PlayerData>? PlayerRemoved;
    public event Action? PlayerListChanged;

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
        var existing = _playersData.FirstOrDefault(pd => pd.PlayerGUID == playerData.PlayerGUID);
        if (existing != null && !ReferenceEquals(existing, playerData))
        {
            // 玩家重连时服务端会重新广播 PlayerData，旧数据可能因客户端未收到离线通知而残留。
            // 直接替换旧记录，避免“Player already added”异常导致新 PlayerData 无法建立、
            // 以及后续玩家实体加载失败。
            _playersData.Remove(existing);
            _usedIndies.Remove(existing.PlayerIndex);
        }

        _playersData.Add(playerData);
        SetPlayerListEntry(new PlayerListEntry(
            playerData.PlayerGUID,
            playerData.Name,
            true));

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
                _subsystemGameWidgets.Messages.Publish(
                    GameMessage.LocalizedSystem(
                        "MultiplayerUI",
                        "PlayerJoined",
                        [playerData.Name],
                        presentation:
                        GameMessagePresentation.Default | GameMessagePresentation.Toast));
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

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameWidgets = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        GlobalSpawnPosition = valuesDictionary.GetValue<Vector3>("GlobalSpawnPosition");
        var blackPlayers = valuesDictionary.GetValue("BlackPlayerGuidList", new ValuesDictionary());
        foreach (var item in blackPlayers)
        {
            BlackPlayerGuidList[item.Key] = (string)item.Value;
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

        foreach (var item in valuesDictionary.GetValue("OfflinePlayers", new ValuesDictionary()))
        {
            if (item.Value is not ValuesDictionary offlinePlayer)
            {
                throw new InvalidOperationException($"Invalid offline player record '{item.Key}'.");
            }

            var playerGuid = Guid.ParseExact(item.Key, "N");
            var playerData = offlinePlayer.GetValue<ValuesDictionary>("Data");
            var entityData = offlinePlayer.GetValue<ValuesDictionary>("Entity");
            _offlinePlayers.Add(playerGuid, new OfflinePlayerData(playerData, entityData));
        }

        RefreshPlayerList();
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
        valuesDictionary.SetValue("GlobalSpawnPosition", GlobalSpawnPosition);
        valuesDictionary.SetValue("Players", onlinePlayersListVd);
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

        // Offline players are server persistence state and are not part of client project snapshots.
        if (Project.SendToClientMode)
        {
            return;
        }

        var offlinePlayers = new ValuesDictionary();
        foreach (var item in _offlinePlayers)
        {
            var record = new ValuesDictionary();
            record.SetValue("Data", item.Value.PlayerData);
            record.SetValue("Entity", item.Value.EntityData);
            offlinePlayers.SetValue(item.Key.ToString("N"), record);
        }

        valuesDictionary.SetValue("OfflinePlayers", offlinePlayers);
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
        if (componentPlayer != null)
        {
            if (showMsg && CommonLib.WorkType == WorkType.Server)
            {
                _subsystemGameWidgets.Messages.Publish(
                    GameMessage.LocalizedSystem(
                        "MultiplayerUI",
                        "PlayerLeft",
                        [pd.Name],
                        presentation:
                        GameMessagePresentation.Default | GameMessagePresentation.Toast));
            }

            if (CommonLib.WorkType == WorkType.Server)
            {
                var list = Project.SaveEntities([componentPlayer.Entity]);
                if (list.EntitiesData.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected one serialized entity for player {playerGuid}, got {list.EntitiesData.Count}.");
                }

                var playerValues = new ValuesDictionary();
                var entityValues = new ValuesDictionary();
                pd.Save(playerValues);
                list.EntitiesData[0].Save(entityValues);
                _offlinePlayers[playerGuid] = new OfflinePlayerData(playerValues, entityValues);
            }

            Project.RemoveEntity(componentPlayer.Entity, true);
        }

        SetPlayerListEntry(new PlayerListEntry(
            pd.PlayerGUID,
            pd.Name,
            false));
        _subsystemTerrain.TerrainUpdater.RemoveUpdateLocation(pd.PlayerIndex);
        RemovePlayerData(pd);
    }

    /// <summary>
    /// 将离线玩家数据恢复为活动玩家实体。
    /// </summary>
    /// <param name="playerGuid">客户端GUID</param>
    /// <param name="playerData">对应的玩家数据</param>
    /// <param name="entity">对应的玩家实体数据</param>
    /// <returns></returns>
    public bool MakePlayerOnline(Guid playerGuid, out PlayerData? playerData, out Entity? entity)
    {
        if (CommonLib.WorkType != WorkType.Server)
        {
            playerData = null;
            entity = null;
            return false;
        }

        var existing = _playersData.Find(p => p.PlayerGUID == playerGuid);
        if (existing?.ComponentPlayer?.Entity is { IsAddedToProject: true } existingEntity)
        {
            existing.WaitEntityAdded();
            NormalizePlayerGroup(existing);
            playerData = existing;
            entity = existingEntity;
            return true;
        }

        if (!_offlinePlayers.TryGetValue(playerGuid, out var offlinePlayer))
        {
            playerData = null;
            entity = null;
            return false;
        }

        if (existing != null)
        {
            throw new InvalidOperationException(
                $"Player {playerGuid} has active data but no active entity.");
        }

        PlayerData? restoredPlayerData = null;
        List<Entity> entityList = [];
        var playerDataAdded = false;
        try
        {
            restoredPlayerData = new PlayerData(Project);
            restoredPlayerData.Load(offlinePlayer.PlayerData);
            if (restoredPlayerData.PlayerGUID != playerGuid)
            {
                throw new InvalidOperationException(
                    $"Offline player record key {playerGuid} does not match data guid " +
                    $"{restoredPlayerData.PlayerGUID}.");
            }

            AddPlayerData(restoredPlayerData);
            playerDataAdded = true;
            restoredPlayerData.WaitEntityAdded();
            NormalizePlayerGroup(restoredPlayerData);

            var list = new EntityDataList
            {
                EntitiesData =
                [
                    new EntityData(Project.GameDatabase, offlinePlayer.EntityData)
                ]
            };
            entityList = Project.InitializeEntities(list);
            Project.LoadEntityData(list, entityList);
            if (entityList.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one restored entity for player {playerGuid}, got {entityList.Count}.");
            }

            entity = entityList[0];
            var componentPlayer = entity.FindComponent<ComponentPlayer>();
            if (componentPlayer == null || componentPlayer.PlayerData.PlayerGUID != playerGuid)
            {
                throw new InvalidOperationException(
                    $"Restored entity does not belong to player {playerGuid}.");
            }

            // 延迟挂载：实体先不加入项目，等客户端到达 ProjectLoaded 后由 GameManager 挂载。
            // 这样 AddPlayer(PlayerData) 广播必然先于实体广播到达其它客户端，避免实体先于
            // PlayerData 被解码导致玩家实体加载失败。
            entity.EntityId = 0;
            // 离线记录保留在 _offlinePlayers 中（读取不删除），避免客户端在 ProjectLoaded
            // 之前断开时丢失玩家数据；下次正常退出时会重新覆盖为最新数据。
            playerData = restoredPlayerData;
            return true;
        }
        catch
        {
            foreach (var initializedEntity in entityList)
            {
                if (initializedEntity.IsAddedToProject)
                {
                    Project.RemoveEntity(initializedEntity, true);
                }
                else
                {
                    initializedEntity.Dispose();
                }
            }

            if (playerDataAdded && restoredPlayerData != null &&
                _playersData.Contains(restoredPlayerData))
            {
                RemovePlayerData(restoredPlayerData);
            }

            throw;
        }
    }

    private void NormalizePlayerGroup(PlayerData playerData)
    {
        // 如果队伍不存在了自动退出组队
        if (!ServerGroups.ContainsKey(playerData.GroupKey))
        {
            playerData.GroupKey = string.Empty;
        }
    }

    public void ApplyOnlinePlayerStates(IEnumerable<OnlinePlayerState> states)
    {
        _onlinePlayerStates.Clear();
        foreach (var state in states)
        {
            _onlinePlayerStates[state.PlayerGuid] = state;
        }
    }

    public void ApplyPlayerList(IEnumerable<PlayerListEntry> players)
    {
        _playerList.Clear();
        foreach (var player in players)
        {
            _playerList[player.PlayerGuid] = player;
            var activePlayer = _playersData.Find(data => data.PlayerGUID == player.PlayerGuid);
            if (activePlayer is null)
            {
                continue;
            }

            activePlayer.Name = player.Name;
        }

        foreach (var playerGuid in _onlinePlayerStates.Keys
                     .Where(playerGuid =>
                         !_playerList.TryGetValue(playerGuid, out var player) ||
                         !player.IsOnline)
                     .ToList())
        {
            _onlinePlayerStates.Remove(playerGuid);
        }

        PlayerListChanged?.Invoke();
    }

    public void RefreshPlayerList()
    {
        var entries = new Dictionary<Guid, PlayerListEntry>();
        foreach (var offlinePlayer in _offlinePlayers)
        {
            entries[offlinePlayer.Key] = new PlayerListEntry(
                offlinePlayer.Key,
                offlinePlayer.Value.PlayerData.GetValue("Name", "Player"),
                false);
        }

        foreach (var playerData in _playersData)
        {
            entries[playerData.PlayerGUID] = new PlayerListEntry(
                playerData.PlayerGUID,
                playerData.Name,
                true);
        }

        _playerList.Clear();
        foreach (var entry in entries)
        {
            _playerList.Add(entry.Key, entry.Value);
        }

        PlayerListChanged?.Invoke();
    }

    public string GetPlayerGroupKey(Guid playerGuid)
    {
        foreach (var group in ServerGroups)
        {
            if (group.Value.Members.Contains(playerGuid))
            {
                return group.Key;
            }
        }

        return string.Empty;
    }

    private void SetPlayerListEntry(PlayerListEntry player)
    {
        _playerList[player.PlayerGuid] = player;
        PlayerListChanged?.Invoke();
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

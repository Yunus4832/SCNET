using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Protocol.Identity;

namespace Game.Subsystems;

public class SubsystemCharacters : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    private sealed record OfflineCharacterData(ValuesDictionary CharacterData, ValuesDictionary? EntityData);

    private readonly Dictionary<Guid, OfflineCharacterData> _offlineCharacters = new();

    private readonly Dictionary<Guid, OnlineCharacterState> _onlineCharacterStates = new();

    private readonly Dictionary<Guid, CharacterListEntry> _characterList = new();

    private readonly List<CharacterData> _toRemove = [];

    private readonly Dictionary<int, CharacterData> _usedIndies = new();

    private readonly List<ComponentCharacter> _componentCharacters = [];

    private readonly List<CharacterData> _characters = [];

    public Guid PendingCharacterCreationId { get; private set; }

    public event Action<CharacterCreationError>? CharacterCreationRejected;

    public event Action<CharacterData>? OwnCharacterCreationRequested;

    public event Action? Closing;

    public event Action<CharacterData>? CharacterUpdated;

    public event Action<CharacterData>? CharacterRemoving;

    public event Action<CharacterRosterMutation>? RosterMutationCompleted;

    public Guid RequestOwnCharacterCreation(CharacterData profile)
    {
        var runtime = WorldRuntimeContext.For(Project);
        if (runtime.IsAuthority || WorldConnections.For(runtime).Authority is null)
        {
            throw new InvalidOperationException("Character requests require a client connected to an authority.");
        }

        if (PendingCharacterCreationId != Guid.Empty) { return PendingCharacterCreationId; }

        PendingCharacterCreationId = Guid.NewGuid();
        OwnCharacterCreationRequested?.Invoke(profile);
        NetworkSender.SendToServer(
            runtime,
            CharacterPackageBuilder.CaptureCreation(profile, PendingCharacterCreationId)
        );
        return PendingCharacterCreationId;
    }

    public void ClearCharacterCreationRequest()
    {
        PendingCharacterCreationId = Guid.Empty;
    }

    public void RejectCharacterCreation(Guid requestId, CharacterCreationError error)
    {
        if (requestId == Guid.Empty || requestId != PendingCharacterCreationId || !Enum.IsDefined(error)) { return; }

        PendingCharacterCreationId = Guid.Empty;
        CharacterCreationRejected?.Invoke(error);
    }

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemMessages _subsystemMessages = null!;

    private SubsystemTime _subsystemTime = null!;

    public ReadOnlyList<CharacterData> Characters => new(_characters);

    public ReadOnlyList<ComponentCharacter> ComponentCharacters => new(_componentCharacters);

    public IReadOnlyDictionary<Guid, OnlineCharacterState> OnlineCharacterStates => _onlineCharacterStates;

    public IReadOnlyDictionary<Guid, CharacterListEntry> CharacterList => _characterList;

    public Vector3 GlobalSpawnPosition { get; set; }

    public ComponentCharacter? LocalCharacter =>
        _characters.FirstOrDefault(character => character.IsLocalCharacter)?.ComponentCharacter;

    public UpdateOrder UpdateOrder => UpdateOrder.SubsystemCharacters;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority &&
            WorldConnections.For(Project).Authority?.ConnectionPhase !=
            ConnectionPhase.Live)
        {
            return;
        }

        foreach (var playersDatum in _characters) // 断开连接离线
        {
            if (WorldRuntimeContext.For(Project).IsAuthority && playersDatum.ControllerConnection is null)
            {
                QueueCharacterOffline(playersDatum);
            }
            else
            {
                playersDatum.Update();
            }

            CharacterUpdated?.Invoke(playersDatum);
        }

        foreach (var characterData in _toRemove)
        {
            MakeCharacterOffline(characterData.CharacterId, false);
        }

        _toRemove.Clear();

        if (WorldRuntimeContext.For(Project).IsAuthority && Time.PeriodicEvent(0.25, 0.0))
        {
            SendOnlineCharacterStates();
        }
    }

    internal void QueueCharacterOffline(CharacterData characterData)
    {
        if (!_toRemove.Contains(characterData))
        {
            _toRemove.Add(characterData);
        }
    }

    public event Action<CharacterData>? CharacterAdded;

    public event Action<CharacterData>? CharacterRemoved;

    public event Action? CharacterListChanged;

    private void SendOnlineCharacterStates()
    {
        foreach (var recipient in _characters)
        {
            if (recipient.IsLocalCharacter || recipient.ControllerConnection is not { IsConnected: true } client)
            {
                continue;
            }

            var states = _characters
                .Where(player => player.ComponentCharacter is not null)
                .Select(player =>
                    {
                        var componentCharacter = player.ComponentCharacter!;
                        return new OnlineCharacterState(
                            player.CharacterId,
                            componentCharacter.ComponentBody.Position,
                            MathUtils.Saturate(componentCharacter.ComponentHealth.Health),
                            componentCharacter.ComponentSleep.IsSleeping
                        );
                    }
                );
            NetworkSender.Send(
                Project,
                new OnlineCharacterStatePackage(states),
                PackageAudience.To(client)
            );
        }
    }

    public bool IsCharacter(Entity entity)
    {
        return _componentCharacters.Any(componentCharacter => entity == componentCharacter.Entity);
    }

    public ComponentCharacter? FindNearestCharacter(Vector3 position)
    {
        ComponentCharacter? result = null;
        var num = float.MaxValue;
        foreach (var componentCharacter in ComponentCharacters)
        {
            var num2 = Vector3.DistanceSquared(componentCharacter.ComponentBody.Position, position);
            if (!(num2 < num))
            {
                continue;
            }

            num = num2;
            result = componentCharacter;
        }

        return result;
    }

    public void FindUnusedIndex(CharacterData characterData)
    {
        for (var i = 0; i < int.MaxValue; i++)
        {
            if (!_usedIndies.TryAdd(i, characterData))
            {
                continue;
            }

            characterData.CharacterIndex = i;
            return;
        }
    }

    public bool IsCharacterNameAvailable(string name, Guid exceptCharacterId = default)
    {
        if (!CharacterData.VerifyName(name)) { return false; }

        if (_characters.Any(player => player.CharacterId != exceptCharacterId &&
                                      string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase)
            ))
        {
            return false;
        }

        if (!WorldRuntimeContext.For(Project).IsAuthority &&
            _characterList.Values.Any(entry =>
                entry.CharacterId != exceptCharacterId &&
                _characters.All(player => player.CharacterId != entry.CharacterId) &&
                string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)
            )) { return false; }

        // Active data supersedes its saved offline copy, including a subsequent rename.
        return !_offlineCharacters.Any(entry => entry.Key != exceptCharacterId &&
                                                _characters.All(player => player.CharacterId != entry.Key) &&
                                                string.Equals(
                                                    entry.Value.CharacterData.GetValue<string>("Name"),
                                                    name,
                                                    StringComparison.OrdinalIgnoreCase
                                                )
        );
    }

    public void AddCharacterData(CharacterData characterData)
    {
        if (!IsCharacterNameAvailable(characterData.Name, characterData.CharacterId))
        {
            throw new InvalidOperationException("The character name is invalid or already used in this world.");
        }

        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            Project.FindSubsystem<SubsystemPlayers>(true)!.Store.BindCharacter(
                characterData.OwnerAddress,
                characterData.CharacterId
            );
        }

        var existing = _characters.FirstOrDefault(pd => pd.CharacterId == characterData.CharacterId);
        if (existing != null && !ReferenceEquals(existing, characterData))
        {
            CharacterRemoving?.Invoke(existing);
            // 玩家重连时服务端会重新广播 CharacterData，旧数据可能因客户端未收到离线通知而残留。
            // 直接替换旧记录，避免“Player already added”异常导致新 CharacterData 无法建立、
            // 以及后续玩家实体加载失败。
            _characters.Remove(existing);
            _usedIndies.Remove(existing.CharacterIndex);
            CharacterRemoved?.Invoke(existing);
            existing.Dispose();
        }

        _characters.Add(characterData);
        SetCharacterListEntry(
            new CharacterListEntry(
                characterData.CharacterId,
                characterData.OwnerAddress,
                characterData.Name,
                true
            )
        );

        FindUnusedIndex(characterData);

        CharacterAdded?.Invoke(characterData);

        if (WorldRuntimeContext.For(Project).IsAuthority && string.IsNullOrEmpty(characterData.CurrentState))
        {
            characterData.TransitionTo("FirstUpdate");
        }

        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            var client = characterData.ControllerConnection;
            if (client is not null)
            {
                _subsystemMessages.Service.Publish(
                    GameMessageLocalization.System(
                        "MultiplayerUI",
                        "PlayerJoined",
                        [characterData.Name],
                        presentation:
                        GameMessagePresentation.Default | GameMessagePresentation.Toast
                    )
                );
            }
        }

        RosterMutationCompleted?.Invoke(CharacterRosterMutation.Added);

        _subsystemTerrain.TerrainUpdater.SetLastChunksUpdateCenter(-1, null);
    }

    public void RemoveCharacterData(CharacterData characterData, bool disposeEntity = true)
    {
        if (!_characters.Contains(characterData))
        {
            throw new InvalidOperationException("Player does not exist.");
        }

        CharacterRemoving?.Invoke(characterData);
        _characters.Remove(characterData);
        _usedIndies.Remove(characterData.CharacterIndex);
        if (characterData.ComponentCharacter != null)
        {
            try
            {
                Project.RemoveEntity(characterData.ComponentCharacter.Entity, disposeEntity);
            }
            catch
            {
                // ignored
            }
        }

        CharacterRemoved?.Invoke(characterData);
        characterData.Dispose();
        RosterMutationCompleted?.Invoke(CharacterRosterMutation.Removed);

        _subsystemTerrain.TerrainUpdater.SetLastChunksUpdateCenter(-1, null);
    }

    public override void Dispose()
    {
        var errors = new List<Exception>();
        foreach (var handler in Closing?.GetInvocationList() ?? [])
        {
            try { ((Action)handler)(); }
            catch (Exception error) { errors.Add(error); }
        }

        Closing = null;
        foreach (var character in _characters)
        {
            try { character.Dispose(); }
            catch (Exception error) { errors.Add(error); }
        }

        if (errors.Count > 0) { throw new AggregateException("Could not dispose character resources.", errors); }
    }

    public void FindCharacterByConnection(WorldConnection client, Action<ComponentCharacter> action)
    {
        var characterData = _characters.Find(p => p.CharacterId == client.CharacterId);
        if (characterData is { ComponentCharacter: not null })
        {
            action.Invoke(characterData.ComponentCharacter);
        }
    }

    public void FindCharacterByClientId(byte id, Action<ComponentCharacter> action)
    {
        var characterData = _characters.Find(p => p.ClientId == id);
        if (characterData is { ComponentCharacter: not null })
        {
            action.Invoke(characterData.ComponentCharacter);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemMessages = Project.FindSubsystem<SubsystemMessages>(true)!;
        GlobalSpawnPosition = valuesDictionary.GetValue<Vector3>("GlobalSpawnPosition");

        foreach (ValuesDictionary item in valuesDictionary.GetValue("Characters", new ValuesDictionary()).Values)
        {
            var characterData = new CharacterData(Project);
            characterData.Load(item);
            AddCharacterData(characterData);
        }

        foreach (var item in valuesDictionary.GetValue("OfflineCharacters", new ValuesDictionary()))
        {
            if (item.Value is not ValuesDictionary offlineCharacter)
            {
                throw new InvalidOperationException($"Invalid offline character record '{item.Key}'.");
            }

            var playerGuid = Guid.ParseExact(item.Key, "N");
            var characterData = offlineCharacter.GetValue<ValuesDictionary>("Data");
            var entityData = offlineCharacter.GetValue<ValuesDictionary?>("Entity", null);
            _offlineCharacters.Add(playerGuid, new OfflineCharacterData(characterData, entityData));
        }

        foreach (var entry in _offlineCharacters)
        {
            if (_characters.Any(player => player.CharacterId == entry.Key)) { continue; }

            if (!IsCharacterNameAvailable(entry.Value.CharacterData.GetValue<string>("Name"), entry.Key))
            {
                throw new InvalidDataException("Stored character names must be valid and unique in this world.");
            }
        }

        RefreshCharacterList();
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var onlineCharactersListVd = new ValuesDictionary();
        var num = 0;
        foreach (var playersDatum in _characters)
        {
            if (Project.SendToClientMode && playersDatum.ControllerConnection == null)
            {
                continue;
            }

            var valuesDictionary3 = new ValuesDictionary();
            playersDatum.Save(valuesDictionary3);
            onlineCharactersListVd.SetValue(num++.ToString(), valuesDictionary3);
        }

        valuesDictionary.SetValue("GlobalSpawnPosition", GlobalSpawnPosition);
        valuesDictionary.SetValue("Characters", onlineCharactersListVd);

        // Offline players are server persistence state and are not part of client project snapshots.
        if (Project.SendToClientMode)
        {
            return;
        }

        var offlineCharacters = new ValuesDictionary();
        foreach (var item in _offlineCharacters)
        {
            var record = new ValuesDictionary();
            record.SetValue("Data", item.Value.CharacterData);
            if (item.Value.EntityData is { } entityData)
            {
                record.SetValue("Entity", entityData);
            }

            offlineCharacters.SetValue(item.Key.ToString("N"), record);
        }

        valuesDictionary.SetValue("OfflineCharacters", offlineCharacters);
    }

    public override void OnEntityAdded(Entity entity)
    {
        foreach (var playersDatum in _characters)
        {
            playersDatum.OnEntityAdded(entity);
        }

        UpdateComponentCharacters();
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var playersDatum in _characters)
        {
            playersDatum.OnEntityRemoved(entity);
        }

        UpdateComponentCharacters();
    }

    public void UpdateComponentCharacters()
    {
        _componentCharacters.Clear();
        foreach (var playersDatum in _characters)
        {
            if (playersDatum.ComponentCharacter != null &&
                (!WorldRuntimeContext.For(Project).IsAuthority || playersDatum.ControllerConnection != null))
            {
                _componentCharacters.Add(playersDatum.ComponentCharacter);
            }
        }
    }

    /// <summary>
    ///     将实例保存为离线数据
    /// </summary>
    /// <param name="playerGuid"></param>
    /// <param name="showMsg">是否广播退出消息</param>
    public void MakeCharacterOffline(Guid playerGuid, bool showMsg = true)
    {
        var pd = _characters.Find(p => p.CharacterId == playerGuid);
        if (pd == null)
        {
            return;
        }

        var updater = _subsystemTerrain.TerrainUpdater;
        if (pd.ControllerConnection != null)
        {
            updater.ServerChunkDistribution?.RemoveClient(pd.ControllerConnection);
        }

        // A committed character can disconnect before its first entity is spawned.
        // Persist the profile even when there is no entity snapshot to restore.
        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            ValuesDictionary? entityValues = null;
            if (pd.ComponentCharacter is { } character)
            {
                var list = Project.SaveEntities([character.Entity]);
                if (list.EntitiesData.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected one serialized entity for player {playerGuid}, got {list.EntitiesData.Count}."
                    );
                }

                entityValues = new ValuesDictionary();
                list.EntitiesData[0].Save(entityValues);
            }

            var characterValues = new ValuesDictionary();
            pd.Save(characterValues);
            _offlineCharacters[playerGuid] = new OfflineCharacterData(characterValues, entityValues);
        }

        var componentCharacter = pd.ComponentCharacter;
        if (componentCharacter != null)
        {
            if (showMsg && WorldRuntimeContext.For(Project).IsAuthority)
            {
                _subsystemMessages.Service.Publish(
                    GameMessageLocalization.System(
                        "MultiplayerUI",
                        "PlayerLeft",
                        [pd.Name],
                        presentation:
                        GameMessagePresentation.Default | GameMessagePresentation.Toast
                    )
                );
            }

            Project.RemoveEntity(componentCharacter.Entity, true);
        }

        SetCharacterListEntry(
            new CharacterListEntry(
                pd.CharacterId,
                pd.OwnerAddress,
                pd.Name,
                false
            )
        );
        _subsystemTerrain.TerrainUpdater.RemoveUpdateLocation(pd.CharacterIndex);
        RemoveCharacterData(pd);
    }

    /// <summary>
    ///     将离线玩家数据恢复为活动玩家实体。
    /// </summary>
    /// <param name="playerGuid">客户端GUID</param>
    /// <param name="characterData">对应的玩家数据</param>
    /// <param name="entity">对应的玩家实体数据</param>
    /// <returns></returns>
    public bool MakeCharacterOnline(Guid playerGuid, out CharacterData? characterData, out Entity? entity)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            characterData = null;
            entity = null;
            return false;
        }

        var existing = _characters.Find(p => p.CharacterId == playerGuid);
        if (existing?.ComponentCharacter?.Entity is { IsAddedToProject: true } existingEntity)
        {
            existing.WaitEntityAdded();
            characterData = existing;
            entity = existingEntity;
            return true;
        }

        if (!_offlineCharacters.TryGetValue(playerGuid, out var offlineCharacter))
        {
            characterData = null;
            entity = null;
            return false;
        }

        if (existing != null)
        {
            throw new InvalidOperationException($"Player {playerGuid} has active data but no active entity.");
        }

        CharacterData? restoredCharacterData = null;
        List<Entity> entityList = [];
        var characterDataAdded = false;
        try
        {
            restoredCharacterData = new CharacterData(Project);
            restoredCharacterData.Load(offlineCharacter.CharacterData);
            if (restoredCharacterData.CharacterId != playerGuid)
            {
                throw new InvalidOperationException(
                    $"Offline player record key {playerGuid} does not match data guid " +
                    $"{restoredCharacterData.CharacterId}."
                );
            }

            AddCharacterData(restoredCharacterData);
            characterDataAdded = true;
            if (offlineCharacter.EntityData is null)
            {
                // Keep FirstUpdate: the ordinary spawn workflow creates the missing entity.
                characterData = restoredCharacterData;
                entity = null;
                return true;
            }

            restoredCharacterData.WaitEntityAdded();

            var list = new EntityDataList
            {
                EntitiesData =
                [
                    new EntityData(Project.GameDatabase, offlineCharacter.EntityData)
                ]
            };
            entityList = Project.InitializeEntities(list);
            Project.LoadEntityData(list, entityList);
            if (entityList.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one restored entity for player {playerGuid}, got {entityList.Count}."
                );
            }

            entity = entityList[0];
            var componentCharacter = entity.FindComponent<ComponentCharacter>();
            if (componentCharacter == null || componentCharacter.CharacterData.CharacterId != playerGuid)
            {
                throw new InvalidOperationException($"Restored entity does not belong to player {playerGuid}.");
            }

            // 恢复的实体先不加入项目，等连接完成初始世界快照后再挂载。
            // 挂载时玩家数据和实体通过 EntityPackage 原子同步。
            entity.EntityId = 0;
            // 离线记录保留在 _offlineCharacters 中（读取不删除），避免客户端在 ProjectLoaded
            // 之前断开时丢失玩家数据；下次正常退出时会重新覆盖为最新数据。
            characterData = restoredCharacterData;
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

            if (characterDataAdded &&
                restoredCharacterData != null &&
                _characters.Contains(restoredCharacterData))
            {
                RemoveCharacterData(restoredCharacterData);
            }

            throw;
        }
    }

    public void ApplyOnlineCharacterStates(IEnumerable<OnlineCharacterState> states)
    {
        _onlineCharacterStates.Clear();
        foreach (var state in states)
        {
            _onlineCharacterStates[state.CharacterId] = state;
        }
    }

    public void ApplyCharacterList(IEnumerable<CharacterListEntry> players)
    {
        _characterList.Clear();
        foreach (var player in players)
        {
            _characterList[player.CharacterId] = player;
            var activePlayer = _characters.Find(data => data.CharacterId == player.CharacterId);
            if (activePlayer is null)
            {
                continue;
            }

            activePlayer.Name = player.Name;
        }

        foreach (var playerGuid in _onlineCharacterStates.Keys
                     .Where(playerGuid =>
                         !_characterList.TryGetValue(playerGuid, out var player) ||
                         !player.IsOnline
                     )
                     .ToList())
        {
            _onlineCharacterStates.Remove(playerGuid);
        }

        CharacterListChanged?.Invoke();
    }

    public void RefreshCharacterList()
    {
        var entries = new Dictionary<Guid, CharacterListEntry>();
        foreach (var offlineCharacter in _offlineCharacters)
        {
            entries[offlineCharacter.Key] = new CharacterListEntry(
                offlineCharacter.Key,
                new PlayerAddress(offlineCharacter.Value.CharacterData.GetValue<string>("OwnerAddress")),
                offlineCharacter.Value.CharacterData.GetValue("Name", "Player"),
                false
            );
        }

        foreach (var characterData in _characters)
        {
            entries[characterData.CharacterId] = new CharacterListEntry(
                characterData.CharacterId,
                characterData.OwnerAddress,
                characterData.Name,
                true
            );
        }

        _characterList.Clear();
        foreach (var entry in entries)
        {
            _characterList.Add(entry.Key, entry.Value);
        }

        CharacterListChanged?.Invoke();
    }

    public IEnumerable<int> GetOfflineInventoryValues()
    {
        foreach (var (playerGuid, record) in _offlineCharacters)
        {
            if (_characters.Any(player => player.CharacterId == playerGuid &&
                                          player.ComponentCharacter is { } componentCharacter &&
                                          Project.EntityKeys.Contains(componentCharacter.Entity)
                ))
            {
                continue;
            }

            if (record.EntityData is null) { continue; }

            foreach (var value in GetSavedInventoryValues(record.EntityData))
            {
                yield return value;
            }
        }
    }

    internal static IEnumerable<int> GetSavedInventoryValues(ValuesDictionary entityData)
    {
        var overrides = entityData.GetValue("Overrides", new ValuesDictionary());
        foreach (var component in overrides.Values.OfType<ValuesDictionary>())
        {
            var slots = component.GetValue("Slots", new ValuesDictionary());
            foreach (var slot in slots.Values.OfType<ValuesDictionary>())
            {
                var value = slot.GetValue("Contents", 0);
                if (value != 0 && slot.GetValue("Count", 1) > 0)
                {
                    yield return value;
                }
            }
        }
    }

    public IEnumerable<CharacterListEntry> GetOwnedCharacters(PlayerAddress address)
    {
        return _characterList.Values.Where(character => character.OwnerAddress == address);
    }

    private void SetCharacterListEntry(CharacterListEntry player)
    {
        _characterList[player.CharacterId] = player;
        CharacterListChanged?.Invoke();
    }

    public CharacterData? FindCharacterData(Func<CharacterData, bool> f)
    {
        return _characters.FirstOrDefault(f);
    }

    public static bool IsLocalCharacter(Entity e)
    {
        var player = e.FindComponent<ComponentCharacter>();
        return player is { CharacterData.IsLocalCharacter: true };
    }
}

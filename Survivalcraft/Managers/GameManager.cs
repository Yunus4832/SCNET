using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Replication;
using Game.Network.Transport;

namespace Game.Managers;

public static class GameManager
{
    private static SubsystemUpdate? _subsystemUpdate;

    private static double _lastAutosaveTime;

    private static readonly ManualResetEvent _saveCompleted = new(true);

    public static Project? Project { get; private set; }

    /// <summary>Runtime world role; startup intent and network connection stage are separate.</summary>
    public static WorldRole Role => CommonLib.Net.Role;

    public static WorldInfo? WorldInfo { get; private set; }

    public static SessionInfo? WorldSession { get; private set; }

    public static LocalGameReturnTarget? LocalPlayerReturnTarget { get; internal set; }

    public static event Action<Project>? ProjectDisposed;

    public static void LoadProject(WorldInfo worldInfo, ContainerWidget gamesWidget, SessionInfo? session = null)
    {
        if (Role is not (WorldRole.SinglePlayer or WorldRole.Server))
        {
            throw new InvalidOperationException("Select the local world execution role before loading its project.");
        }

        if (CommonLib.Net.IsTransportRunning)
        {
            throw new InvalidOperationException("Stop the existing transport before replacing an authoritative world.");
        }

        DisposeProject();
        WorldSession = session ?? SessionInfoManager.CreateWorldSession(worldInfo);
        try
        {
            if (worldInfo.ProjectFormatVersion != WorldVersions.ProjectFormatVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported project format version \"{worldInfo.ProjectFormatVersion}\". Expected \"{WorldVersions.ProjectFormatVersion}\"."
                );
            }

            var xmlFile = Storage.CombinePaths(worldInfo.DirectoryName, "Project.xml");

            if (!Storage.FileExists(xmlFile))
            {
                throw new FileNotFoundException("Project file not found.", xmlFile);
            }

            XElement projectNode;
            using (var stream = Storage.OpenFile(xmlFile, OpenFileMode.Read))
            {
                projectNode = XmlUtils.LoadXmlFromStream(stream, null, true);
            }

            var valuesDictionary = new ValuesDictionary();
            var valuesDictionary2 = new ValuesDictionary();
            valuesDictionary.SetValue("GameInfo", valuesDictionary2);
            valuesDictionary2.SetValue("WorldDirectoryName", worldInfo.DirectoryName);
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary.SetValue("Views", valuesDictionary3);
            valuesDictionary3.SetValue("GamesWidget", gamesWidget);
            var registryNode = projectNode.Element("BlockRegistry");
            var catalog = CurrentModRuntime.Value!.CreateWorldCatalog(BlockIndexMapping.FromXml(registryNode));
            registryNode?.Remove();
            projectNode.Add(catalog.Registry.Mapping.ToXml());
            // A missing empty registry can wait for the normal save; allocated mod indices must be durable first.
            if (registryNode is not null || catalog.Registry.Mapping.Entries.Count > 0)
            {
                SaveProjectMetadata(worldInfo.DirectoryName, projectNode);
            }

            var projectData = new ProjectData(DatabaseManager.GameDatabase, projectNode, valuesDictionary, true);
            Project = new Project(DatabaseManager.GameDatabase, projectData);
            _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;

            if (Project == null)
            {
                throw new Exception("未能加载Project");
            }

            if (worldInfo.GameModeOverride is { } gameModeOverride)
            {
                var subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
                var persistedGameMode = subsystemGameInfo.WorldSettings.GameMode;
                subsystemGameInfo.ApplyGameModeOverride(gameModeOverride);
                worldInfo.WorldSettings.GameMode = gameModeOverride;
                Log.Information(
                    $"Applied session game mode override: {persistedGameMode} -> {gameModeOverride} (world save remains {persistedGameMode})."
                );
            }

            Project.FindSubsystem<SubsystemGameInfo>(true)!.ApplyRunServerOverride(worldInfo.WorldSettings.RunServer);
            _lastAutosaveTime = Time.RealTime;
            var characters = Project.FindSubsystem<SubsystemCharacters>(true)!;
            foreach (var character in characters.Characters.ToArray())
            {
                characters.MakeCharacterOffline(character.CharacterId, false);
            }

            SetupNetworkHandlers(Project);
            WorldInfo = worldInfo;
            Log.Information(
                "Loaded world, GameMode={0}, StartingPosition={1}, WorldName={2}, VisibilityRange={3}, Resolution={4}",
                worldInfo.WorldSettings.GameMode,
                worldInfo.WorldSettings.StartingPositionMode,
                worldInfo.WorldSettings.Name,
                SettingsManager.Current.VisibilityRange.ToString(),
                SettingsManager.Current.ResolutionMode.ToString()
            );
            GC.Collect();
        }
        catch
        {
            DisposeProject();
            throw;
        }
    }

    public static void LoadProject(
        byte[] messagePackData,
        ContainerWidget gamesWidget,
        BlockIndexMapping mapping
    )
    {
        DisposeProject();
        try
        {
            var valuesDictionary = new ValuesDictionary();
            var valuesDictionary2 = new ValuesDictionary();
            valuesDictionary.SetValue("GameInfo", valuesDictionary2);
            valuesDictionary2.SetValue("WorldDirectoryName", "NetWorld");
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary.SetValue("Views", valuesDictionary3);
            valuesDictionary3.SetValue("GamesWidget", gamesWidget);
            CurrentModRuntime.Value!.CreateWorldCatalog(mapping, true);
            var projectData = new ProjectData(DatabaseManager.GameDatabase, messagePackData, valuesDictionary, true);
            Project = new Project(DatabaseManager.GameDatabase, projectData);
            _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
            _lastAutosaveTime = Time.RealTime;
            SetupNetworkHandlers(Project);
            WorldInfo = new WorldInfo();
            Log.Information("加载NetProject");
            GC.Collect();
        }
        catch
        {
            DisposeProject();
            throw;
        }
    }

    public static void SaveProject(bool waitForCompletion, bool showErrorDialog)
    {
        if (Role == WorldRole.RemoteClient)
        {
            return;
        }

        if (Project == null)
        {
            return;
        }

        if (Project.FindSubsystem<SubsystemGameInfo>(true)!.DirectoryName == "NetWorld")
        {
            return;
        }

        var realTime = Time.RealTime;

        var projectData = Project.Save();
        var blockMapping = CurrentModRuntime.Value!.RequireWorldCatalog().Registry.Mapping;
        _saveCompleted.WaitOne();
        _saveCompleted.Reset();
        var subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        Exception? e;
        Task.Run(
            delegate
            {
                try
                {
                    if (string.IsNullOrEmpty(subsystemGameInfo.DirectoryName))
                    {
                        return;
                    }

                    var projectNode = new XElement("Project");
                    XmlUtils.SetAttributeValue(projectNode, "Version", WorldVersions.ProjectFormatVersion);
                    projectData.Save(projectNode);
                    projectNode.Add(blockMapping.ToXml());
                    Storage.CreateDirectory(subsystemGameInfo.DirectoryName);
                    var projectPath = Storage.CombinePaths(subsystemGameInfo.DirectoryName, "Project.xml");
                    var temporaryPath = Storage.CombinePaths(subsystemGameInfo.DirectoryName, "Project.xml.tmp");
                    using (var stream = Storage.OpenFile(temporaryPath, OpenFileMode.Create))
                    {
                        XmlUtils.SaveXmlToStream(projectNode, stream, null, true);
                    }

                    if (!WorldsManager.TestProjectFile(temporaryPath))
                    {
                        throw new InvalidOperationException("Generated project file is invalid.");
                    }

                    Storage.MoveFile(temporaryPath, projectPath);
                }
                catch (Exception ex)
                {
                    e = ex;
                    if (showErrorDialog)
                    {
                        Dispatcher.Dispatch(
                            delegate
                            {
                                Log.Error(e);
                                if (Role != WorldRole.RemoteClient)
                                {
                                    DialogsManager.ShowDialog(
                                        null,
                                        new MessageDialog(
                                            "保存存档失败",
                                            "Project.xml 未被新的存档文件替换。\n" + e.Message,
                                            LanguageManager.Ok
                                        )
                                    );
                                }
                            }
                        );
                    }
                }
                finally
                {
                    _saveCompleted.Set();
                }
            }
        );

        if (waitForCompletion)
        {
            _saveCompleted.WaitOne();
        }

        var realTime2 = Time.RealTime;
        Log.Verbose($"Saved project, {MathUtils.Round((realTime2 - realTime) * 1000.0)}ms");
    }

    public static void UpdateProject()
    {
        _subsystemUpdate?.Update();
        if (Project is not null && Role != WorldRole.RemoteClient && Time.RealTime - _lastAutosaveTime > 120)
        {
            _lastAutosaveTime = Time.RealTime;
            SaveProject(false, RunMode.Value == RunModeType.Gui);
        }
    }

    private static void SaveProjectMetadata(string directory, XElement node)
    {
        var path = Storage.CombinePaths(directory, "Project.xml");
        var temporary = Storage.CombinePaths(directory, "Project.xml.tmp");
        using (var stream = Storage.OpenFile(temporary, OpenFileMode.Create))
        {
            XmlUtils.SaveXmlToStream(node, stream, null, true);
        }

        if (!WorldsManager.TestProjectFile(temporary))
        {
            throw new InvalidDataException("Generated project metadata is invalid.");
        }

        Storage.MoveFile(temporary, path);
    }

    public static void LeaveLocalPlayer()
    {
        CommonLib.Net.EndAdmission();
        CommonLib.Net.LeaveLocalPlayer();
        SaveProject(true, true);
        LocalPlayerReturnTarget = null;
    }

    public static void CloseWorld()
    {
        if (Project is not null) { SaveProject(true, true); }

        CommonLib.Net.StopImmediate();
        DisposeProject();
    }

    public static void DisposeProject()
    {
        _saveCompleted.WaitOne();
        var project = Project;
        try
        {
            if (project is not null)
            {
                try
                {
                    ProjectDisposed?.Invoke(project);
                }
                finally
                {
                    project.Dispose();
                }
            }
        }
        finally
        {
            Project = null;
            CurrentModRuntime.Value?.ReleaseWorldCatalog();
            _subsystemUpdate = null;
            WorldInfo = null;
            WorldSession = null;
            LocalPlayerReturnTarget = null;
        }

        GC.Collect();
    }

    private static void SetupNetworkHandlers(Project project)
    {
        var netNode = CommonLib.Net;
        foreach (var session in netNode.Clients.Values) { session.SetProject(project); }

        netNode.TurnOnPackageHandle(project);

        if (Role == WorldRole.Server)
        {
            SetupServerNetworkHandlers(project, netNode);
        }

        if (Role == WorldRole.RemoteClient)
        {
            SetupClientNetworkHandlers(project, netNode);
        }
    }

    internal static void ActivateLocalPlayer(Project project, Client session)
    {
        session.SetProject(project);
        if (session.CharacterId == Guid.Empty) { return; }

        var characters = project.FindSubsystem<SubsystemCharacters>(true)!;
        var character = characters.FindCharacterData(data => data.CharacterId == session.CharacterId);
        Entity? restoredEntity = null;
        if (character is null)
        {
            if (!characters.MakeCharacterOnline(session.CharacterId, out character, out restoredEntity))
            {
                throw new InvalidDataException("The local subject's character data is missing.");
            }
        }

        character!.SetMain();
        if (restoredEntity is not null && !restoredEntity.IsAddedToProject) { project.AddEntity(restoredEntity); }

        project.FindSubsystem<SubsystemGameWidgets>(true)!.SelectLocalCharacter(character);
    }

    private static void SetupServerNetworkHandlers(Project project, NetNode net)
    {
        var entityMaps = BuildServerEntityMap(project);
        project.BeforeEntityAdded += (_, arg) => EnsureEntityId(arg.Entity, entityMaps);
        project.EntityRemoved += (_, arg) => { entityMaps.Remove(arg.Entity.EntityId); };
        project.EntityAdded += (_, arg) =>
        {
            if (!EntityPackageBuilder.ShouldSend(arg.Entity) ||
                IsInterestManagedBody(arg.Entity) ||
                arg.Entity.FindComponent<ComponentBlockEntity>() is not null)
            {
                return;
            }

            net.QueuePackage(EntityPackageBuilder.Capture(arg.Entity), PackageAudience.Global);
        };
        project.EntityRemoved += (_, arg) =>
        {
            if (arg.Entity.FindComponent<ComponentBlockEntity>() is not null)
            {
                var interest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
                net.QueuePackage(
                    new EntityRemovedPackage { EntityId = arg.Entity.EntityId },
                    PackageAudience.To(
                        interest.Entities.GetObservers(EntityInterestGroup.BlockEntities, arg.Entity.EntityId).ToArray()
                    )
                );
                interest.Entities.RemoveEntity(EntityInterestGroup.BlockEntities, arg.Entity.EntityId);
                return;
            }

            if (!IsInterestManagedBody(arg.Entity))
            {
                net.QueuePackage(
                    new EntityRemovedPackage { EntityId = arg.Entity.EntityId },
                    PackageAudience.Global
                );
            }
        };
        net.OnClientStateChanged += client => OnServerClientStateChanged(project, net, client);
        net.OnClientTransportConnected += client => SendBootstrap(project, net, client);
        net.OnClientBootstrapApplied += client => SendInitialWorldSnapshot(project, net, client);
        net.OnClientBecameLive += client => CompleteClientJoin(project, client);
    }

    private static void SetupClientNetworkHandlers(Project project, NetNode net)
    {
        net.CurrentConnectionPhase = ConnectionPhase.BootstrapApplied;
        net.QueuePackage(
            new ConnectionPhaseAckPackage(net.ConnectionEpoch, ConnectionPhase.BootstrapApplied),
            PackageAudience.To(net.Server!)
        );
        net.OnClientStateChanged += c =>
        {
            c.SetProject(project);
            if (c.State != ClientState.NotConnected)
            {
                return;
            }

            var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
            var characterData = subsystemCharacters.Characters.Find(pd => pd.CharacterId == c.CharacterId);
            if (characterData is { IsLocalCharacter: false })
            {
                subsystemCharacters.MakeCharacterOffline(characterData.CharacterId);
            }
        };
    }

    private static Dictionary<int, Entity> BuildServerEntityMap(Project project)
    {
        var entityMaps = new Dictionary<int, Entity>();
        foreach (var entity in project.EntityKeys.Where(entity => entity.EntityId != 0))
        {
            EnsureEntityId(entity, entityMaps);
        }

        return entityMaps;
    }

    private static void EnsureEntityId(Entity entity, Dictionary<int, Entity> entityMaps)
    {
        if (entity.EntityId != 0 && entityMaps.TryAdd(entity.EntityId, entity))
        {
            return;
        }

        for (ushort i = 1; i < ushort.MaxValue; i++)
        {
            if (entityMaps.ContainsKey(i))
            {
                continue;
            }

            entity.EntityId = i;
            entityMaps.Add(i, entity);
            return;
        }
    }

    private static bool IsInterestManagedBody(Entity entity)
    {
        var body = entity.FindComponent<ComponentBody>();
        return body is not null;
    }

    private static void OnServerClientStateChanged(Project project, NetNode net, Client client)
    {
        client.SetProject(project);
        net.QueuePackage(
            new ClientStateChangedPackage { ClientId = client.ID, State = client.State },
            PackageAudience.Except(client)
        );
        switch (client.State)
        {
            case ClientState.NotConnected:
                project.FindSubsystem<SubsystemNetworkInterest>(true)!.Entities.RemoveClient(client);
                var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
                subsystemCharacters.MakeCharacterOffline(client.CharacterId);
                net.QueuePackage(CharacterPackageBuilder.Capture(subsystemCharacters), PackageAudience.Global);
                GC.Collect();
                break;
            case ClientState.LoadTerrain:
                GC.Collect();
                if (client.CharacterData is { ComponentCharacter: not null })
                {
                    if (project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode != GameMode.Creative)
                    {
                        client.CharacterData.ComponentCharacter.ComponentHealth.IsInvulnerable = true;
                    }
                }

                break;
            case ClientState.Playing:
                if (client.CharacterData?.ComponentCharacter is not null)
                {
                    if (project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode != GameMode.Creative)
                    {
                        client.CharacterData.ComponentCharacter.ComponentHealth.IsInvulnerable = false;
                    }
                }

                break;
        }
    }

    private static void SendBootstrap(Project project, NetNode net, Client client)
    {
        var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
        try
        {
            if (client.Subject is not { } subject || subject.Ban is not null)
            {
                net.RemoveClient(client, "用户未获准进入此存档");
                return;
            }

            var restored = subsystemCharacters.MakeCharacterOnline(
                client.CharacterId,
                out var characterData,
                out var entity
            );
            if (subject.CharacterId != Guid.Empty && !restored)
            {
                throw new InvalidDataException("The subject's persisted character data is missing.");
            }

            if (restored)
            {
                client.CachePlayerEntity = entity;
                net.QueuePackage(CharacterPackageBuilder.Capture(subsystemCharacters), PackageAudience.Global);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"玩家数据恢复失败，断开连接: guid={client.CharacterId:N}, error={ex.Message}");
            net.RemoveClient(client, "玩家数据加载失败，请稍后重试");
            return;
        }

        byte[]? textureData = null;
        var subsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true)!;
        if (!string.IsNullOrEmpty(subsystemGameInfo.WorldSettings.BlocksTextureName))
        {
            using var s = Storage.OpenFile(
                BlocksTexturesManager.GetFileName(subsystemGameInfo.WorldSettings.BlocksTextureName),
                OpenFileMode.Read
            );
            textureData = StreamUtils.ReadBytes(s);
        }

        var data = CommonLib.GetNowProject(project);
        if (data == null)
        {
            Log.Error($"Failed to get project data for client {client.ID}, disconnecting...");
            client.Peer?.Disconnect();
            return;
        }

        client.ConnectionPhase = ConnectionPhase.BootstrapSent;
        net.QueuePackage(
            WorldSnapshotBuilder.Bootstrap(
                project,
                client.ConnectionEpoch,
                net.Clients.Values,
                textureData,
                data,
                CurrentModRuntime.Value!.RequireWorldCatalog().Registry.Mapping
            ),
            PackageAudience.To(client)
        );
    }

    private static void SendInitialWorldSnapshot(Project project, NetNode net, Client client)
    {
        var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
        var subsystemNetworkInterest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        var sendList = project.EntityKeys
            .Where(EntityPackageBuilder.ShouldSend)
            .Where(entity => subsystemNetworkInterest.ShouldIncludeInInitialSnapshot(client, entity))
            .ToList();
        subsystemNetworkInterest.SeedInitialSnapshot(client, sendList);
        client.ConnectionPhase = ConnectionPhase.WorldSnapshotSent;
        net.QueuePackage(
            WorldSnapshotBuilder.Initial(
                client.ConnectionEpoch,
                project,
                net.Clients.Values,
                subsystemCharacters.Characters,
                sendList
            ),
            PackageAudience.To(client)
        );
    }

    private static void CompleteClientJoin(Project project, Client client)
    {
        if (client.CachePlayerEntity == null)
        {
            return;
        }

        project.AddEntity(client.CachePlayerEntity);
        client.CachePlayerEntity = null;
        // A restored entity may already be attached from world loading. AddEntity then emits
        // no event, so refresh the connection-dependent player list after the join completes.
        project.FindSubsystem<SubsystemCharacters>(true)!.UpdateComponentCharacters();
    }
}

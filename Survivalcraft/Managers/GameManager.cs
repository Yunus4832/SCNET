using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Managers;

public static class GameManager
{
    private static SubsystemUpdate? _subsystemUpdate;

    private static readonly ManualResetEvent _saveCompleted = new(true);

    public static Project? Project { get; private set; }

    public static WorldInfo? WorldInfo { get; private set; }

    public static event Action<Project>? ProjectDisposed;

    public static void LoadProject(WorldInfo worldInfo, ContainerWidget gamesWidget, bool useNetProj = true)
    {
        DisposeProject();
        if (worldInfo.ProjectFormatVersion != WorldVersions.ProjectFormatVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported project format version \"{worldInfo.ProjectFormatVersion}\". Expected \"{WorldVersions.ProjectFormatVersion}\".");
        }

        var xmlFile = Storage.CombinePaths(worldInfo.DirectoryName, "Project.xml");

        if (!Storage.FileExists(xmlFile))
        {
            throw new FileNotFoundException("Project file not found.", xmlFile);
        }

        using (var stream = Storage.OpenFile(xmlFile, OpenFileMode.Read))
        {
            var valuesDictionary = new ValuesDictionary();
            var valuesDictionary2 = new ValuesDictionary();
            valuesDictionary.SetValue("GameInfo", valuesDictionary2);
            valuesDictionary2.SetValue("WorldDirectoryName", worldInfo.DirectoryName);
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary.SetValue("Views", valuesDictionary3);
            valuesDictionary3.SetValue("GamesWidget", gamesWidget);
            var projectNode = XmlUtils.LoadXmlFromStream(stream, null, true);
            var projectData = new ProjectData(DatabaseManager.GameDatabase, projectNode, valuesDictionary, true);
            Project = new Project(DatabaseManager.GameDatabase, projectData);
            _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
        }

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
                $"Applied session game mode override: {persistedGameMode} -> {gameModeOverride} (world save remains {persistedGameMode}).");
        }

        SetupNetworkHandlers(Project);
        WorldInfo = worldInfo;
        Log.Information(
            "Loaded world, GameMode={0}, StartingPosition={1}, WorldName={2}, VisibilityRange={3}, Resolution={4}",
            worldInfo.WorldSettings.GameMode, worldInfo.WorldSettings.StartingPositionMode,
            worldInfo.WorldSettings.Name, SettingsManager.Current.VisibilityRange.ToString(),
            SettingsManager.Current.ResolutionMode.ToString());
        GC.Collect();
    }

    public static void LoadProject(byte[] messagePackData, ContainerWidget gamesWidget, bool useNetProj = true)
    {
        DisposeProject();
        var valuesDictionary = new ValuesDictionary();
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("GameInfo", valuesDictionary2);
        valuesDictionary2.SetValue("WorldDirectoryName", "NetWorld");
        var valuesDictionary3 = new ValuesDictionary();
        valuesDictionary.SetValue("Views", valuesDictionary3);
        valuesDictionary3.SetValue("GamesWidget", gamesWidget);
        var projectData = new ProjectData(DatabaseManager.GameDatabase, messagePackData, valuesDictionary, true);
        Project = new Project(DatabaseManager.GameDatabase, projectData);
        _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
        SetupNetworkHandlers(Project);
        WorldInfo = new WorldInfo();
        Log.Information("加载NetProject");
        GC.Collect();
    }

    public static void SaveProject(bool waitForCompletion, bool showErrorDialog)
    {
        if (CommonLib.WorkType == WorkType.Client)
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
        _saveCompleted.WaitOne();
        _saveCompleted.Reset();
        var subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        Exception? e;
        Task.Run(delegate
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
                    Dispatcher.Dispatch(delegate
                    {
                        Log.Error(e);
                        if (CommonLib.WorkType != WorkType.Client)
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
                    });
                }
            }
            finally
            {
                _saveCompleted.Set();
            }
        });

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
    }

    public static void DisposeProject()
    {
        if (Project is not null)
        {
            ProjectDisposed?.Invoke(Project);
            Project.Dispose();
            Project = null;
        }

        _subsystemUpdate = null;
        WorldInfo = null;
        GC.Collect();
    }

    private static void SetupNetworkHandlers(Project project)
    {
        var netNode = CommonLib.Net;
        netNode.TurnOnPackageHandle(project);

        if (CommonLib.WorkType == WorkType.Server)
        {
            SetupServerNetworkHandlers(project, netNode);
            return;
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            SetupClientNetworkHandlers(project, netNode);
        }
    }

    private static void SetupServerNetworkHandlers(Project project, NetNode net)
    {
        var entityMaps = BuildServerEntityMap(project);
        project.BeforeEntityAdded += (_, arg) => EnsureEntityId(arg.Entity, entityMaps);
        project.EntityRemoved += (_, arg) => { entityMaps.Remove(arg.Entity.EntityId); };
        project.EntityAdded += (_, arg) =>
        {
            if (!ShouldSendEntityToClients(arg.Entity))
            {
                return;
            }

            var componentPlayer = arg.Entity.FindComponent<ComponentPlayer>();
            net.QueuePackage(componentPlayer?.PlayerData.Client != null
                ? new PlayerJoinedPackage(project, componentPlayer.PlayerData, arg.Entity)
                : new EntityPackage(arg.Entity));
        };
        project.EntityRemoved += (_, arg) => { net.QueuePackage(new EntityPackage(arg.Entity.EntityId)); };
        net.OnClientStateChanged += client => OnServerClientStateChanged(project, net, client);
        net.OnClientTransportConnected += client => SendBootstrap(project, net, client);
        net.OnClientBootstrapApplied += client => SendInitialWorldSnapshot(project, net, client);
        net.OnClientBecameLive += client => CompleteClientJoin(project, client);
    }

    private static void SetupClientNetworkHandlers(Project project, NetNode net)
    {
        net.CurrentConnectionPhase = ConnectionPhase.BootstrapApplied;
        net.QueuePackage(new ConnectionPhaseAckPackage(net.ConnectionEpoch, ConnectionPhase.BootstrapApplied));
        net.OnClientStateChanged += c =>
        {
            c.SetProject(project);
            if (c.State != ClientState.NotConnected)
            {
                return;
            }

            var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
            var playerData = subsystemPlayers.PlayersData.Find(pd => pd.PlayerGUID == c.GUID);
            if (playerData is { IsMainPlayer: false })
            {
                subsystemPlayers.MakePlayerOffline(playerData.PlayerGUID);
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

    private static bool ShouldSendEntityToClients(Entity entity)
    {
        if (RunMode.Value is RunModeType.Gui)
        {
            return true;
        }

        var componentPlayer = entity.FindComponent<ComponentPlayer>();
        return componentPlayer is null || componentPlayer.PlayerData.Client is not null;
    }

    private static void OnServerClientStateChanged(Project project, NetNode net, Client client)
    {
        client.SetProject(project);
        net.QueuePackage(new ClientPackage(client.ID, client.State) { Except = client });
        switch (client.State)
        {
            case ClientState.NotConnected:
                var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
                subsystemPlayers.MakePlayerOffline(client.GUID);
                net.QueuePackage(new PlayerListPackage(subsystemPlayers));
                GC.Collect();
                break;
            case ClientState.LoadTerrain:
                GC.Collect();
                if (client.PlayerData is { ComponentPlayer: not null })
                {
                    if (project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode != GameMode.Creative)
                    {
                        client.PlayerData.ComponentPlayer.ComponentHealth.IsInvulnerable = true;
                    }
                }

                break;
            case ClientState.Playing:
                if (client.PlayerData.ComponentPlayer != null)
                {
                    if (project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode != GameMode.Creative)
                    {
                        client.PlayerData.ComponentPlayer.ComponentHealth.IsInvulnerable = false;
                    }
                }

                var blacklist = project.FindSubsystem<SubsystemPlayers>(true)!;
                if (blacklist.BlackPlayerGuidList.ContainsKey(client.GUID.ToString()))
                {
                    net.RemoveClientImmediate(client, "你被禁止加入该服务器");
                }

                break;
        }
    }

    private static void SendBootstrap(Project project, NetNode net, Client client)
    {
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        try
        {
            if (subsystemPlayers.MakePlayerOnline(client.GUID, out var playerData, out var entity))
            {
                client.CachePlayerEntity = entity!;
                net.QueuePackage(new PlayerListPackage(subsystemPlayers));
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"玩家数据恢复失败，断开连接: guid={client.GUID:N}, error={ex.Message}");
            net.RemoveClient(client, "玩家数据加载失败，请稍后重试");
            return;
        }

        byte[]? textureData = null;
        var subsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true)!;
        if (!string.IsNullOrEmpty(subsystemGameInfo.WorldSettings.BlocksTextureName))
        {
            using var s = Storage.OpenFile(
                BlocksTexturesManager.GetFileName(subsystemGameInfo.WorldSettings.BlocksTextureName),
                OpenFileMode.Read);
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
        net.QueuePackage(new BootstrapPackage(client.ConnectionEpoch, net.Clients.Values, textureData, data)
        { To = client });
    }

    private static void SendInitialWorldSnapshot(Project project, NetNode net, Client client)
    {
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        var sendList = project.EntityKeys.Where(ShouldSendEntityToClients).ToList();
        client.ConnectionPhase = ConnectionPhase.WorldSnapshotSent;
        net.QueuePackage(new InitialWorldSnapshotPackage(client.ConnectionEpoch, project, net.Clients.Values,
            subsystemPlayers.PlayersData, sendList)
        { To = client });
    }

    private static void CompleteClientJoin(Project project, Client client)
    {
        if (client.CachePlayerEntity == null)
        {
            return;
        }

        project.AddEntity(client.CachePlayerEntity);
        client.CachePlayerEntity = null;
    }
}

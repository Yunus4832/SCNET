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
        WorldsManager.RepairWorldIfNeeded(worldInfo.DirectoryName);
        VersionsManager.UpgradeWorld(worldInfo.DirectoryName);
        var xmlFile = Storage.CombinePaths(worldInfo.DirectoryName, "Project.xml");
        var mpkFile = Storage.CombinePaths(worldInfo.DirectoryName, "Project.mpk");
        var jsonFile = Storage.CombinePaths(worldInfo.DirectoryName, "Project.json");

        if (Storage.FileExists(xmlFile))
        {
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

            Storage.DeleteFile(xmlFile);
        }
        else if (Storage.FileExists(mpkFile))
        {
            using (var stream = Storage.OpenFile(mpkFile, OpenFileMode.Read))
            {
                var data = new byte[stream.Length];
                stream.ReadExactly(data, 0, data.Length);
                var rootNode = new ValuesDictionary();
                rootNode.ApplyOverridesUseMessagePack(data);
                var valuesDictionary = new ValuesDictionary();
                var valuesDictionary2 = new ValuesDictionary();
                valuesDictionary.SetValue("GameInfo", valuesDictionary2);
                valuesDictionary2.SetValue("WorldDirectoryName", worldInfo.DirectoryName);
                var valuesDictionary3 = new ValuesDictionary();
                valuesDictionary.SetValue("Views", valuesDictionary3);
                valuesDictionary3.SetValue("GamesWidget", gamesWidget);
                var projectData = new ProjectData(DatabaseManager.GameDatabase, data, valuesDictionary, true);
                Project = new Project(DatabaseManager.GameDatabase, projectData);
                _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
            }

            Storage.DeleteFile(mpkFile);
        }
        else if (Storage.FileExists(jsonFile))
        {
            using var stream = Storage.OpenFile(jsonFile, OpenFileMode.Read);
            var reader = new StreamReader(stream);
            var jsonText = reader.ReadToEnd();
            reader.Dispose();
            var rootNode = new ValuesDictionary();
            rootNode.ApplyOverridesUseJson(jsonText, out var data);
            var valuesDictionary = new ValuesDictionary();
            var valuesDictionary2 = new ValuesDictionary();
            valuesDictionary.SetValue("GameInfo", valuesDictionary2);
            valuesDictionary2.SetValue("WorldDirectoryName", worldInfo.DirectoryName);
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary.SetValue("Views", valuesDictionary3);
            valuesDictionary3.SetValue("GamesWidget", gamesWidget);
            var projectData = new ProjectData(DatabaseManager.GameDatabase, data, valuesDictionary, true);
            Project = new Project(DatabaseManager.GameDatabase, projectData);
            _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
        }

        if (Project == null)
        {
            throw new Exception("未能加载Project");
        }

        SetupNetworkHandlers(Project);
        WorldInfo = worldInfo;
        Log.Information(
            "Loaded world, GameMode={0}, StartingPosition={1}, WorldName={2}, VisibilityRange={3}, Resolution={4}",
            worldInfo.WorldSettings.GameMode, worldInfo.WorldSettings.StartingPositionMode,
            worldInfo.WorldSettings.Name, SettingsManager.VisibilityRange.ToString(),
            SettingsManager.ResolutionMode.ToString());
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

                var rootNode = new ValuesDictionary();
                rootNode.SetValue("Version", VersionsManager.SerializationVersion);
                projectData.Save(rootNode);
                Storage.CreateDirectory(subsystemGameInfo.DirectoryName);
                var path1 = Storage.CombinePaths(subsystemGameInfo.DirectoryName, "Project.json");
                // 上次保存
                var path2 = Storage.CombinePaths(subsystemGameInfo.DirectoryName, "Project.temp");
                // 备份文件
                var path3 = Storage.CombinePaths(subsystemGameInfo.DirectoryName, "Project.bak");
                if (Storage.FileExists(path1))
                {
                    Storage.CopyFile(path1, path2);
                }

                using (var stream = Storage.OpenFile(path1, OpenFileMode.Create))
                {
                    var streamWriter = new StreamWriter(stream);
                    streamWriter.Write(rootNode.ToJsonText());
                    streamWriter.Dispose();
                }

                if (Storage.FileExists(path1))
                {
                    Storage.CopyFile(path1, path3);
                }
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
                                    "请及时做存档还原操作，存档备份文件为\nProject.bak和Project.temp\n" + e.Message,
                                    "OK"
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
            if (ShouldSendEntityToClients(arg.Entity))
            {
                net.QueuePackage(new EntityPackage(arg.Entity));
            }
        };
        project.EntityRemoved += (_, arg) => { net.QueuePackage(new EntityPackage(arg.Entity.EntityId)); };
        net.OnClientStateChanged += client => OnServerClientStateChanged(project, net, client);
    }

    private static void SetupClientNetworkHandlers(Project project, NetNode net)
    {
        net.QueuePackage(new ClientPackage(net.Self!.ID, ClientState.ProjectLoaded));
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
            case ClientState.Connected:
                HandleServerClientConnected(project, net, client);
                break;
            case ClientState.NotConnected:
                project.FindSubsystem<SubsystemPlayers>(true)!.MakePlayerOffline(client.GUID);
                GC.Collect();
                break;
            case ClientState.ProjectLoaded:
                HandleServerClientProjectLoaded(project, net, client);
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

    private static void HandleServerClientConnected(Project project, NetNode net, Client client)
    {
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        if (subsystemPlayers.MakePlayerOnline(client.GUID, out var playerData, out var entity))
        {
            client.CachePlayerEntity = entity!;
            net.QueuePackage(new PlayerDataPackage(playerData!, PlayerDataPackage.DataType.AddPlayer)
                { Except = client });
        }

        byte[]? textureData = null;
        var subsystemGameInfo = project.FindSubsystem<SubsystemGameInfo>(true)!;
        if (!string.IsNullOrEmpty(subsystemGameInfo.WorldSettings.BlocksTextureName))
        {
            using var s = Storage.OpenFile(
                BlocksTexturesManager.GetFileName(subsystemGameInfo.WorldSettings.BlocksTextureName),
                OpenFileMode.Read);
            textureData = ModsManager.StreamToBytes(s);
        }

        var data = CommonLib.GetNowProject(project);
        if (data == null)
        {
            Log.Error($"Failed to get project data for client {client.ID}, disconnecting...");
            client.Peer?.Disconnect();
            return;
        }

        net.QueuePackage(new ProjectPackage(textureData, data) { To = client });
    }

    private static void HandleServerClientProjectLoaded(Project project, NetNode net, Client client)
    {
        var sendList = project.EntityKeys.Where(ShouldSendEntityToClients).ToList();
        net.QueuePackage(new EntityPackage(sendList) { To = client });
        if (client.CachePlayerEntity == null)
        {
            return;
        }

        project.AddEntity(client.CachePlayerEntity);
        client.CachePlayerEntity = null;
    }
}

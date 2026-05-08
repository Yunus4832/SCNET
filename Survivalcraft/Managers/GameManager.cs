using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;
using Game.NetWork;

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
                Project = useNetProj
                    ? new ProjectNet(DatabaseManager.GameDatabase, projectData)
                    : new Project(DatabaseManager.GameDatabase, projectData);
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
                Project = useNetProj
                    ? new ProjectNet(DatabaseManager.GameDatabase, projectData)
                    : new Project(DatabaseManager.GameDatabase, projectData);
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
            Project = useNetProj
                ? new ProjectNet(DatabaseManager.GameDatabase, projectData)
                : new Project(DatabaseManager.GameDatabase, projectData);
            _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
        }

        if (Project == null)
        {
            throw new Exception("未能加载Project");
        }

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
        Project = useNetProj
            ? new ProjectNet(DatabaseManager.GameDatabase, projectData)
            : new Project(DatabaseManager.GameDatabase, projectData);
        _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
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

        var realTime = Time.RealTime;
        if (Project == null)
        {
            return;
        }

        var projectData = Project.Save();
        _saveCompleted.WaitOne();
        _saveCompleted.Reset();
        var subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        Exception? e;
        Task.Run(delegate
        {
            try
            {
                if (subsystemGameInfo.DirectoryName == "NetWorld")
                {
                    Log.Error("保存失败，存档路径有误，Path：" + subsystemGameInfo.DirectoryName);
                    return;
                }

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
}

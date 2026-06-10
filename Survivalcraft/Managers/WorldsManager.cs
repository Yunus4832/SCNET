using System.Globalization;
using System.Xml.Linq;

using Engine.Serialization;

using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

using Game.TerrainSerializers;

namespace Game.Managers;

public static class WorldsManager
{
    public const int MaxAllowedWorlds = 30;

    private static readonly List<WorldInfo> _worldInfos = [];

    private static readonly string _worldsDirectoryName = GamePaths.Worlds;

    private static bool _loaded;

    public static ReadOnlyList<string> NewWorldNames { get; private set; }

    public static ReadOnlyList<WorldInfo> WorldInfos => new(_worldInfos);

    public static event Action<string>? WorldDeleted;

    public static void Initialize()
    {
        if (_loaded)
        {
            return;
        }

        Storage.CreateDirectory(_worldsDirectoryName);
        var text = ContentManager.Get<string>("NewWorldNames");
        NewWorldNames = new ReadOnlyList<string>(text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries));
        _loaded = true;
    }

    public static string ImportWorld(Stream sourceStream)
    {
        var unusedWorldDirectoryName = GetUnusedWorldDirectoryName();
        Storage.CreateDirectory(unusedWorldDirectoryName);
        UnpackWorld(unusedWorldDirectoryName, sourceStream, true);
        //if (!TestXmlFile(Storage.CombinePaths(unusedWorldDirectoryName, "Project.xml"), "Project"))
        //{
        //    try
        //    {
        //        DeleteWorld(unusedWorldDirectoryName);
        //    }
        //    catch
        //    {
        //    }
        //    throw new InvalidOperationException("Cannot import world because it does not contain valid world data.");
        //}
        return unusedWorldDirectoryName;
    }

    public static void ExportWorld(string directoryName, Stream targetStream)
    {
        PackWorld(directoryName, targetStream, null, true);
    }

    public static void DeleteWorld(string directoryName)
    {
        if (Storage.DirectoryExists(directoryName))
        {
            DeleteWorldContents(directoryName, null);
            Storage.DeleteDirectory(directoryName);
        }

        WorldDeleted?.Invoke(directoryName);
    }

    public static void RepairWorldIfNeeded(string directoryName)
    {
        try
        {
            var xmlFile = Storage.CombinePaths(directoryName, "Project.xml");
            if (Storage.FileExists(xmlFile) && !TestXmlFile(xmlFile, "Project"))
            {
                Log.Warning(
                    $"Project file at \"{xmlFile}\" is corrupt or nonexistent. Will try copying data from the backup file. If that fails, will try making a recovery project file.");
                var text2 = Storage.CombinePaths(directoryName, "Project.bak");
                if (TestXmlFile(text2, "Project"))
                {
                    Storage.CopyFile(text2, xmlFile);
                }
                else
                {
                    var path = Storage.CombinePaths(directoryName, "Chunks.dat");
                    if (!Storage.FileExists(path))
                    {
                        throw new InvalidOperationException(
                            "Recovery project file could not be generated because chunks file does not exist.");
                    }

                    var xElement = ContentManager.Get<XElement>("RecoveryProject");
                    using (var stream = Storage.OpenFile(path, OpenFileMode.Read))
                    {
                        TerrainSerializer14.ReadTocEntry(stream, out var cx, out var cz, out _);
                        var vector = new Vector3(16 * cx, 255f, 16 * cz);
                        xElement.Element("Subsystems")?.Element("Values")?.Element("Value")?
                            .Attribute("Value")?
                            .SetValue(HumanReadableConverter.ConvertToString(vector));
                    }

                    using (var stream2 = Storage.OpenFile(xmlFile, OpenFileMode.Create))
                    {
                        XmlUtils.SaveXmlToStream(xElement, stream2, null, true);
                    }
                }
            }
        }
        catch (Exception)
        {
            throw new InvalidOperationException("The world files are corrupt and could not be repaired.");
        }
    }

    public static void MakeQuickWorldBackup(string directoryName)
    {
        var mpkFile = Storage.CombinePaths(directoryName, "Project.json");
        var backUpDir = Storage.CombinePaths(directoryName, "backup");
        if (!Storage.DirectoryExists(backUpDir))
        {
            Storage.CreateDirectory(backUpDir);
        }

        if (!Storage.FileExists(mpkFile))
        {
            return;
        }

        var destinationPath = Storage.CombinePaths(backUpDir,
            $"[{DateTime.Now.ToString("yyyyMMdd HH:mm:ss").Replace(":", "-").Replace(" ", "-")}]Project.json");
        Storage.CopyFile(mpkFile, destinationPath);
    }

    public static void MakeQuickWorldBackup()
    {
        Task.Run(delegate
        {
            if (GameManager.Project is null)
            {
                return;
            }

            var subsystemGameInfo = GameManager.Project.FindSubsystem<SubsystemGameInfo>();
            if (subsystemGameInfo == null)
            {
                return;
            }

            var path = subsystemGameInfo.DirectoryName;
            var name = subsystemGameInfo.WorldSettings.Name;
            var backUpDir = Storage.CombinePaths(GamePaths.External, "WorldsBackup", name);
            if (!Storage.DirectoryExists(backUpDir))
            {
                Storage.CreateDirectory(backUpDir);
            }

            var backupFile = Storage.CombinePaths(
                backUpDir,
                DateTime.Now
                    .ToString(CultureInfo.InvariantCulture)
                    .Replace("/", "-")
                    .Replace(":", "-")
                    .Replace(" ", "-") + ".zip"
            );
            using var targetStream = Storage.OpenFile(backupFile, OpenFileMode.Create);
            ExportWorld(path, targetStream);
        });
    }

    public static bool SnapshotExists(string directoryName, string snapshotName)
    {
        return Storage.FileExists(MakeSnapshotFilename(directoryName, snapshotName));
    }

    private static void TakeWorldSnapshot(string directoryName, string snapshotName)
    {
        using var targetStream =
            Storage.OpenFile(MakeSnapshotFilename(directoryName, snapshotName), OpenFileMode.Create);
        PackWorld(directoryName, targetStream, fn => Path.GetExtension(fn).ToLower() != ".snapshot", false);
    }

    public static void RestoreWorldFromSnapshot(string directoryName, string snapshotName)
    {
        if (!SnapshotExists(directoryName, snapshotName))
        {
            return;
        }

        DeleteWorldContents(directoryName, fn => Storage.GetExtension(fn).ToLower() != ".snapshot");
        using var sourceStream =
            Storage.OpenFile(MakeSnapshotFilename(directoryName, snapshotName), OpenFileMode.Read);
        UnpackWorld(directoryName, sourceStream, false);
    }

    private static void DeleteWorldSnapshot(string directoryName, string snapshotName)
    {
        var path = MakeSnapshotFilename(directoryName, snapshotName);
        if (Storage.FileExists(path))
        {
            Storage.DeleteFile(path);
        }
    }

    public static void UpdateWorldsList()
    {
        _worldInfos.Clear();
        foreach (var item in Storage.ListDirectoryNames(_worldsDirectoryName))
        {
            var worldInfo = GetWorldInfo(Storage.CombinePaths(_worldsDirectoryName, item));
            if (worldInfo != null)
            {
                _worldInfos.Add(worldInfo);
            }
        }
    }

    public static bool ValidateWorldName(string name)
    {
        return !name.Contains('\\') && name.Length <= 128;
    }

    public static WorldInfo? GetWorldInfo(string directoryName)
    {
        var list = new List<string>();
        RecursiveEnumerateDirectory(directoryName, list, [], null);
        if (list.Count <= 0)
        {
            return null;
        }

        var worldInfo = new WorldInfo
        {
            DirectoryName = directoryName,
            LastSaveTime = DateTime.MinValue
        };
        foreach (var item in list)
        {
            var fileLastWriteTime = Storage.GetFileLastWriteTime(item);
            if (fileLastWriteTime > worldInfo.LastSaveTime)
            {
                worldInfo.LastSaveTime = fileLastWriteTime;
            }

            try
            {
                worldInfo.Size += Storage.GetFileSize(item);
            }
            catch (Exception e2)
            {
                Log.Error(ExceptionManager.MakeFullErrorMessage($"Error getting size of file \"{item}\".", e2));
            }
        }

        var xmlFile = Storage.CombinePaths(directoryName, "Project.xml");
        var mpkFile = Storage.CombinePaths(directoryName, "Project.mpk");
        var jsonFile = Storage.CombinePaths(directoryName, "Project.json");
        try
        {
            if (Storage.FileExists(xmlFile))
            {
                using var stream = Storage.OpenFile(xmlFile, OpenFileMode.Read);
                var xElement = XmlUtils.LoadXmlFromStream(stream, null, true);
                worldInfo.SerializationVersion = XmlUtils.GetAttributeValue(xElement, "Version", "1.0");
                VersionsManager.UpgradeProjectXml(xElement);
                var gameInfoNode = GetGameInfoNode(xElement);
                var valuesDictionary = new ValuesDictionary();
                valuesDictionary.ApplyOverrides(gameInfoNode);
                worldInfo.WorldSettings.Load(valuesDictionary);
                foreach (var item2 in (from e in GetPlayersNode(xElement).Elements()
                             where XmlUtils.GetAttributeValue<string>(e, "Name") == "Players"
                             select e).First().Elements())
                {
                    var playerInfo = new PlayerInfo();
                    worldInfo.PlayerInfos.Add(playerInfo);
                    var xElement2 = (from e in item2.Elements()
                        where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "CharacterSkinName"
                        select e).FirstOrDefault();
                    if (xElement2 != null)
                    {
                        playerInfo.CharacterSkinName =
                            XmlUtils.GetAttributeValue(xElement2, "Value", string.Empty);
                    }
                }

                return worldInfo;
            }

            if (Storage.FileExists(mpkFile))
            {
                using var stream = Storage.OpenFile(mpkFile, OpenFileMode.Read);
                var data = new byte[stream.Length];
                stream.ReadExactly(data, 0, data.Length);
                var rootNode = new ValuesDictionary();
                rootNode.ApplyOverridesUseMessagePack(data);
                worldInfo.SerializationVersion = rootNode.GetValue("Version", "1.0");
                var gameInfoNode = GetGameInfoNode(rootNode);
                worldInfo.WorldSettings.Load(gameInfoNode);
                return worldInfo;
            }

            if (!Storage.FileExists(jsonFile))
            {
                return worldInfo;
            }

            {
                using var stream = Storage.OpenFile(jsonFile, OpenFileMode.Read);
                var reader = new StreamReader(stream);
                var jsonText = reader.ReadToEnd();
                reader.Dispose();
                var rootNode = new ValuesDictionary();
                rootNode.ApplyOverridesUseJson(jsonText, out _);
                worldInfo.SerializationVersion = rootNode.GetValue("Version", "1.0");
                var gameInfoNode = GetGameInfoNode(rootNode);
                worldInfo.WorldSettings.Load(gameInfoNode);
                return worldInfo;
            }
        }
        catch (Exception e3)
        {
            Log.Error(ExceptionManager.MakeFullErrorMessage(
                $"Error getting data from project file \"{xmlFile}\" or \"{jsonFile}\".", e3));
            return worldInfo;
        }
    }

    public static WorldInfo CreateWorld(WorldSettings worldSettings, string customWorldDirectoryName = "")
    {
        string unusedWorldDirectoryName;
        if (string.IsNullOrEmpty(customWorldDirectoryName))
        {
            unusedWorldDirectoryName = GetUnusedWorldDirectoryName();
        }
        else
        {
            if (Storage.DirectoryExists(customWorldDirectoryName))
            {
                throw new InvalidOperationException($"World directory name: \"{worldSettings.Name}\" has being used.");
            }

            unusedWorldDirectoryName = customWorldDirectoryName;
        }

        Storage.CreateDirectory(unusedWorldDirectoryName);
        if (!ValidateWorldName(worldSettings.Name))
        {
            throw new InvalidOperationException($"World name \"{worldSettings.Name}\" is invalid.");
        }

        int num;
        if (string.IsNullOrEmpty(worldSettings.Seed))
        {
            num = (int)(long)(Time.RealTime * 1000.0);
        }
        else if (worldSettings.Seed == "0")
        {
            num = 0;
        }
        else
        {
            if (int.TryParse(worldSettings.Seed, out num))
            {
                // 输入是一个合法的整数字符串，直接使用
            }
            else
            {
                // 输入是字符串，使用现有的逐字符计算逻辑
                num = 0;
                var num2 = 1;
                foreach (var c in worldSettings.Seed)
                {
                    num += c * num2;
                    num2 += 29;
                }
            }
        }

        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary2.SetValue("Players", new ValuesDictionary());
        var databaseObject = DatabaseManager.GameDatabase.Database
            .FindDatabaseObject("GameProject", DatabaseManager.GameDatabase.ProjectTemplateType, true)!;
        var rootNode = new ValuesDictionary();
        var subsystems = new ValuesDictionary();
        var gameInfoValues = new ValuesDictionary();
        rootNode.SetValue("Guid", databaseObject.Guid);
        rootNode.SetValue("Name", "GameProject");
        rootNode.SetValue("Version", VersionsManager.SerializationVersion);
        rootNode.SetValue("Subsystems", subsystems);
        subsystems.SetValue("GameInfo", gameInfoValues);
        worldSettings.Save(gameInfoValues, false);
        gameInfoValues.SetValue("WorldDirectoryName", unusedWorldDirectoryName);
        gameInfoValues.SetValue("WorldSeed", num);
        using (var stream = Storage.OpenFile(Storage.CombinePaths(unusedWorldDirectoryName, "Project.json"),
                   OpenFileMode.Create))
        {
            var streamWriter = new StreamWriter(stream);
            streamWriter.Write(rootNode.ToJsonText());
            streamWriter.Dispose();
        }

        return GetWorldInfo(unusedWorldDirectoryName)
               ?? throw new ArgumentException("Create world failed");
    }

    public static void ChangeWorld(string directoryName, WorldSettings worldSettings)
    {
        var jsonFile = Storage.CombinePaths(directoryName, "Project.json");
        if (!Storage.FileExists(jsonFile))
        {
            return;
        }

        var rootNode = new ValuesDictionary();
        GameMode value;
        using (var stream = Storage.OpenFile(jsonFile, OpenFileMode.Read))
        {
            var reader = new StreamReader(stream);
            var jsonText = reader.ReadToEnd();
            reader.Dispose();
            rootNode.ApplyOverridesUseJson(jsonText, out _);
            var gameInfoNode = GetGameInfoNode(rootNode);
            value = gameInfoNode.GetValue<GameMode>("GameMode");
            worldSettings.Save(gameInfoNode, true);
        }

        using (var stream = Storage.OpenFile(jsonFile, OpenFileMode.Create))
        {
            var streamWriter = new StreamWriter(stream);
            streamWriter.Write(rootNode.ToJsonText());
            streamWriter.Dispose();
        }

        if (worldSettings.GameMode == value)
        {
            return;
        }

        if (worldSettings.GameMode == GameMode.Adventure)
        {
            TakeWorldSnapshot(directoryName, "AdventureRestart");
        }
        else
        {
            DeleteWorldSnapshot(directoryName, "AdventureRestart");
        }
    }

    private static string GetUnusedWorldDirectoryName()
    {
        var text = Storage.CombinePaths(_worldsDirectoryName, "World");
        for (var i = 0; i < 1000; i++)
        {
            var arg = Storage.CombinePaths(Storage.GetDirectoryName(text), Storage.GetFileNameWithoutExtension(text));
            var extension = Storage.GetExtension(text);
            var text2 = $"{arg}{(i > 0 ? i.ToString() : string.Empty)}{extension}";
            if (!Storage.DirectoryExists(text2) && !Storage.FileExists(text2))
            {
                return text2;
            }
        }

        throw new InvalidOperationException($"Out of filenames for root \"{text}\".");
    }

    private static void RecursiveEnumerateDirectory(
        string directoryName,
        List<string> files,
        List<string> directories,
        Func<string, bool>? filesFilter
    )
    {
        try
        {
            foreach (var item in Storage.ListDirectoryNames(directoryName))
            {
                var text = Storage.CombinePaths(directoryName, item);
                RecursiveEnumerateDirectory(text, files, directories, filesFilter);
                directories.Add(text);
            }

            foreach (var item2 in Storage.ListFileNames(directoryName))
            {
                var text2 = Storage.CombinePaths(directoryName, item2);
                if (filesFilter == null || filesFilter(text2))
                {
                    files.Add(text2);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error enumerating files/directories. Reason: {ex.Message}");
        }
    }

    private static XElement GetGameInfoNode(XElement projectNode)
    {
        var xElement = (from n in projectNode.Element("Subsystems")?.Elements("Values")
            where XmlUtils.GetAttributeValue(n, "Name", string.Empty) == "GameInfo"
            select n).FirstOrDefault();
        return xElement ?? throw new InvalidOperationException("GameInfo node not found in project.");
    }

    private static ValuesDictionary GetGameInfoNode(ValuesDictionary projectNode)
    {
        foreach (var item in projectNode.GetValue<ValuesDictionary>("Subsystems"))
        {
            if (item.Key == "GameInfo")
            {
                return (ValuesDictionary)item.Value;
            }
        }

        throw new InvalidOperationException("GameInfo node not found in project.");
    }

    private static XElement GetPlayersNode(XElement projectNode)
    {
        var xElement = (from n in projectNode.Element("Subsystems")?.Elements("Values")
            where XmlUtils.GetAttributeValue(n, "Name", string.Empty) == "Players"
            select n).FirstOrDefault();
        return xElement ?? throw new InvalidOperationException("Players node not found in project.");
    }

    private static void PackWorld(
        string directoryName,
        Stream targetStream,
        Func<string, bool>? filter,
        bool embedExternalContent
    )
    {
        var worldInfo = GetWorldInfo(directoryName);
        if (worldInfo == null)
        {
            throw new InvalidOperationException("Directory does not contain a world.");
        }

        var list = new List<string>();
        RecursiveEnumerateDirectory(directoryName, list, [], filter);
        using var zipArchive = ZipArchive.ZipArchive.Create(targetStream);
        foreach (var item in list)
        {
            using var source = Storage.OpenFile(item, OpenFileMode.ReadWrite);
            var fileName = Storage.GetFileName(item);
            var fileDir = Storage.GetDirectoryName(item);
            if (fileDir.EndsWith("backup"))
            {
                continue;
            }

            if (fileDir.EndsWith("Regions"))
            {
                fileName = Storage.CombinePaths("Regions", fileName);
            }
            else if (fileDir.EndsWith("PlayerEntities"))
            {
                fileName = Storage.CombinePaths("PlayerEntities", fileName);
            }

            zipArchive.AddStream(fileName, source);
        }

        if (!embedExternalContent)
        {
            return;
        }

        if (!BlocksTexturesManager.IsBuiltIn(worldInfo.WorldSettings.BlocksTextureName))
        {
            try
            {
                using var source2 =
                    Storage.OpenFile(
                        BlocksTexturesManager.GetFileName(worldInfo.WorldSettings.BlocksTextureName),
                        OpenFileMode.Read);
                var filenameInZip = Storage.CombinePaths("EmbeddedContent",
                    Storage.GetFileNameWithoutExtension(worldInfo.WorldSettings.BlocksTextureName) +
                    ".scbtex");
                zipArchive.AddStream(filenameInZip, source2);
            }
            catch (Exception ex)
            {
                Log.Warning(
                    $"Failed to embed blocks texture \"{worldInfo.WorldSettings.BlocksTextureName}\". Reason: {ex.Message}");
            }
        }

        foreach (var playerInfo in worldInfo.PlayerInfos)
        {
            if (!CharacterSkinsManager.IsBuiltIn(playerInfo.CharacterSkinName))
            {
                try
                {
                    CharacterSkinsManager.GetFileName(playerInfo.CharacterSkinName, out var n);
                    using var source3 = Storage.OpenFile(n, OpenFileMode.Read);
                    var filenameInZip2 = Storage.CombinePaths("EmbeddedContent",
                        Storage.GetFileNameWithoutExtension(playerInfo.CharacterSkinName) + ".scskin");
                    zipArchive.AddStream(filenameInZip2, source3);
                }
                catch (Exception ex2)
                {
                    Log.Warning(
                        $"Failed to embed character skin \"{playerInfo.CharacterSkinName}\". Reason: {ex2.Message}");
                }
            }
        }
    }

    private static void UnpackWorld(string directoryName, Stream sourceStream, bool importEmbeddedExternalContent)
    {
        if (!Storage.DirectoryExists(directoryName))
        {
            throw new InvalidOperationException(
                $"Cannot import world into \"{directoryName}\" because this directory does not exist.");
        }

        using var zipArchive = ZipArchive.ZipArchive.Open(sourceStream, true);
        foreach (var item in zipArchive.ReadCentralDir())
        {
            var text = item.FilenameInZip.Replace('\\', '/');
            var extension = Storage.GetExtension(text);
            if (text.StartsWith("EmbeddedContent"))
            {
                try
                {
                    if (importEmbeddedExternalContent)
                    {
                        var memoryStream = new MemoryStream();
                        zipArchive.ExtractFile(item, memoryStream);
                        memoryStream.Position = 0L;
                        var type = ExternalContentManager.ExtensionToType(extension);
                        ExternalContentManager.ImportExternalContentSync(memoryStream, type,
                            Storage.GetFileNameWithoutExtension(text));
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed to import embedded content \"{text}\". Reason: {ex.Message}");
                }
            }
            else
            {
                var fileName = Storage.GetFileName(text);
                var fileDir = Storage.GetDirectoryName(text);
                if (fileDir.EndsWith("Regions"))
                {
                    if (!Storage.DirectoryExists(Storage.CombinePaths(directoryName, "Regions")))
                    {
                        Storage.CreateDirectory(Storage.CombinePaths(directoryName, "Regions"));
                    }

                    fileName = Storage.CombinePaths("Regions", fileName);
                }

                if (fileDir.EndsWith("PlayerEntities"))
                {
                    if (!Storage.DirectoryExists(Storage.CombinePaths(directoryName, "PlayerEntities")))
                    {
                        Storage.CreateDirectory(Storage.CombinePaths(directoryName, "PlayerEntities"));
                    }

                    fileName = Storage.CombinePaths("PlayerEntities", fileName);
                }

                using var stream = Storage.OpenFile(Storage.CombinePaths(directoryName, fileName),
                    OpenFileMode.Create);
                zipArchive.ExtractFile(item, stream);
            }
        }
    }

    private static void DeleteWorldContents(string directoryName, Func<string, bool>? filter)
    {
        var list = new List<string>();
        var list2 = new List<string>();
        RecursiveEnumerateDirectory(directoryName, list, list2, filter);
        foreach (var item in list)
        {
            Storage.DeleteFile(item);
        }

        foreach (var item2 in list2)
        {
            Storage.DeleteDirectory(item2);
        }
    }

    private static string MakeSnapshotFilename(string directoryName, string snapshotName)
    {
        return Storage.CombinePaths(directoryName, $"{snapshotName}.snapshot");
    }

    private static bool TestXmlFile(string fileName, string rootNodeName)
    {
        try
        {
            if (!Storage.FileExists(fileName))
            {
                return false;
            }

            using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
            var xElement = XmlUtils.LoadXmlFromStream(stream, null, false);
            return xElement.Name == rootNodeName;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

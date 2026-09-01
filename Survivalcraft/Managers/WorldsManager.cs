using System.IO.Compression;
using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

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

    public static string ImportWorldPackage(ZipArchive package)
    {
        var directoryName = GetUnusedWorldDirectoryName();
        Storage.CreateDirectory(directoryName);
        try
        {
            WriteWorldPackage(directoryName, package);
            return directoryName;
        }
        catch
        {
            DeleteWorld(directoryName);
            throw;
        }
    }

    public static string ReplaceWorldPackage(string assetKey, ZipArchive package)
    {
        if (assetKey.Contains('/') || assetKey.Contains('\\') || string.IsNullOrWhiteSpace(assetKey))
            throw new InvalidOperationException("World AssetKey is invalid.");
        var target = Storage.CombinePaths(_worldsDirectoryName, assetKey);
        if (!Storage.DirectoryExists(target)) throw new InvalidOperationException($"World '{assetKey}' does not exist.");
        var runningDirectory = GameManager.Project?.FindSubsystem<SubsystemGameInfo>()?.DirectoryName;
        if (runningDirectory is not null && string.Equals(runningDirectory, target, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A running world cannot be replaced.");

        var staging = Storage.CombinePaths(_worldsDirectoryName, $".{assetKey}.{Guid.NewGuid():N}.staging");
        var backup = Storage.CombinePaths(_worldsDirectoryName, $".{assetKey}.{Guid.NewGuid():N}.backup");
        Storage.CreateDirectory(staging);
        try
        {
            WriteWorldPackage(staging, package);
            if (!Storage.FileExists(Storage.CombinePaths(staging, "Project.xml")))
                throw new InvalidOperationException("Staged world does not contain Project.xml.");
            Storage.MoveDirectory(target, backup);
            try
            {
                Storage.MoveDirectory(staging, target);
            }
            catch
            {
                Storage.MoveDirectory(backup, target);
                throw;
            }
            Storage.DeleteDirectoryRecursive(backup);
            return target;
        }
        catch
        {
            if (Storage.DirectoryExists(staging)) Storage.DeleteDirectoryRecursive(staging);
            if (Storage.DirectoryExists(backup) && !Storage.DirectoryExists(target))
                Storage.MoveDirectory(backup, target);
            throw;
        }
    }

    private static void WriteWorldPackage(string directoryName, ZipArchive package)
    {
        foreach (var entry in package.Entries.Where(entry => entry.FullName == "payload/world/Project.xml" ||
                     entry.FullName.StartsWith("payload/world/Regions/", StringComparison.Ordinal)))
        {
            var target = Storage.CombinePaths(directoryName, entry.FullName["payload/world/".Length..]);
            var parent = Storage.GetDirectoryName(target);
            if (!Storage.DirectoryExists(parent)) Storage.CreateDirectory(parent);
            using var input = entry.Open();
            using var output = Storage.OpenFile(target, OpenFileMode.Create);
            input.CopyTo(output);
        }
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

    public static bool SnapshotExists(string directoryName, string snapshotName)
    {
        return Storage.FileExists(MakeSnapshotFilename(directoryName, snapshotName));
    }

    private static void TakeWorldSnapshot(string directoryName, string snapshotName)
    {
        using var targetStream =
            Storage.OpenFile(MakeSnapshotFilename(directoryName, snapshotName), OpenFileMode.Create);
        PackWorld(directoryName, targetStream, fn => Path.GetExtension(fn).ToLower() != ".snapshot");
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
        UnpackWorld(directoryName, sourceStream);
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
        if (!Storage.DirectoryExists(_worldsDirectoryName))
        {
            Storage.CreateDirectory(_worldsDirectoryName);
            return;
        }

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

    public static int ReplaceAssetReferences(ContentType type, string oldAssetKey, string newAssetKey)
    {
        if (type is not (ContentType.BlocksTexture or ContentType.CharacterSkin))
            throw new ArgumentOutOfRangeException(nameof(type));
        var valueName = type == ContentType.BlocksTexture ? "BlockTextureName" : "CharacterSkinName";
        var changes = new List<(string Project, string Temporary, string Backup)>();
        foreach (var worldName in Storage.ListDirectoryNames(_worldsDirectoryName))
        {
            var project = Storage.CombinePaths(_worldsDirectoryName, worldName, "Project.xml");
            if (!Storage.FileExists(project)) continue;
            using var input = Storage.OpenFile(project, OpenFileMode.Read);
            var document = XDocument.Load(input);
            var references = document.Descendants().Where(element =>
                (string?)element.Attribute("Name") == valueName &&
                (string?)element.Attribute("Value") == oldAssetKey).ToArray();
            if (references.Length == 0) continue;
            foreach (var reference in references) reference.SetAttributeValue("Value", newAssetKey);
            var temporary = project + $".{Guid.NewGuid():N}.temp";
            var backup = project + $".{Guid.NewGuid():N}.backup";
            using (var output = Storage.OpenFile(temporary, OpenFileMode.Create)) document.Save(output);
            changes.Add((project, temporary, backup));
        }

        var committed = new List<(string Project, string Backup)>();
        try
        {
            foreach (var change in changes)
            {
                Storage.MoveFile(change.Project, change.Backup);
                Storage.MoveFile(change.Temporary, change.Project);
                committed.Add((change.Project, change.Backup));
            }
        }
        catch
        {
            foreach (var change in changes.AsEnumerable().Reverse())
                if (Storage.FileExists(change.Backup)) Storage.MoveFile(change.Backup, change.Project);
            foreach (var change in changes)
                if (Storage.FileExists(change.Temporary)) Storage.DeleteFile(change.Temporary);
            throw;
        }
        foreach (var change in committed)
            if (Storage.FileExists(change.Backup)) Storage.DeleteFile(change.Backup);

        if (GameManager.Project is { } activeProject)
        {
            if (type == ContentType.BlocksTexture)
            {
                var settings = activeProject.FindSubsystem<SubsystemGameInfo>()?.WorldSettings;
                if (settings?.BlocksTextureName == oldAssetKey) settings.BlocksTextureName = newAssetKey;
            }
            else
            {
                var players = activeProject.FindSubsystem<SubsystemPlayers>()?.PlayersData;
                if (players is not null)
                    foreach (var player in players)
                        if (player.CharacterSkinName == oldAssetKey) player.CharacterSkinName = newAssetKey;
            }
        }
        return changes.Count;
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
        try
        {
            if (!Storage.FileExists(xmlFile))
            {
                return worldInfo;
            }

            using var stream = Storage.OpenFile(xmlFile, OpenFileMode.Read);
            var xElement = XmlUtils.LoadXmlFromStream(stream, null, true);
            worldInfo.ProjectFormatVersion = XmlUtils.GetAttributeValue(xElement, "Version", string.Empty);
            var gameInfoNode = GetGameInfoNode(xElement);
            var valuesDictionary = new ValuesDictionary();
            valuesDictionary.ApplyOverrides(gameInfoNode);
            worldInfo.WorldSettings.Load(valuesDictionary);
            var playersNode = GetPlayersNode(xElement);
            var playersValues = playersNode == null
                ? null
                : (from e in playersNode.Elements()
                    where XmlUtils.GetAttributeValue<string>(e, "Name") == "Players"
                    select e).FirstOrDefault();
            if (playersValues != null)
            {
                foreach (var item2 in playersValues.Elements())
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
            }

            return worldInfo;
        }
        catch (Exception e3)
        {
            Log.Error(ExceptionManager.MakeFullErrorMessage(
                $"Error getting data from project file \"{xmlFile}\".", e3));
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

        var databaseObject = DatabaseManager.GameDatabase.Database
            .FindDatabaseObject("GameProject", DatabaseManager.GameDatabase.ProjectTemplateType, true)!;
        var overrides = new ValuesDictionary();
        var gameInfoValues = new ValuesDictionary();
        overrides.SetValue("GameInfo", gameInfoValues);
        worldSettings.Save(gameInfoValues, false);
        gameInfoValues.SetValue("WorldDirectoryName", unusedWorldDirectoryName);
        gameInfoValues.SetValue("WorldSeed", num);
        var projectData = new ProjectData(DatabaseManager.GameDatabase, databaseObject, overrides);
        var projectNode = new XElement("Project");
        XmlUtils.SetAttributeValue(projectNode, "Version", WorldVersions.ProjectFormatVersion);
        projectData.Save(projectNode);
        using (var stream = Storage.OpenFile(Storage.CombinePaths(unusedWorldDirectoryName, "Project.xml"),
                   OpenFileMode.Create))
        {
            XmlUtils.SaveXmlToStream(projectNode, stream, null, true);
        }

        return GetWorldInfo(unusedWorldDirectoryName)
               ?? throw new ArgumentException("Create world failed");
    }

    public static void ChangeWorld(string directoryName, WorldSettings worldSettings)
    {
        var xmlFile = Storage.CombinePaths(directoryName, "Project.xml");
        if (!Storage.FileExists(xmlFile))
        {
            return;
        }

        XElement projectNode;
        GameMode value;
        using (var stream = Storage.OpenFile(xmlFile, OpenFileMode.Read))
        {
            projectNode = XmlUtils.LoadXmlFromStream(stream, null, true);
            var gameInfoNode = GetGameInfoNode(projectNode);
            var valuesDictionary = new ValuesDictionary();
            valuesDictionary.ApplyOverrides(gameInfoNode);
            value = valuesDictionary.GetValue<GameMode>("GameMode");
            worldSettings.Save(valuesDictionary, true);
            gameInfoNode.RemoveNodes();
            valuesDictionary.Save(gameInfoNode);
        }

        using (var stream = Storage.OpenFile(xmlFile, OpenFileMode.Create))
        {
            XmlUtils.SaveXmlToStream(projectNode, stream, null, true);
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

    private static XElement? GetPlayersNode(XElement projectNode)
    {
        var xElement = (from n in projectNode.Element("Subsystems")?.Elements("Values")
            where XmlUtils.GetAttributeValue(n, "Name", string.Empty) == "Players"
            select n).FirstOrDefault();
        return xElement;
    }

    private static void PackWorld(
        string directoryName,
        Stream targetStream,
        Func<string, bool>? filter
    )
    {
        var worldInfo = GetWorldInfo(directoryName);
        if (worldInfo == null)
        {
            throw new InvalidOperationException("Directory does not contain a world.");
        }

        var list = new List<string>();
        RecursiveEnumerateDirectory(directoryName, list, [], filter);
        using var zipArchive = new ZipArchive(targetStream, ZipArchiveMode.Create, false);
        foreach (var item in list)
        {
            using var source = Storage.OpenFile(item, OpenFileMode.Read);
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
            AddZipEntry(zipArchive, fileName, source);
        }

    }

    private static void UnpackWorld(string directoryName, Stream sourceStream)
    {
        if (!Storage.DirectoryExists(directoryName))
        {
            throw new InvalidOperationException(
                $"Cannot import world into \"{directoryName}\" because this directory does not exist.");
        }

        using var zipArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read, true);
        foreach (var item in zipArchive.Entries)
        {
            if (string.IsNullOrEmpty(item.Name))
            {
                continue;
            }

            var text = item.FullName.Replace('\\', '/');
            if (text.StartsWith("EmbeddedContent"))
            {
                continue;
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

                using var stream = Storage.OpenFile(Storage.CombinePaths(directoryName, fileName),
                    OpenFileMode.Create);
                using var entryStream = item.Open();
                entryStream.CopyTo(stream);
            }
        }
    }

    private static void AddZipEntry(ZipArchive zipArchive, string filenameInZip, Stream source)
    {
        var normalizedName = filenameInZip.Replace('\\', '/').Trim('/');
        var entry = zipArchive.CreateEntry(normalizedName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        source.CopyTo(entryStream);
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

    public static bool TestProjectFile(string fileName)
    {
        try
        {
            if (!Storage.FileExists(fileName))
            {
                return false;
            }

            using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
            var xElement = XmlUtils.LoadXmlFromStream(stream, null, false);
            return xElement.Name == "Project";
        }
        catch (Exception)
        {
            return false;
        }
    }
}

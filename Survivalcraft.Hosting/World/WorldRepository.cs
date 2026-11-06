using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

using Random = Engine.Core.Mathematics.Random;

namespace Game.World;

public sealed class WorldRepository
{
    public const int MaxAllowedWorlds = 30;

    private readonly List<WorldInfo> _worldInfos = [];

    private readonly Random _random = new();

    private readonly string _worldsDirectoryName;

    private readonly Hosting.WorldProjectActivity _activity;

    private readonly Action _waitForPendingSaves;

    public ReadOnlyList<string> NewWorldNames { get; private set; } = new([]);

    public ReadOnlyList<WorldInfo> WorldInfos => new(_worldInfos);

    public event Action<string>? WorldDeleted;

    public WorldRepository(
        string worldsDirectory,
        IEnumerable<string> newWorldNames,
        Hosting.WorldProjectActivity activity,
        Action waitForPendingSaves
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldsDirectory);
        ArgumentNullException.ThrowIfNull(newWorldNames);
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _waitForPendingSaves = waitForPendingSaves ?? throw new ArgumentNullException(nameof(waitForPendingSaves));
        _worldsDirectoryName = worldsDirectory;
        SetNewWorldNames(newWorldNames);
        Storage.CreateDirectory(_worldsDirectoryName);
    }

    public void SetNewWorldNames(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        NewWorldNames = new ReadOnlyList<string>(names.ToArray());
    }

    public string ImportWorldPackage(ZipArchive package)
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

    public string ReplaceWorldPackage(string assetKey, ZipArchive package)
    {
        if (assetKey.Contains('/') || assetKey.Contains('\\') || string.IsNullOrWhiteSpace(assetKey))
        {
            throw new InvalidOperationException("World AssetKey is invalid.");
        }

        var target = Storage.CombinePaths(_worldsDirectoryName, assetKey);
        if (!Storage.DirectoryExists(target))
        {
            throw new InvalidOperationException($"World '{assetKey}' does not exist.");
        }

        using var operation = BeginFileOperation(target);

        var staging = Storage.CombinePaths(_worldsDirectoryName, $".{assetKey}.{Guid.NewGuid():N}.staging");
        var backup = Storage.CombinePaths(_worldsDirectoryName, $".{assetKey}.{Guid.NewGuid():N}.backup");
        Storage.CreateDirectory(staging);
        try
        {
            WriteWorldPackage(staging, package);
            if (!Storage.FileExists(Storage.CombinePaths(staging, "Project.xml")))
            {
                throw new InvalidOperationException("Staged world does not contain Project.xml.");
            }

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
            if (Storage.DirectoryExists(staging))
            {
                Storage.DeleteDirectoryRecursive(staging);
            }

            if (Storage.DirectoryExists(backup) && !Storage.DirectoryExists(target))
            {
                Storage.MoveDirectory(backup, target);
            }

            throw;
        }
    }

    private void WriteWorldPackage(string directoryName, ZipArchive package)
    {
        foreach (var entry in package.Entries.Where(entry => entry.FullName == "payload/world/Project.xml" ||
                                                             entry.FullName.StartsWith(
                                                                 "payload/world/Regions/",
                                                                 StringComparison.Ordinal
                                                             )
                 ))
        {
            var target = Storage.CombinePaths(directoryName, entry.FullName["payload/world/".Length..]);
            var parent = Storage.GetDirectoryName(target);
            if (!Storage.DirectoryExists(parent))
            {
                Storage.CreateDirectory(parent);
            }

            using var input = entry.Open();
            using var output = Storage.OpenFile(target, OpenFileMode.Create);
            input.CopyTo(output);
        }
    }

    private IDisposable BeginFileOperation(string directoryName)
    {
        _waitForPendingSaves();
        return _activity.BeginFileOperation(directoryName);
    }

    public void DeleteWorld(string directoryName)
    {
        if (Storage.DirectoryExists(directoryName))
        {
            using var operation = BeginFileOperation(directoryName);
            DeleteWorldContents(directoryName, null);
            Storage.DeleteDirectory(directoryName);
        }

        WorldDeleted?.Invoke(directoryName);
    }

    public bool SnapshotExists(string directoryName, string snapshotName)
    {
        return Storage.FileExists(MakeSnapshotFilename(directoryName, snapshotName));
    }

    private void TakeWorldSnapshot(string directoryName, string snapshotName)
    {
        using var targetStream =
            Storage.OpenFile(MakeSnapshotFilename(directoryName, snapshotName), OpenFileMode.Create);
        PackWorld(directoryName, targetStream, fn => Path.GetExtension(fn).ToLower() != ".snapshot");
    }

    public void RestoreWorldFromSnapshot(string directoryName, string snapshotName)
    {
        if (!SnapshotExists(directoryName, snapshotName))
        {
            return;
        }

        using var operation = BeginFileOperation(directoryName);
        DeleteWorldContents(directoryName, fn => Storage.GetExtension(fn).ToLower() != ".snapshot");
        using var sourceStream =
            Storage.OpenFile(MakeSnapshotFilename(directoryName, snapshotName), OpenFileMode.Read);
        UnpackWorld(directoryName, sourceStream);
    }

    private void DeleteWorldSnapshot(string directoryName, string snapshotName)
    {
        var path = MakeSnapshotFilename(directoryName, snapshotName);
        if (Storage.FileExists(path))
        {
            Storage.DeleteFile(path);
        }
    }

    public void UpdateWorldsList()
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

    public bool ValidateWorldName(string name)
    {
        return !name.Contains('\\') && name.Length <= 128;
    }

    public string GetUnusedWorldName()
    {
        var usedNames = _worldInfos.Select(world => world.WorldSettings.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableNames = NewWorldNames.Where(name => !usedNames.Contains(name)).ToArray();
        if (availableNames.Length > 0)
        {
            return availableNames[_random.Int(0, availableNames.Length - 1)];
        }

        var baseName = NewWorldNames[_random.Int(0, NewWorldNames.Count - 1)];
        for (var index = 2;; index++)
        {
            var name = $"{baseName} {index}";
            if (!usedNames.Contains(name))
            {
                return name;
            }
        }
    }

    public int ReplaceAssetReferences(ContentType type, string oldAssetKey, string newAssetKey)
    {
        if (type is not (ContentType.BlocksTexture or ContentType.CharacterSkin))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        _waitForPendingSaves();
        var valueName = type == ContentType.BlocksTexture ? "BlockTextureName" : "CharacterSkinName";
        var changes = new List<(string Project, string Temporary, string Backup)>();
        foreach (var worldName in Storage.ListDirectoryNames(_worldsDirectoryName))
        {
            var project = Storage.CombinePaths(_worldsDirectoryName, worldName, "Project.xml");
            if (!Storage.FileExists(project))
            {
                continue;
            }

            using var input = Storage.OpenFile(project, OpenFileMode.Read);
            var document = XDocument.Load(input);
            var references = document.Descendants().Where(element =>
                (string?)element.Attribute("Name") == valueName &&
                (string?)element.Attribute("Value") == oldAssetKey
            ).ToArray();
            if (references.Length == 0)
            {
                continue;
            }

            foreach (var reference in references)
            {
                reference.SetAttributeValue("Value", newAssetKey);
            }

            var temporary = project + $".{Guid.NewGuid():N}.temp";
            var backup = project + $".{Guid.NewGuid():N}.backup";
            using (var output = Storage.OpenFile(temporary, OpenFileMode.Create))
            {
                document.Save(output);
            }

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
            {
                if (Storage.FileExists(change.Backup))
                {
                    Storage.MoveFile(change.Backup, change.Project);
                }
            }

            foreach (var change in changes)
            {
                if (Storage.FileExists(change.Temporary))
                {
                    Storage.DeleteFile(change.Temporary);
                }
            }

            throw;
        }

        foreach (var change in committed)
        {
            if (Storage.FileExists(change.Backup))
            {
                Storage.DeleteFile(change.Backup);
            }
        }

        foreach (var activeProject in _activity.Projects)
        {
            if (type == ContentType.BlocksTexture)
            {
                var settings = activeProject.FindSubsystem<SubsystemGameInfo>()?.WorldSettings;
                if (settings?.BlocksTextureName == oldAssetKey)
                {
                    settings.BlocksTextureName = newAssetKey;
                }
            }
            else
            {
                var players = activeProject.FindSubsystem<SubsystemCharacters>()?.Characters;
                if (players is not null)
                {
                    foreach (var player in players)
                    {
                        if (player.CharacterSkinName == oldAssetKey)
                        {
                            player.CharacterSkinName = newAssetKey;
                        }
                    }
                }
            }
        }

        return changes.Count;
    }

    public WorldInfo? GetWorldInfo(string directoryName)
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
                Log.Error($"Error getting size of file \"{item}\". {e2}");
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
            worldInfo.Hosting.Load(valuesDictionary);
            var playersNode = GetCharactersNode(xElement);
            var playersValues = playersNode == null
                ? null
                : (from e in playersNode.Elements()
                    where XmlUtils.GetAttributeValue<string>(e, "Name") == "Characters"
                    select e).FirstOrDefault();
            if (playersValues != null)
            {
                foreach (var item2 in playersValues.Elements())
                {
                    var characterInfo = new CharacterInfo();
                    worldInfo.CharacterInfos.Add(characterInfo);
                    var xElement2 = (from e in item2.Elements()
                        where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "CharacterSkinName"
                        select e).FirstOrDefault();
                    if (xElement2 != null)
                    {
                        characterInfo.CharacterSkinName =
                            XmlUtils.GetAttributeValue(xElement2, "Value", string.Empty);
                    }
                }
            }

            return worldInfo;
        }
        catch (Exception e3)
        {
            Log.Error($"Error getting data from project file \"{xmlFile}\". {e3}");
            return worldInfo;
        }
    }

    public WorldInfo CreateWorld(
        GameDatabase database,
        WorldSettings worldSettings,
        WorldHostingSettings hosting,
        string customWorldDirectoryName = "",
        Admission.ServerAdmissionPolicy? admission = null
    )
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

        if (string.IsNullOrEmpty(worldSettings.Seed))
        {
            worldSettings.Seed = WorldSeedConverter.CreateRandomTextSeed();
        }

        var worldSeed = WorldSeedConverter.FromText(worldSettings.Seed);

        var databaseObject = database.Database
            .FindDatabaseObject("GameProject", database.ProjectTemplateType, true)!;
        var overrides = new ValuesDictionary();
        var gameInfoValues = new ValuesDictionary();
        overrides.SetValue("GameInfo", gameInfoValues);
        worldSettings.Save(gameInfoValues, false);
        hosting.Save(gameInfoValues, worldSettings.Name);
        gameInfoValues.SetValue("WorldDirectoryName", unusedWorldDirectoryName);
        gameInfoValues.SetValue("WorldSeed", worldSeed);
        var projectData = new ProjectData(database, databaseObject, overrides);
        var projectNode = new XElement("Project");
        XmlUtils.SetAttributeValue(projectNode, "Version", WorldVersions.ProjectFormatVersion);
        projectData.Save(projectNode);
        if (admission is not null)
        {
            WriteAdmissionPolicy(projectNode, admission);
        }

        projectNode.Add(BlockIndexMapping.Empty.ToXml());
        SaveWorldMetadata(Storage.CombinePaths(unusedWorldDirectoryName, "Project.xml"), projectNode);

        return GetWorldInfo(unusedWorldDirectoryName) ?? throw new ArgumentException("Create world failed");
    }

    public WorldInfo CloneWorld(string sourceDirectoryName, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!ValidateWorldName(name))
        {
            throw new InvalidOperationException($"World name \"{name}\" is invalid.");
        }

        var sourceWorld = GetWorldInfo(sourceDirectoryName) ??
                          throw new InvalidOperationException("Source directory does not contain a world.");
        if (!Storage.FileExists(Storage.CombinePaths(sourceDirectoryName, "Project.xml")))
        {
            throw new InvalidOperationException("Source directory does not contain a valid world project.");
        }

        var targetDirectoryName = GetUnusedWorldDirectoryName();
        Storage.CreateDirectory(targetDirectoryName);
        try
        {
            CopyDirectory(sourceDirectoryName, targetDirectoryName);
            sourceWorld.WorldSettings.Name = name;
            ChangeWorld(targetDirectoryName, sourceWorld.WorldSettings, sourceWorld.Hosting);
            var clonedWorld = GetWorldInfo(targetDirectoryName);
            if (clonedWorld is null ||
                !Storage.FileExists(Storage.CombinePaths(targetDirectoryName, "Project.xml")))
            {
                throw new InvalidOperationException("Cloned directory does not contain a valid world project.");
            }

            return clonedWorld;
        }
        catch
        {
            if (Storage.DirectoryExists(targetDirectoryName))
            {
                try
                {
                    DeleteWorldContents(targetDirectoryName, null);
                    Storage.DeleteDirectory(targetDirectoryName);
                }
                catch (Exception exception)
                {
                    Log.Error($"Failed to clean up incomplete world clone \"{targetDirectoryName}\". {exception}");
                }
            }

            throw;
        }
    }

    public void ChangeWorld(
        string directoryName,
        WorldSettings worldSettings,
        WorldHostingSettings hosting,
        Admission.ServerAdmissionPolicy? admission = null
    )
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
            hosting.Save(valuesDictionary, worldSettings.Name);
            gameInfoNode.RemoveNodes();
            valuesDictionary.Save(gameInfoNode);
        }

        if (admission is not null)
        {
            WriteAdmissionPolicy(projectNode, admission);
        }

        SaveWorldMetadata(xmlFile, projectNode);

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

    private void SaveWorldMetadata(string location, XElement root)
    {
        var path = Storage.GetSystemPath(location);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
                {
                    XmlUtils.SaveXmlToTextWriter(root, writer, true);
                }

                stream.Flush(true);
            }

            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) { File.Delete(temporary); }
        }
    }

    private string GetUnusedWorldDirectoryName()
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

    private void RecursiveEnumerateDirectory(
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

    private void CopyDirectory(string sourceDirectoryName, string targetDirectoryName)
    {
        foreach (var directory in Storage.ListDirectoryNames(sourceDirectoryName))
        {
            var source = Storage.CombinePaths(sourceDirectoryName, directory);
            var target = Storage.CombinePaths(targetDirectoryName, directory);
            Storage.CreateDirectory(target);
            CopyDirectory(source, target);
        }

        foreach (var file in Storage.ListFileNames(sourceDirectoryName))
        {
            Storage.CopyFile(
                Storage.CombinePaths(sourceDirectoryName, file),
                Storage.CombinePaths(targetDirectoryName, file)
            );
        }
    }

    private XElement GetGameInfoNode(XElement projectNode)
    {
        var xElement = (from n in projectNode.Element("Subsystems")?.Elements("Values")
            where XmlUtils.GetAttributeValue(n, "Name", string.Empty) == "GameInfo"
            select n).FirstOrDefault();
        return xElement ?? throw new InvalidOperationException("GameInfo node not found in project.");
    }

    private XElement? GetCharactersNode(XElement projectNode)
    {
        var xElement = (from n in projectNode.Element("Subsystems")?.Elements("Values")
            where XmlUtils.GetAttributeValue(n, "Name", string.Empty) == "Characters"
            select n).FirstOrDefault();
        return xElement;
    }

    private void PackWorld(
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

    private void UnpackWorld(string directoryName, Stream sourceStream)
    {
        if (!Storage.DirectoryExists(directoryName))
        {
            throw new InvalidOperationException(
                $"Cannot import world into \"{directoryName}\" because this directory does not exist."
            );
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

                using var stream = Storage.OpenFile(
                    Storage.CombinePaths(directoryName, fileName),
                    OpenFileMode.Create
                );
                using var entryStream = item.Open();
                entryStream.CopyTo(stream);
            }
        }
    }

    private void AddZipEntry(ZipArchive zipArchive, string filenameInZip, Stream source)
    {
        var normalizedName = filenameInZip.Replace('\\', '/').Trim('/');
        var entry = zipArchive.CreateEntry(normalizedName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        source.CopyTo(entryStream);
    }

    private void DeleteWorldContents(string directoryName, Func<string, bool>? filter)
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

    private string MakeSnapshotFilename(string directoryName, string snapshotName)
    {
        return Storage.CombinePaths(directoryName, $"{snapshotName}.snapshot");
    }

    public bool TestProjectFile(string fileName)
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

    public Admission.ServerAdmissionPolicy ReadAdmissionPolicy(string directory)
    {
        using var stream = Storage.OpenFile(Storage.CombinePaths(directory, "Project.xml"), OpenFileMode.Read);
        var root = XElement.Load(stream);
        var node = root.Element("ExtensionData")?.Elements("Values")
            .SingleOrDefault(value => (string?)value.Attribute("Name") == "game")?
            .Elements("Values").SingleOrDefault(value => (string?)value.Attribute("Name") == "admission");
        var data = new ValuesDictionary();
        if (node is not null) { data.ApplyOverrides(node); }

        return Admission.ServerAdmissionPolicy.Load(data);
    }

    internal static void WriteAdmissionPolicy(XElement root, Admission.ServerAdmissionPolicy policy)
    {
        var extension = root.Element("ExtensionData");
        if (extension is null)
        {
            extension = new XElement("ExtensionData");
            root.Add(extension);
        }

        var game = extension.Elements("Values").SingleOrDefault(value => (string?)value.Attribute("Name") == "game");
        if (game is null)
        {
            game = new XElement("Values", new XAttribute("Name", "game"));
            extension.Add(game);
        }

        var previous = game.Elements("Values")
            .SingleOrDefault(value => (string?)value.Attribute("Name") == "admission");
        var admission = new XElement("Values", new XAttribute("Name", "admission"));
        policy.Save().Save(admission);
        if (previous is not null) { previous.ReplaceWith(admission); }
        else { game.Add(admission); }
    }
}

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
        RecoverInterruptedEdits();
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

        ModProfileManager.SaveWorldProfile(directoryName, new ModProfile());
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

    public async Task<int> ReplaceAssetReferencesAsync(ContentType type, string oldAssetKey, string newAssetKey)
    {
        if (type is not (ContentType.BlocksTexture or ContentType.CharacterSkin))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        var saves = new List<(string Directory, Task<WorldSaveOutcome> Completion)>();
        await _activity.ApplyToProjectsAsync(activeProject =>
            {
                var gameInfo = activeProject.FindSubsystem<SubsystemGameInfo>();
                if (type == ContentType.BlocksTexture)
                {
                    var settings = gameInfo?.WorldSettings;
                    if (settings?.BlocksTextureName == oldAssetKey)
                    {
                        settings.BlocksTextureName = newAssetKey;
                    }
                }
                else
                {
                    activeProject.FindSubsystem<SubsystemCharacters>()?.ReplaceSkinReferences(oldAssetKey, newAssetKey);
                }

                // An earlier replacement may have changed the live references without committing its save.
                if (!string.IsNullOrEmpty(gameInfo?.DirectoryName) &&
                    WorldProjectPersistence.TryGet(activeProject, out var persistence))
                {
                    saves.Add((Storage.GetSystemPath(gameInfo.DirectoryName), persistence!.SaveAsync()));
                }
            }
        ).ConfigureAwait(false);

        var outcomes = await Task.WhenAll(saves.Select(save => save.Completion)).ConfigureAwait(false);
        foreach (var outcome in outcomes)
        {
            if (outcome.Failure != null) { throw new WorldSaveException(outcome.WorldName, outcome.Failure); }
        }

        var savedDirectories = saves.Select(save => save.Directory).ToHashSet(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
        );
        return await Task.Run(() => ReplaceStoredAssetReferences(type, oldAssetKey, newAssetKey, savedDirectories))
            .ConfigureAwait(false);
    }

    private int ReplaceStoredAssetReferences(
        ContentType type,
        string oldAssetKey,
        string newAssetKey,
        HashSet<string> savedDirectories
    )
    {
        var leases = new List<IDisposable>();
        var changes = new List<(string Project, string Temporary, string Backup)>();
        try
        {
            _waitForPendingSaves();
            var valueName = type == ContentType.BlocksTexture ? "BlockTextureName" : "CharacterSkinName";
            foreach (var worldName in Storage.ListDirectoryNames(_worldsDirectoryName))
            {
                var directory = Storage.CombinePaths(_worldsDirectoryName, worldName);
                if (_activity.IsDirectoryInUse(directory))
                {
                    if (savedDirectories.Contains(Storage.GetSystemPath(directory))) { continue; }

                    throw new InvalidOperationException("An active world has not saved its asset references.");
                }

                leases.Add(_activity.BeginFileOperation(directory));
                var project = Storage.CombinePaths(directory, "Project.xml");
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
                changes.Add((project, temporary, backup));
                using (var output = Storage.OpenFile(temporary, OpenFileMode.Create))
                {
                    document.Save(output);
                }
            }

            try
            {
                foreach (var change in changes)
                {
                    Storage.MoveFile(change.Project, change.Backup);
                    Storage.MoveFile(change.Temporary, change.Project);
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

                throw;
            }

            foreach (var change in changes)
            {
                if (Storage.FileExists(change.Backup))
                {
                    Storage.DeleteFile(change.Backup);
                }
            }

            return changes.Count;
        }
        finally
        {
            try
            {
                foreach (var change in changes)
                {
                    if (Storage.FileExists(change.Temporary)) { Storage.DeleteFile(change.Temporary); }
                }
            }
            finally
            {
                foreach (var lease in leases) { lease.Dispose(); }
            }
        }
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

            var offlineCharacters = playersNode?.Elements("Values")
                .FirstOrDefault(element => (string?)element.Attribute("Name") == "OfflineCharacters");
            foreach (var value in offlineCharacters?.Descendants("Value") ?? [])
            {
                if ((string?)value.Attribute("Name") == "CharacterSkinName" &&
                    (string?)value.Attribute("Value") is { } name)
                {
                    worldInfo.OfflineCharacterSkinNames.Add(name);
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
        ModProfile initialProfile,
        string customWorldDirectoryName = "",
        Admission.ServerAdmissionPolicy? admission = null
    )
    {
        ModProfileValidation.Validate(initialProfile);
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
        using var operation = BeginFileOperation(unusedWorldDirectoryName);
        var staging = CreateStagingDirectory(unusedWorldDirectoryName);
        try
        {
            SaveWorldMetadata(Storage.CombinePaths(staging, "Project.xml"), projectNode);
            ModProfileManager.SaveWorldProfile(staging, initialProfile);
            Storage.MoveDirectory(staging, unusedWorldDirectoryName);
        }
        finally { DeleteStagingDirectory(staging); }

        return GetWorldInfo(unusedWorldDirectoryName) ?? throw new ArgumentException("Create world failed");
    }

    /// <summary>检查模组变更，必要时先克隆完整旧世界，再向当前世界应用编辑结果。</summary>
    public WorldInfo? ApplyWorldEdit(
        string directoryName,
        WorldSettings worldSettings,
        WorldHostingSettings hosting,
        Admission.ServerAdmissionPolicy admission,
        ModProfile desiredProfile,
        LocalModRepository packages,
        string? cloneName
    )
    {
        ArgumentNullException.ThrowIfNull(packages);
        ModProfileValidation.Validate(desiredProfile);
        if (!ValidateWorldName(worldSettings.Name))
        {
            throw new InvalidOperationException($"World name \"{worldSettings.Name}\" is invalid.");
        }

        using var operation = BeginFileOperation(directoryName);
        var original = ModProfileManager.LoadWorldProfile(directoryName) ??
                       throw new InvalidOperationException("World mod profile is missing.");
        var changed = !ModProfileIdentity.AreEquivalent(original, desiredProfile);
        WorldInfo? clone = null;
        if (changed)
        {
            foreach (var removed in original.Packages.Where(package =>
                         !desiredProfile.Packages.Any(target =>
                             string.Equals(target.ModId, package.ModId, StringComparison.OrdinalIgnoreCase)
                         )
                     ))
            {
                if (!ReadPackage(removed).Manifest.CanRemoveFromWorld)
                {
                    throw new InvalidOperationException($"Mod '{removed.ModId}' cannot be removed from this world.");
                }
            }

            packages.ReadLoadPlan(desiredProfile);

            ArgumentException.ThrowIfNullOrWhiteSpace(cloneName);
            clone = CloneWorld(directoryName, cloneName);
        }

        if (changed)
        {
            var staging = CreateStagingDirectory(directoryName);
            try
            {
                CopyDirectory(directoryName, staging);
                ModProfileManager.SaveWorldProfile(staging, desiredProfile);
                ChangeWorld(staging, worldSettings, hosting, admission, refreshAdventureSnapshot: true);

                PublishEditedWorld(staging, directoryName);
            }
            finally { DeleteStagingDirectory(staging); }
        }
        else { ChangeWorld(directoryName, worldSettings, hosting, admission); }

        return clone;

        ModPackage ReadPackage(ModPackageRequirement requirement)
        {
            var entry = packages.Find(requirement) ??
                        throw new InvalidOperationException(
                            $"Mod package '{requirement.ModId}@{requirement.Version}' is not available locally."
                        );
            using var stream = File.OpenRead(entry.Path);
            var package = ModPackage.Read(entry.Path, stream);
            if (!string.Equals(package.PackageHash, requirement.PackageHash, StringComparison.Ordinal) ||
                !string.Equals(package.Manifest.Id, requirement.ModId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(package.Manifest.Version, requirement.Version, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Mod package '{requirement.ModId}' does not match its profile.");
            }

            return package;
        }
    }

    /// <summary>生成确认框和实际副本共用的名称，仅替换末尾已有的克隆时间戳。</summary>
    public static string GetCloneName(string sourceName, DateTime localTime)
    {
        return CloneNaming.Create(sourceName, localTime, 128);
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
        ChangeWorld(directoryName, worldSettings, hosting, admission, refreshAdventureSnapshot: false);
    }

    private void ChangeWorld(
        string directoryName,
        WorldSettings worldSettings,
        WorldHostingSettings hosting,
        Admission.ServerAdmissionPolicy? admission,
        bool refreshAdventureSnapshot
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
            ValidateSavedGenerator(projectNode);
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

        if (worldSettings.GameMode == value && !(refreshAdventureSnapshot && value == GameMode.Adventure))
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

    private string CreateStagingDirectory(string target)
    {
        var staging = Storage.CombinePaths(_worldsDirectoryName + ".staging", Storage.GetFileName(target));
        DeleteStagingDirectory(staging);
        Storage.CreateDirectory(staging);
        return staging;
    }

    private static void DeleteStagingDirectory(string staging)
    {
        if (Storage.DirectoryExists(staging)) { Storage.DeleteDirectoryRecursive(staging); }
    }

    private void PublishEditedWorld(string staging, string target)
    {
        var previousRoot = _worldsDirectoryName + ".previous";
        Storage.CreateDirectory(previousRoot);
        var previous = Storage.CombinePaths(previousRoot, Storage.GetFileName(target));
        Storage.MoveDirectory(target, previous);
        try { Storage.MoveDirectory(staging, target); }
        catch
        {
            Storage.MoveDirectory(previous, target);
            throw;
        }

        Storage.DeleteDirectoryRecursive(previous);
    }

    /// <summary>完成进程中断时尚未结束的目录替换；玩家命名的独立副本另行保存。</summary>
    private void RecoverInterruptedEdits()
    {
        var previousRoot = _worldsDirectoryName + ".previous";
        if (!Storage.DirectoryExists(previousRoot)) { return; }

        foreach (var name in Storage.ListDirectoryNames(previousRoot))
        {
            var previous = Storage.CombinePaths(previousRoot, name);
            var target = Storage.CombinePaths(_worldsDirectoryName, name);
            if (Storage.DirectoryExists(target)) { Storage.DeleteDirectoryRecursive(previous); }
            else { Storage.MoveDirectory(previous, target); }
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

    internal static void ValidateSavedGenerator(XElement projectNode)
    {
        var gameInfo = GetGameInfoNode(projectNode);
        var fields = gameInfo.Elements().Where(element =>
            (string?)element.Attribute("Name") is "TerrainGeneratorId" or "TerrainGeneratorVersion"
            or "TerrainGeneratorParameters"
        );
        var saved = new ValuesDictionary();
        saved.ApplyOverrides(new XElement("Values", fields));
        Game.Terrains.Generation.TerrainGeneratorSettings.Load(saved);
    }

    private static XElement GetGameInfoNode(XElement projectNode)
    {
        return projectNode.Element("Subsystems")?.Elements("Values")
                   .FirstOrDefault(element => (string?)element.Attribute("Name") == "GameInfo") ??
               throw new InvalidDataException("Missing GameInfo in world metadata.");
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

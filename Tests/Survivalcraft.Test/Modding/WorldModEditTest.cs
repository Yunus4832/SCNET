using Engine.Core.FileStorage;

using Game.Admission;
using Game.Hosting;
using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.World;

namespace Survivalcraft.Test.Modding;

/// <summary>通过真实世界文件验证模组变更的克隆顺序、作者声明和失败隔离。</summary>
[Collection(ConfigFileCollection.Name)]
public sealed class WorldModEditTest : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "scnet-world-mod-edit-" + Guid.NewGuid().ToString("N")
    );

    private readonly LocalModRepository _packages;

    private readonly GameModRuntime _runtime;

    private readonly WorldRepository _worlds;

    public WorldModEditTest()
    {
        Directory.CreateDirectory(_root);
        var scheme = "modedit" + Guid.NewGuid().ToString("N");
        Storage.RegisterFileSystemRoot(scheme, _root);
        _packages = new LocalModRepository(Path.Combine(_root, "Packages"));
        _worlds = new WorldRepository(scheme + ":Worlds", ["World"], new WorldProjectActivity(), () => { });
        _runtime = GameModRuntime.Start();
        _runtime.InitializeDatabase();
    }

    [Theory]
    [InlineData("海岛", "海岛@24153045")]
    [InlineData("基地@北岸", "基地@北岸@24153045")]
    [InlineData("基地@北岸@01010203", "基地@北岸@24153045")]
    [InlineData("海岛@01010203A", "海岛@01010203A@24153045")]
    public void CloneNameReplacesOnlyTheTrailingTimestamp(string source, string expected)
    {
        Assert.Equal(expected, WorldRepository.GetCloneName(source, new DateTime(2026, 11, 24, 15, 30, 45)));
    }

    [Fact]
    public void AddingModClonesAllOldSettingsAndProfileBeforeChangingCurrentWorld()
    {
        var world = CreateWorld(new ModProfile());
        var terrain = Storage.CombinePaths(world.DirectoryName, "terrain.fixture");
        using (var stream = Storage.OpenFile(terrain, OpenFileMode.Create))
        {
            stream.Write([1, 2, 3, 4]);
        }

        var desired = Profile(AddPackage("example.add", false));
        world.WorldSettings.Name = "Edited";
        var clone = _worlds.ApplyWorldEdit(
            world.DirectoryName,
            world.WorldSettings,
            world.Hosting,
            ServerAdmissionPolicy.WithPassword("test-password"),
            desired,
            _packages,
            "Before"
        );

        Assert.NotNull(clone);
        Assert.Equal("Before", clone.WorldSettings.Name);
        Assert.Equal("Edited", _worlds.GetWorldInfo(world.DirectoryName)!.WorldSettings.Name);
        Assert.Empty(ModProfileManager.LoadWorldProfile(clone.DirectoryName)!.Packages);
        AssertPackage(desired.Packages[0], ModProfileManager.LoadWorldProfile(world.DirectoryName)!);
        Assert.Equal(ServerAdmissionMode.Open, _worlds.ReadAdmissionPolicy(clone.DirectoryName).Mode);
        Assert.Equal(ServerAdmissionMode.Password, _worlds.ReadAdmissionPolicy(world.DirectoryName).Mode);
        Assert.Equal(
            [1, 2, 3, 4],
            File.ReadAllBytes(Storage.GetSystemPath(Storage.CombinePaths(clone.DirectoryName, "terrain.fixture")))
        );
        Assert.Equal(2, Directory.GetDirectories(Path.Combine(_root, "Worlds")).Length);
    }

    [Fact]
    public void NonRemovableModCannotBeRemovedAndSourceIsNotWritten()
    {
        var world = CreateWorld(Profile(AddPackage("example.required", false)));
        var before = ReadProject(world);
        world.WorldSettings.Name = "MustNotBeSaved";

        Assert.Throws<InvalidOperationException>(() => Apply(world, new ModProfile(), "Before"));

        Assert.Equal(before, ReadProject(world));
        Assert.Single(ModProfileManager.LoadWorldProfile(world.DirectoryName)!.Packages);
        Assert.Single(Directory.GetDirectories(Path.Combine(_root, "Worlds")));
    }

    [Fact]
    public void RemovableModLeavesItsOldProfileOnlyInIndependentClone()
    {
        var requirement = AddPackage("example.removable", true);
        var world = CreateWorld(Profile(requirement));

        var clone = Apply(world, new ModProfile(), "Before");

        Assert.NotNull(clone);
        Assert.Empty(ModProfileManager.LoadWorldProfile(world.DirectoryName)!.Packages);
        AssertPackage(requirement, ModProfileManager.LoadWorldProfile(clone.DirectoryName)!);
    }

    [Fact]
    public void ReplacingNonRemovableModVersionIsAllowedAndStillClones()
    {
        var original = AddPackage("example.required", false);
        var replacement = AddPackage("example.required", false, "2.0.0");
        var world = CreateWorld(Profile(original));

        var clone = Apply(world, Profile(replacement), "Before");

        Assert.NotNull(clone);
        AssertPackage(original, ModProfileManager.LoadWorldProfile(clone.DirectoryName)!);
        AssertPackage(replacement, ModProfileManager.LoadWorldProfile(world.DirectoryName)!);
    }

    [Fact]
    public void ReorderingUnchangedProfileDoesNotRequireClone()
    {
        var first = AddPackage("example.first", false);
        var second = AddPackage("example.second", false);
        var world = CreateWorld(Profile(first, second));
        world.WorldSettings.Name = "Renamed";

        var clone = Apply(world, Profile(second, first), null);

        Assert.Null(clone);
        Assert.Equal("Renamed", _worlds.GetWorldInfo(world.DirectoryName)!.WorldSettings.Name);
        Assert.Single(Directory.GetDirectories(Path.Combine(_root, "Worlds")));
    }

    [Fact]
    public void MissingOriginalDeclarationBlocksRemovalWithoutWritingOrCloning()
    {
        var requirement = AddPackage("example.missing", true);
        var world = CreateWorld(Profile(requirement));
        File.Delete(_packages.Find(requirement)!.Path);
        _packages.Invalidate();
        var before = ReadProject(world);

        Assert.Throws<InvalidOperationException>(() => Apply(world, new ModProfile(), "Before"));

        Assert.Equal(before, ReadProject(world));
        AssertPackage(requirement, ModProfileManager.LoadWorldProfile(world.DirectoryName)!);
        Assert.Single(Directory.GetDirectories(Path.Combine(_root, "Worlds")));
    }

    [Fact]
    public void CloneReadFailureLeavesSourceAndProfileUnchanged()
    {
        var world = CreateWorld(new ModProfile());
        var before = ReadProject(world);
        var desired = Profile(AddPackage("example.add", true));
        var file = Storage.GetSystemPath(Storage.CombinePaths(world.DirectoryName, "locked.fixture"));
        File.WriteAllText(file, "preserve");
        using var locked = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        world.WorldSettings.Name = "MustNotBeSaved";

        Assert.ThrowsAny<IOException>(() => Apply(world, desired, "Before"));

        Assert.Equal(before, ReadProject(world));
        Assert.Empty(ModProfileManager.LoadWorldProfile(world.DirectoryName)!.Packages);
        Assert.Single(Directory.GetDirectories(Path.Combine(_root, "Worlds")));
    }

    [Fact]
    public void AddingModCanDependOnBuiltInContent()
    {
        var world = CreateWorld(new ModProfile());
        var requirement = AddPackage("example.builtin", true, dependency: BuiltInContentMod.Manifest.Id);

        var clone = Apply(world, Profile(requirement), "Before");

        Assert.NotNull(clone);
        AssertPackage(requirement, ModProfileManager.LoadWorldProfile(world.DirectoryName)!);
    }

    [Fact]
    public void MissingTargetDependencyIsRejectedBeforeCloningOrWriting()
    {
        var world = CreateWorld(new ModProfile());
        var before = ReadProject(world);
        var requirement = AddPackage("example.dependency", true, dependency: "example.absent");

        Assert.Throws<ModDependencyException>(() => Apply(world, Profile(requirement), "Before"));

        Assert.Equal(before, ReadProject(world));
        Assert.Empty(ModProfileManager.LoadWorldProfile(world.DirectoryName)!.Packages);
        Assert.Single(Directory.GetDirectories(Path.Combine(_root, "Worlds")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdventureRestartUsesTheEditedWorldProfile(bool alreadyAdventure)
    {
        var world = CreateWorld(new ModProfile());
        world.WorldSettings.GameMode = GameMode.Adventure;
        if (alreadyAdventure) { _worlds.ChangeWorld(world.DirectoryName, world.WorldSettings, world.Hosting); }

        var requirement = AddPackage("example.adventure", true);
        var clone = Apply(world, Profile(requirement), "Before");
        Assert.NotNull(clone);
        _worlds.RestoreWorldFromSnapshot(world.DirectoryName, "AdventureRestart");
        AssertPackage(requirement, ModProfileManager.LoadWorldProfile(world.DirectoryName)!);
        Assert.Empty(ModProfileManager.LoadWorldProfile(clone.DirectoryName)!.Packages);
    }

    [Fact]
    public void InterruptedDirectoryReplacementMakesThePreviousWorldAvailableOnStartup()
    {
        var world = CreateWorld(new ModProfile());
        var previous = Path.Combine(_root, "Worlds.previous", Storage.GetFileName(world.DirectoryName));
        Directory.CreateDirectory(Path.GetDirectoryName(previous)!);
        Directory.Move(Storage.GetSystemPath(world.DirectoryName), previous);
        var reopened = new WorldRepository(
            Storage.GetDirectoryName(world.DirectoryName),
            ["World"],
            new WorldProjectActivity(),
            () => { }
        );
        Assert.Equal("Original", reopened.GetWorldInfo(world.DirectoryName)!.WorldSettings.Name);
        Assert.Empty(ModProfileManager.LoadWorldProfile(world.DirectoryName)!.Packages);
    }

    [Fact]
    public void FailedEditPreparationKeepsOriginalFilesAndTheCompletedClone()
    {
        var world = CreateWorld(new ModProfile());
        var before = ReadProject(world);
        var stagingPath = Path.Combine(_root, "Worlds.staging", Storage.GetFileName(world.DirectoryName));
        File.WriteAllText(stagingPath, "occupied");
        world.WorldSettings.Name = "ShouldNotBePublished";
        Assert.ThrowsAny<IOException>(() => Apply(world, Profile(AddPackage("example.add", true)), "Before"));

        Assert.Equal(before, ReadProject(world));
        Assert.Empty(ModProfileManager.LoadWorldProfile(world.DirectoryName)!.Packages);
        _worlds.UpdateWorldsList();
        Assert.Contains(_worlds.WorldInfos, info => info.WorldSettings.Name == "Before");
    }

    public void Dispose()
    {
        _runtime.Dispose();
        Directory.Delete(_root, true);
    }

    private WorldInfo CreateWorld(ModProfile profile)
    {
        return _worlds.CreateWorld(
            _runtime.Database,
            new WorldSettings { Name = "Original", Seed = "1234", GameMode = GameMode.Creative },
            new WorldHostingSettings(),
            profile
        );
    }

    private WorldInfo? Apply(WorldInfo world, ModProfile profile, string? cloneName)
    {
        return _worlds.ApplyWorldEdit(
            world.DirectoryName,
            world.WorldSettings,
            world.Hosting,
            ServerAdmissionPolicy.Open,
            profile,
            _packages,
            cloneName
        );
    }

    private ModPackageRequirement AddPackage(
        string id,
        bool canRemove,
        string version = "1.0.0",
        string? dependency = null
    )
    {
        var dependencies = dependency is null ? "[]" : $"[{{\"id\":\"{dependency}\"}}]";
        using var stream = ScpkgTestPackage.Create(
            $$"""
              {
                "id": "{{id}}",
                "name": "{{id}}",
                "version": "{{version}}",
                "scope": "both",
                "canRemoveFromWorld": {{canRemove.ToString().ToLowerInvariant()}},
                "dependencies": {{dependencies}}
              }
              """,
            new Dictionary<string, string> { ["data/blocks/marker.csv"] = "Id;DisplayName\ngame:air;Marker" }
        );
        var entry = _packages.AddPackage(stream);
        return new ModPackageRequirement
            { ModId = entry.ModId, Version = entry.Version, PackageHash = entry.PackageHash };
    }

    private static ModProfile Profile(params ModPackageRequirement[] packages) => new() { Packages = [.. packages] };

    private static void AssertPackage(ModPackageRequirement expected, ModProfile profile)
    {
        var actual = Assert.Single(profile.Packages);
        Assert.Equal(expected.ModId, actual.ModId);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.PackageHash, actual.PackageHash);
    }

    private static string ReadProject(WorldInfo world) =>
        File.ReadAllText(Storage.GetSystemPath(Storage.CombinePaths(world.DirectoryName, "Project.xml")));
}

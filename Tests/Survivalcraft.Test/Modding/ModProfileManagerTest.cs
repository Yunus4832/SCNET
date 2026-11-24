using System.Xml.Linq;

using Engine.Core.FileStorage;
using Engine.Network;

using Game.Application;
using Game.FileStorage;
using Game.Managers;
using Game.Modding;
using Game.Startup;
using Game.World;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class ModProfileManagerTest : IDisposable
{
    private const string _packageHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly FileBackup _globalProfileBackup = FileBackup.Create(ModProfileManager.GlobalProfilePath);

    private readonly DirectoryBackup _sessionProfilesBackup =
        DirectoryBackup.Create(Storage.CombinePaths(GamePaths.Config, "SessionProfiles"));

    private readonly DirectoryBackup _worldsBackup =
        DirectoryBackup.Create(GamePaths.Worlds);

    [Fact]
    public void PreparingSelectedWorldPreservesWorldListEntries()
    {
        var directory = CreateWorldDirectory("Selected");
        var worlds = ClientApplication.Worlds;
        worlds.UpdateWorldsList();
        var selected = worlds.WorldInfos.Single(world => world.DirectoryName == directory);
        ModRestartHelper.PrepareWorldSession(selected);
        Assert.Same(selected, worlds.WorldInfos.Single(world => world.DirectoryName == directory));
    }

    [Fact]
    public void ResolveStartupProfilePrefersSessionProfile()
    {
        SaveGlobalProfile(
            """
            <ModProfile Id="global">
              <Packages>
                <Package ModId="global.mod" Version="1.0.0" PackageHash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" />
              </Packages>
            </ModProfile>
            """
        );
        ModProfileManager.SaveSessionProfile(
            new ModProfile
            {
                Id = "session-a",
                Packages =
                [
                    new ModPackageRequirement
                    {
                        ModId = "session.mod",
                        Version = "2.0.0",
                        PackageHash = _packageHash
                    }
                ]
            }
        );

        var profile = ModProfileManager.ResolveStartupProfile(
            new SessionInfo { Target = SessionTarget.RemoteServer, SessionId = "session-a" }
        );

        Assert.Equal("session-a", profile.Id);
        var package = Assert.Single(profile.Packages);
        Assert.Equal("session.mod", package.ModId);
        Assert.Equal("2.0.0", package.Version);
        Assert.Equal(_packageHash, package.PackageHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemoteServerSessionProfileDoesNotMergeGlobalProfile(bool invitation)
    {
        SaveGlobalProfile(
            """
            <ModProfile Id="global">
              <Packages>
                <Package ModId="global.mod" Version="1.0.0" PackageHash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" />
              </Packages>
            </ModProfile>
            """
        );
        ModProfileManager.SaveSessionProfile(
            new ModProfile
            {
                Id = "remote-session",
                Packages =
                [
                    new ModPackageRequirement
                    {
                        ModId = "server.mod",
                        Version = "2.0.0",
                        PackageHash = _packageHash
                    }
                ]
            }
        );

        var profile = ModProfileManager.ResolveStartupProfile(
            new SessionInfo
            {
                SessionId = "remote-session",
                Target = SessionTarget.RemoteServer,
                RemoteTarget = invitation
                    ? new InvitationSessionNetworkTarget(
                        new Uri("https://signal.example/"),
                        Guid.NewGuid(),
                        RelayPolicy.Required
                    )
                    : new AddressNetworkTarget("127.0.0.1", 28887)
            }
        );

        Assert.Equal("remote-session", profile.Id);
        var package = Assert.Single(profile.Packages);
        Assert.Equal("server.mod", package.ModId);
        Assert.Equal("2.0.0", package.Version);
        Assert.Equal(_packageHash, package.PackageHash);
    }

    [Fact]
    public void LoadSessionProfileReturnsModProfile()
    {
        ModProfileManager.SaveSessionProfile(
            new ModProfile
            {
                Id = "session-b",
                Packages =
                [
                    new ModPackageRequirement
                    {
                        ModId = "session.mod",
                        Version = "2.0.0",
                        PackageHash = _packageHash
                    }
                ]
            }
        );

        var profile = ModProfileManager.LoadSessionProfile("session-b");

        Assert.NotNull(profile);
        Assert.Equal("session-b", profile!.Id);
        Assert.Equal("session.mod", Assert.Single(profile.Packages).ModId);
    }

    [Fact]
    public void UnpreparedRemoteStartupDoesNotLoadGlobalTemplate()
    {
        ModProfileManager.SaveGlobalProfile(ProfileWithPackage("global.mod"));
        DeleteDirectoryIfExists(Storage.CombinePaths(GamePaths.Config, "SessionProfiles"));

        var profile = ModProfileManager.ResolveStartupProfile(
            new SessionInfo { Target = SessionTarget.RemoteServer, SessionId = "missing" }
        );

        Assert.Equal("default", profile.Id);
        Assert.Empty(profile.Packages);
    }

    [Fact]
    public void SaveWorldProfileUsesWorldDirectorySidecar()
    {
        var worldDirectoryName = CreateWorldDirectory("WorldSidecar");
        ModProfileManager.SaveWorldProfile(
            worldDirectoryName,
            new ModProfile
            {
                Id = "ignored",
                Packages =
                [
                    new ModPackageRequirement
                    {
                        ModId = "world.mod",
                        Version = "1.0.0",
                        PackageHash = _packageHash
                    }
                ]
            }
        );

        var path = ModProfileManager.GetWorldProfilePath(worldDirectoryName);
        using var stream = Storage.OpenFile(path, OpenFileMode.Read);
        var root = XElement.Load(stream);

        Assert.Equal("ModProfile", root.Name.LocalName);
        Assert.Equal(Path.GetFileName(worldDirectoryName), root.Attribute(nameof(ModProfile.Id))?.Value);
    }

    [Theory]
    [InlineData(SessionTarget.World)]
    [InlineData(SessionTarget.HostedWorld)]
    public void ExistingWorldProfileOverridesLocalSessionAndGlobalProfiles(SessionTarget target)
    {
        ModProfileManager.SaveGlobalProfile(ProfileWithPackage("global.mod"));
        ModProfileManager.SaveSessionProfile(
            ModProfileManager.CreateSessionProfile("local-session", ProfileWithPackage("session.mod"))
        );
        var worldDirectoryName = CreateWorldDirectory("SavedWorld");
        ModProfileManager.SaveWorldProfile(
            worldDirectoryName,
            new ModProfile
            {
                Packages =
                [
                    new ModPackageRequirement
                    {
                        ModId = "world.mod",
                        Version = "2.0.0",
                        PackageHash = _packageHash
                    }
                ]
            }
        );

        var session = new SessionInfo { SessionId = "local-session", Target = target, World = "SavedWorld" };
        var profile = ModProfileManager.ResolveStartupProfile(session);

        var package = Assert.Single(profile.Packages);
        Assert.Equal("world.mod", package.ModId);
        Assert.Equal("2.0.0", package.Version);
        Assert.Equal(_packageHash, package.PackageHash);
    }

    [Theory]
    [InlineData(SessionTarget.World)]
    [InlineData(SessionTarget.HostedWorld)]
    public void EmptyWorldProfileDoesNotLoadGlobalOrSessionPackages(SessionTarget target)
    {
        ModProfileManager.SaveGlobalProfile(ProfileWithPackage("global.mod"));
        ModProfileManager.SaveSessionProfile(
            ModProfileManager.CreateSessionProfile("empty-world-session", ProfileWithPackage("session.mod"))
        );
        var directory = CreateWorldDirectory("EmptyWorld");

        var profile = ModProfileManager.ResolveStartupProfile(
            new SessionInfo { SessionId = "empty-world-session", Target = target, World = directory }
        );

        Assert.Empty(profile.Packages);
        Assert.Empty(ModProfileManager.LoadWorldProfile(directory)!.Packages);
    }

    [Fact]
    public void ExistingWorldWithoutProfileFailsInsteadOfUsingSessionOrGlobal()
    {
        ModProfileManager.SaveGlobalProfile(ProfileWithPackage("global.mod"));
        ModProfileManager.SaveSessionProfile(
            ModProfileManager.CreateSessionProfile("missing-profile-session", ProfileWithPackage("session.mod"))
        );
        var directory = CreateWorldDirectory("MissingProfile");
        ModProfileManager.DeleteWorldProfile(directory);

        var error = Assert.Throws<InvalidOperationException>(() =>
            ModProfileManager.ResolveStartupProfile(
                new SessionInfo
                    { SessionId = "missing-profile-session", Target = SessionTarget.World, World = directory }
            )
        );

        Assert.Contains(ModProfileManager.GetWorldProfilePath(directory), error.Message);
        Assert.False(Storage.FileExists(ModProfileManager.GetWorldProfilePath(directory)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MissingWorldUsesPreparedCreationProfileBeforeGlobalTemplate(bool prepared, bool empty)
    {
        ModProfileManager.SaveGlobalProfile(ProfileWithPackage("global.mod"));
        var sessionId = Guid.NewGuid().ToString("N");
        if (prepared)
        {
            ModProfileManager.SaveSessionProfile(
                ModProfileManager.CreateSessionProfile(
                    sessionId,
                    empty ? new ModProfile() : ProfileWithPackage("prepared.mod")
                )
            );
        }

        var session = new SessionInfo
        {
            SessionId = sessionId,
            Target = SessionTarget.World,
            World = "Uncreated-" + Guid.NewGuid().ToString("N")
        };
        var profile = ModProfileManager.ResolveStartupProfile(session);

        Assert.Equal(
            empty ? [] : new[] { prepared ? "prepared.mod" : "global.mod" },
            profile.Packages.Select(package => package.ModId)
        );
        Assert.False(Storage.DirectoryExists(Storage.CombinePaths(GamePaths.Worlds, session.World)));
    }

    [Theory]
    [InlineData(SessionTarget.MainMenu)]
    [InlineData(SessionTarget.WorldList)]
    [InlineData(SessionTarget.ServerBrowser)]
    public void PreWorldTargetsPreferExplicitSessionProfileIncludingEmpty(SessionTarget target)
    {
        ModProfileManager.SaveGlobalProfile(ProfileWithPackage("global.mod"));
        var session = new SessionInfo { Target = target, World = "EmptyPreWorld" };
        CreateWorldDirectory(session.World);
        session.SessionId = Guid.NewGuid().ToString("N");
        Assert.Equal("global.mod", Assert.Single(ModProfileManager.ResolveStartupProfile(session).Packages).ModId);
        ModProfileManager.SaveSessionProfile(
            ModProfileManager.CreateSessionProfile(session.SessionId, ProfileWithPackage("session.mod"))
        );
        Assert.Equal("session.mod", Assert.Single(ModProfileManager.ResolveStartupProfile(session).Packages).ModId);
        ModProfileManager.SaveSessionProfile(
            ModProfileManager.CreateSessionProfile(session.SessionId, new ModProfile())
        );
        Assert.Empty(ModProfileManager.ResolveStartupProfile(session).Packages);
    }

    [Fact]
    public void ResolveStartupProfileResolvesWorldProfileByWorldNameNotOnlyDirectoryName()
    {
        DeleteIfExists(ModProfileManager.GlobalProfilePath);
        var worldDirectoryName = CreateWorldDirectory(
            "VisibleWorldName",
            directoryName: "HiddenDirectoryName"
        );
        ModProfileManager.SaveWorldProfile(
            worldDirectoryName,
            new ModProfile
            {
                Packages =
                [
                    new ModPackageRequirement
                    {
                        ModId = "world.mod",
                        Version = "5.0.0",
                        PackageHash = _packageHash
                    }
                ]
            }
        );

        var profile = ModProfileManager.ResolveStartupProfile(
            new SessionInfo
            {
                SessionId = "missing-session",
                Target = SessionTarget.World,
                World = "VisibleWorldName"
            }
        );

        var package = Assert.Single(profile.Packages);
        Assert.Equal("world.mod", package.ModId);
        Assert.Equal("5.0.0", package.Version);
    }

    [Fact]
    public void ChangingGlobalTemplateDoesNotChangeSavedWorldProfile()
    {
        var worldDirectoryName = CreateWorldDirectory("IndependentWorld");
        ModProfileManager.SaveGlobalProfile(ProfileWithPackage("initial-global.mod"));
        ModProfileManager.SaveWorldProfile(worldDirectoryName, ProfileWithPackage("world.mod"));
        var original =
            File.ReadAllBytes(Storage.GetSystemPath(ModProfileManager.GetWorldProfilePath(worldDirectoryName)));
        SaveGlobalProfile(
            """
            <ModProfile Id="global">
              <Packages>
                <Package ModId="shared.mod" Version="1.0.0" PackageHash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" />
                <Package ModId="global.mod" Version="1.0.0" PackageHash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" />
              </Packages>
            </ModProfile>
            """
        );

        var profile = ModProfileManager.ResolveStartupProfile(
            new SessionInfo
            {
                SessionId = "missing-session",
                Target = SessionTarget.World,
                World = "IndependentWorld"
            }
        );

        Assert.Equal("world.mod", Assert.Single(profile.Packages).ModId);
        Assert.Equal(
            original,
            File.ReadAllBytes(Storage.GetSystemPath(ModProfileManager.GetWorldProfilePath(worldDirectoryName)))
        );
    }

    [Fact]
    public void SaveSessionProfileUsesDedicatedSessionRoot()
    {
        ModProfileManager.SaveSessionProfile(
            new ModProfile
            {
                Id = "session-c",
                Packages =
                [
                    new ModPackageRequirement
                    {
                        ModId = "session.mod",
                        Version = "3.0.0",
                        PackageHash = _packageHash
                    }
                ]
            }
        );

        var path = Storage.CombinePaths(GamePaths.Config, "SessionProfiles", "session-c.xml");
        using var stream = Storage.OpenFile(path, OpenFileMode.Read);
        var root = XElement.Load(stream);

        Assert.Equal("ModProfile", root.Name.LocalName);
        Assert.Equal("session-c", root.Attribute(nameof(ModProfile.Id))?.Value);
    }

    [Fact]
    public void SaveSessionProfileWritesPackageHash()
    {
        ModProfileManager.SaveSessionProfile(
            new ModProfile
            {
                Id = "session-with-hash",
                Packages =
                [
                    new ModPackageRequirement
                    {
                        ModId = "session.mod",
                        Version = "3.0.0",
                        PackageHash = _packageHash
                    }
                ]
            }
        );

        var path = Storage.CombinePaths(GamePaths.Config, "SessionProfiles", "session-with-hash.xml");
        using var stream = Storage.OpenFile(path, OpenFileMode.Read);
        var root = XElement.Load(stream);

        var package = Assert.Single(root.Element(nameof(ModProfile.Packages))!.Elements("Package"));
        Assert.Equal("session.mod", package.Attribute(nameof(ModPackageRequirement.ModId))?.Value);
        Assert.Equal("3.0.0", package.Attribute(nameof(ModPackageRequirement.Version))?.Value);
        Assert.Equal(_packageHash, package.Attribute(nameof(ModPackageRequirement.PackageHash))?.Value);
    }

    [Fact]
    public void LoadGlobalProfileRequiresPackageHashAttribute()
    {
        SaveGlobalProfile(
            """
            <ModProfile Id="global">
              <Packages>
                <Package ModId="legacy.mod" Version="1.0.0" PackageHash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" />
              </Packages>
            </ModProfile>
            """
        );

        var profile = ModProfileManager.LoadGlobalProfile();

        Assert.Equal(_packageHash, Assert.Single(profile.Packages).PackageHash);
    }

    public void Dispose()
    {
        _globalProfileBackup.Dispose();
        _sessionProfilesBackup.Dispose();
        _worldsBackup.Dispose();
    }

    private static void SaveGlobalProfile(string xml)
    {
        EnsureConfigDirectory();
        using var stream = Storage.OpenFile(ModProfileManager.GlobalProfilePath, OpenFileMode.Create);
        var root = XElement.Parse(xml);
        root.Save(stream);
    }

    private static void EnsureConfigDirectory()
    {
        if (!Storage.DirectoryExists(GamePaths.Config))
        {
            Storage.CreateDirectory(GamePaths.Config);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (Storage.FileExists(path))
        {
            Storage.DeleteFile(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Storage.DirectoryExists(path))
        {
            DeleteDirectoryRecursive(path);
        }
    }

    private static ModProfile ProfileWithPackage(string modId)
    {
        return new ModProfile
        {
            Packages =
            [
                new ModPackageRequirement { ModId = modId, Version = "1.0.0", PackageHash = _packageHash }
            ]
        };
    }

    private static string CreateWorldDirectory(string worldName, string? directoryName = null)
    {
        using var runtime = GameModRuntime.Start();
        runtime.InitializeDatabase();

        var worldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, directoryName ?? worldName);
        ClientApplication.Worlds.CreateWorld(
            runtime.Database,
            new WorldSettings { Name = worldName, Seed = "seed" },
            new WorldHostingSettings(),
            new ModProfile(),
            worldDirectoryName
        );

        return worldDirectoryName;
    }

    private sealed class FileBackup : IDisposable
    {
        private readonly string _path;

        private readonly string _backupPath;

        private readonly bool _hadOriginal;

        private FileBackup(string path, string backupPath, bool hadOriginal)
        {
            _path = path;
            _backupPath = backupPath;
            _hadOriginal = hadOriginal;
        }

        public static FileBackup Create(string path)
        {
            var backupPath = Storage.CombinePaths(GamePaths.Config, $"{Guid.NewGuid():N}.bak");
            var hadOriginal = Storage.FileExists(path);
            if (hadOriginal)
            {
                EnsureParentDirectory(backupPath);
                Storage.CopyFile(path, backupPath);
            }

            return new FileBackup(path, backupPath, hadOriginal);
        }

        public void Dispose()
        {
            if (Storage.FileExists(_path))
            {
                Storage.DeleteFile(_path);
            }

            if (_hadOriginal && Storage.FileExists(_backupPath))
            {
                EnsureParentDirectory(_path);
                Storage.CopyFile(_backupPath, _path);
                Storage.DeleteFile(_backupPath);
            }
        }
    }

    private sealed class DirectoryBackup : IDisposable
    {
        private readonly string _path;

        private readonly string _backupPath;

        private readonly bool _hadOriginal;

        private DirectoryBackup(string path, string backupPath, bool hadOriginal)
        {
            _path = path;
            _backupPath = backupPath;
            _hadOriginal = hadOriginal;
        }

        public static DirectoryBackup Create(string path)
        {
            var backupPath = Storage.CombinePaths(GamePaths.Config, $"{Guid.NewGuid():N}.dirbak");
            var hadOriginal = Storage.DirectoryExists(path);
            if (hadOriginal)
            {
                CopyDirectory(path, backupPath);
            }

            return new DirectoryBackup(path, backupPath, hadOriginal);
        }

        public void Dispose()
        {
            if (Storage.DirectoryExists(_path))
            {
                DeleteDirectoryRecursive(_path);
            }

            if (_hadOriginal && Storage.DirectoryExists(_backupPath))
            {
                CopyDirectory(_backupPath, _path);
                DeleteDirectoryRecursive(_backupPath);
            }
        }
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Storage.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Storage.DirectoryExists(directory))
        {
            Storage.CreateDirectory(directory);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Storage.DirectoryExists(destination))
        {
            Storage.CreateDirectory(destination);
        }

        foreach (var file in Storage.ListFileNames(source))
        {
            var sourcePath = Storage.CombinePaths(source, file);
            var destinationPath = Storage.CombinePaths(destination, file);
            Storage.CopyFile(sourcePath, destinationPath);
        }

        foreach (var directory in Storage.ListDirectoryNames(source))
        {
            CopyDirectory(
                Storage.CombinePaths(source, directory),
                Storage.CombinePaths(destination, directory)
            );
        }
    }

    private static void DeleteDirectoryRecursive(string path)
    {
        foreach (var file in Storage.ListFileNames(path))
        {
            Storage.DeleteFile(Storage.CombinePaths(path, file));
        }

        foreach (var directory in Storage.ListDirectoryNames(path))
        {
            DeleteDirectoryRecursive(Storage.CombinePaths(path, directory));
        }

        Storage.DeleteDirectory(path);
    }
}

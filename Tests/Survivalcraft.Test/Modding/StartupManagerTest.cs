using System.Reflection;

using Engine.Core.FileStorage;
using Engine.Network;
using Engine.Network.Security;

using Game.Application;
using Game.Commands;
using Game.Commands.BuiltIn;
using Game.FileStorage;
using Game.Managers;
using Game.Modding;
using Game.Startup;
using Game.Terrains;
using Game.World;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class StartupManagerTest : IDisposable
{
    private readonly FileBackup _runningSettingBackup = FileBackup.Create(StartupManager.Profiles.RunningSettingsPath);

    private readonly FileBackup _sessionInfoBackup = FileBackup.Create(StartupManager.Profiles.SessionsPath);

    public StartupManagerTest()
    {
        DeleteIfExists(StartupManager.Profiles.RunningSettingsPath);
        DeleteIfExists(StartupManager.Profiles.SessionsPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartupProfileRestartPreservesAndConsumesWorldCreationAndHostingIntent(bool existingWorld)
    {
        using var globalProfile = FileBackup.Create(ModProfileManager.GlobalProfilePath);

        var name = "RestartIntent" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "scnet-restart-profile-" + Guid.NewGuid().ToString("N"));
        string? initialSessionId = null;
        string? pendingSessionId = null;
        WorldInfo? world = null;
        try
        {
            using var mods = GameModRuntime.Start();
            mods.InitializeContentData();
            var startup = StartupManager.Load(
                [
                    "--session", name, "--world", name, "--host", "--player", "RestartPlayer"
                ]
            );
            initialSessionId = startup.Session.SessionId;
            if (existingWorld)
            {
                world = SessionInfoManager.ResolveWorldSession(startup.Session, true, false);
                Assert.NotNull(world);
                Assert.False(world.Hosting.RunServer);
            }

            ModProfileManager.SaveGlobalProfile(new ModProfile());
            ModProfileManager.SaveSessionProfile(
                new ModProfile
                {
                    Id = initialSessionId,
                    Packages =
                    [
                        new ModPackageRequirement
                        {
                            ModId = "test.previous", Version = "1.0.0", PackageHash = new string('a', 64)
                        }
                    ]
                }
            );
            // Exercise the same restart handoff without opening a test window.

            GameExitManager.BeginSession();
            Assert.True(
                StartupModProfileBootstrapper.EnsureStartupSessionProfile(initialSessionId, startup.Session, directory)
            );
            pendingSessionId = StartupManager.Profiles.LoadRunningSettings().PendingSessionId;
            Assert.False(string.IsNullOrWhiteSpace(pendingSessionId));
            var resumed = StartupManager.Load([]);
            Assert.False(resumed.Request.HasExplicitSession);
            Assert.False(resumed.Request.ForceWorldRunServer);
            Assert.True(resumed.Session.CreateWorldIfMissing);
            Assert.True(resumed.Session.ForceWorldRunServer);
            Assert.Equal(SessionTarget.World, resumed.Session.Target);
            Assert.Equal("RestartPlayer", resumed.Session.AutoJoinCharacterName);
            Assert.False(startup.Session.CreateWorldIfMissing);
            Assert.False(startup.Session.ForceWorldRunServer);
            world = SessionInfoManager.ResolveWorldSession(resumed.Session, false, false);
            Assert.NotNull(world);
            Assert.True(world.Hosting.RunServer);
            Assert.False(resumed.Session.CreateWorldIfMissing);
            Assert.False(resumed.Session.ForceWorldRunServer);
            Assert.True(ClientApplication.Worlds.GetWorldInfo(world.DirectoryName)!.Hosting.RunServer);
            world.Hosting.RunServer = false;
            ClientApplication.Worlds.ChangeWorld(world.DirectoryName, world.WorldSettings, world.Hosting);
            Assert.False(SessionInfoManager.ResolveWorldSession(resumed.Session, false, false)!.Hosting.RunServer);
            ClientApplication.Worlds.DeleteWorld(world.DirectoryName);
            world = null;
            Assert.Null(SessionInfoManager.ResolveWorldSession(resumed.Session, false, false));
        }
        finally
        {
            if (world is not null) { ClientApplication.Worlds.DeleteWorld(world.DirectoryName); }

            if (initialSessionId is not null) { ModProfileManager.DeleteSessionProfile(initialSessionId); }

            if (pendingSessionId is not null) { ModProfileManager.DeleteSessionProfile(pendingSessionId); }

            if (Directory.Exists(directory)) { Directory.Delete(directory, true); }

            GameExitManager.BeginSession();
        }
    }

    [Fact]
    public void ServerIdentityOverridePersistsOnlyWhenSavedAndSurvivesRestart()
    {
        using var first = NetworkIdentity.Create();
        using var second = NetworkIdentity.Create();
        var firstAddress = first.PublicIdentity.Address;
        var secondAddress = second.PublicIdentity.Address;
        StartupManager.Load(
            ["--session", "server-key", "--world", "World", "--server-identity", firstAddress, "--save"]
        );
        Assert.Equal(firstAddress, StartupManager.Profiles.LoadSessionByName("server-key")!.ServerIdentityAddress);
        var restored = StartupManager.Load(["--session", "server-key"]);
        Assert.Equal(firstAddress, restored.Session.ServerIdentityAddress);
        var overridden = StartupManager.Load(["--session", "server-key", "--server-identity", secondAddress]);
        Assert.Equal(secondAddress, overridden.Session.ServerIdentityAddress);
        Assert.Equal(firstAddress, StartupManager.Profiles.LoadSessionByName("server-key")!.ServerIdentityAddress);
        var captured = SessionInfoManager.CaptureCurrentSession();
        var pending = SessionInfoManager.PrepareRestartSession(captured);
        Assert.Equal(secondAddress, StartupManager.Profiles.LoadSession(pending.SessionId).ServerIdentityAddress);
    }

    [Fact]
    public void InvalidServerIdentityOverrideFailsInsteadOfUsingDefault()
    {
        Assert.Throws<FormatException>(() => StartupManager.Load(["--server-identity", "invalid"]));
        Assert.Throws<ArgumentException>(() => StartupManager.Load(["--server-identity"]));
    }

    [Fact]
    public void CorruptSavedServerIdentityDoesNotSilentlyFallback()
    {
        using var identity = NetworkIdentity.Create();
        var startup = StartupManager.Load(
            ["--session", "bad-key", "--server-identity", identity.PublicIdentity.Address, "--save"]
        );
        var xml = System.Xml.Linq.XElement.Parse(Storage.ReadAllText(StartupManager.Profiles.SessionsPath));
        xml.Elements("SessionInfo").Single(item => item.Attribute("SessionId")!.Value == startup.Session.SessionId)
            .SetAttributeValue(nameof(SessionInfo.ServerIdentityAddress), "invalid");
        Storage.WriteAllText(StartupManager.Profiles.SessionsPath, xml.ToString());
        Assert.Throws<InvalidDataException>(() => StartupManager.Profiles.LoadSessionByName("bad-key"));
    }

    [Fact]
    public void CapturingAnUnsavedSessionKeepsEffectiveAutomationAndTransportOverrides()
    {
        var startup = StartupManager.Load(
            [
                "--session", "capture-effective", "--connect", "game.example:28887", "--player", "ResumeTester",
                "--http-command", "--http-command-port", "35711", "--p2p", "true", "--room-name", "Transient room"
            ]
        );
        var captured = SessionInfoManager.CaptureCurrentSession();
        Assert.True(captured.HttpCommandEnabled);
        Assert.Equal(35711, captured.HttpCommandPort);
        Assert.Equal("ResumeTester", captured.AutoJoinCharacterName);
        Assert.Equal(startup.Session.P2POverrides, captured.P2POverrides);
        var pending = SessionInfoManager.PrepareRestartSession(captured);
        var restored = StartupManager.Profiles.LoadSession(pending.SessionId);
        Assert.True(restored.HttpCommandEnabled);
        Assert.Equal(35711, restored.HttpCommandPort);
        Assert.Equal("ResumeTester", restored.AutoJoinCharacterName);
        Assert.Equal(startup.Session.P2POverrides, restored.P2POverrides);
    }

    [Fact]
    public void ModRestartRestoresTheRemoteSessionWithoutClientAuthenticationInitialization()
    {
        var currentField = typeof(AuthenticationManager).GetField(
            "_current",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var previousManager = currentField.GetValue(null);

        var pending = string.Empty;
        try
        {
            currentField.SetValue(null, null);
            var startup = StartupManager.Load(
                ["--session", "mod-restart", "--connect", "game.example:28887", "--player", "ResumeTester"]
            );

            GameExitManager.BeginSession();
            GameExitManager.RequestRestart(startup.Session, new ModProfile());
            pending = StartupManager.Profiles.CurrentRunningSettings.PendingSessionId;
            Assert.NotEmpty(pending);
            Assert.Equal(GameExitAction.Restart, GameExitManager.ExitAction);
            var restored = StartupManager.Load([]);
            Assert.Equal(startup.Session.RemoteTarget, restored.Session.RemoteTarget);
            Assert.Equal("ResumeTester", restored.Session.AutoJoinCharacterName);
            Assert.Null(currentField.GetValue(null));
        }
        finally
        {
            currentField.SetValue(null, previousManager);

            GameExitManager.BeginSession();
            if (pending.Length > 0) { ModProfileManager.DeleteSessionProfile(pending); }
        }
    }

    [Fact]
    public void UnreadableAdmissionSessionDoesNotFallBackToOpenDefaults()
    {
        var session = SaveSession("locked-policy");
        using var locked = new FileStream(
            Storage.GetSystemPath(StartupManager.Profiles.SessionsPath),
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None
        );
        Assert.Throws<InvalidDataException>(() => StartupManager.Profiles.LoadSession(session.SessionId));
        Assert.Throws<InvalidDataException>(() => StartupManager.Profiles.LoadSessionByName(session.Name));
        Assert.Throws<InvalidDataException>(() =>
            StartupManager.Profiles.SessionExists(session.SessionId)
        );
    }

    [Fact]
    public void MalformedSessionDoesNotCreateAnOpenReplacement()
    {
        var session = SaveSession("malformed-policy");
        File.WriteAllText(Storage.GetSystemPath(StartupManager.Profiles.SessionsPath), "<Sessions><SessionInfo");
        Assert.Throws<InvalidDataException>(() =>
            StartupManager.Load(["--session", session.Name])
        );
    }

    [Fact]
    public void RoomOverridesAreTransientUnlessSavedAndPreserveEmptyDescription()
    {
        var session = SaveSession("room-overrides");
        var args = new[]
        {
            "--session", "room-overrides", "--p2p", "false",
            "--rendezvous", "https://signal.example/", "--room-name", "Room",
            "--room-description", "", "--room-public", "false"
        };
        var transient = StartupManager.Load(args);
        Assert.False(transient.Session.P2POverrides.Enabled);
        Assert.Equal(string.Empty, transient.Session.P2POverrides.Description);
        Assert.Null(StartupManager.Profiles.LoadSession(session.SessionId).P2POverrides.Enabled);
        StartupManager.Load([.. args, "--save"]);
        var restored = StartupManager.Load(["--session", "room-overrides"]);
        Assert.Equal(transient.Session.P2POverrides, restored.Session.P2POverrides);
        var changed = StartupManager.Load(["--session", "room-overrides", "--p2p", "true"]);
        Assert.True(changed.Session.P2POverrides.Enabled);
        Assert.Equal("Room", changed.Session.P2POverrides.Name);
        Assert.False(StartupManager.Profiles.LoadSession(session.SessionId).P2POverrides.Enabled);
        var restart = SessionInfoManager.PrepareRestartSession(changed.Session);
        Assert.Equal(changed.Session.P2POverrides, StartupManager.Profiles.LoadSession(restart.SessionId).P2POverrides);
    }

    [Fact]
    public void AdmissionSessionOverridesAreRecoverableOnlyWhenExplicitlySaved()
    {
        var saved = SaveSession("admission-config");
        var path = Path.Combine(Path.GetTempPath(), "scnet-password-reference");
        var args = new[] { "--session", "admission-config", "--admission", "password", "--server-password-file", path };
        var temporary = StartupManager.Load(args);
        Assert.Equal(ServerAdmissionMode.Password, temporary.Session.AdmissionOverrides.Mode);
        Assert.Equal(path, temporary.Session.AdmissionOverrides.PasswordFile);
        Assert.True(StartupManager.Profiles.LoadSession(saved.SessionId).AdmissionOverrides.IsEmpty);
        StartupManager.Load([.. args, "--save"]);
        var restored = StartupManager.Load(["--session", "admission-config"]);
        Assert.Equal(path, restored.Session.AdmissionOverrides.PasswordFile);
        var changed = StartupManager.Load(["--session", "admission-config", "--admission", "open"]);
        Assert.Equal(ServerAdmissionMode.Open, changed.Session.AdmissionOverrides.Mode);
        Assert.Null(changed.Session.AdmissionOverrides.PasswordFile);
        Assert.Equal(path, StartupManager.Profiles.LoadSession(saved.SessionId).AdmissionOverrides.PasswordFile);
        var pending = SessionInfoManager.PrepareRestartSession(restored.Session);
        Assert.Equal(
            restored.Session.AdmissionOverrides,
            StartupManager.Profiles.LoadSession(pending.SessionId).AdmissionOverrides
        );
        var running = Storage.ReadAllText(StartupManager.Profiles.RunningSettingsPath);
        Assert.DoesNotContain("server-password-file", running);
        Assert.DoesNotContain(path, running);
    }

    [Fact]
    public void AdmissionEnvironmentIsInvocationIntentAndExplicitCliModeTakesPrecedence()
    {
        var variables = new[]
        {
            "SCNET_ADMISSION", "SCNET_SERVER_PASSWORD_FILE", "SCNET_AUTHENTICATION_URL",
            "SCNET_AUTHENTICATION_KEY", "SCNET_AUTHENTICATION_NAME", "SCNET_SERVER_PASSWORD"
        };
        var previous = variables.ToDictionary(variable => variable, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var variable in variables) { Environment.SetEnvironmentVariable(variable, null); }

            Environment.SetEnvironmentVariable("SCNET_ADMISSION", "password");
            Environment.SetEnvironmentVariable(
                "SCNET_SERVER_PASSWORD_FILE",
                Path.Combine(Path.GetTempPath(), "only-a-file-reference")
            );
            var inherited = StartupManager.Load([]);
            Assert.Equal(
                ServerAdmissionMode.Password,
                inherited.Session.AdmissionOverrides.Mode
            );
            var explicitOpen = StartupManager.Load(["--admission", "open"]);
            Assert.Equal(
                ServerAdmissionMode.Open,
                explicitOpen.Session.AdmissionOverrides.Mode
            );
            Assert.Null(explicitOpen.Session.AdmissionOverrides.PasswordFile);
            Environment.SetEnvironmentVariable("SCNET_SERVER_PASSWORD", "not-accepted");
            Assert.Throws<Game.Admission.AdmissionConfigurationException>(() => StartupManager.Load([]));
        }
        finally
        {
            foreach (var variable in variables) { Environment.SetEnvironmentVariable(variable, previous[variable]); }
        }
    }

    [Theory]
    [InlineData("--admission", "invalid")]
    [InlineData("--server-password", "must-not-be-accepted")]
    [InlineData("--SERVER-PASSWORD", "must-not-be-accepted")]
    [InlineData("--authentication-url", "http://not-secure.example/")]
    public void UnsafeOrInvalidAdmissionFlagsNeverBecomeUnknownSavedArguments(string flag, string value)
    {
        Assert.Throws<Game.Admission.AdmissionConfigurationException>(() => StartupManager.Load([flag, value]));
    }

    [Theory]
    [InlineData("--p2p")]
    [InlineData("--room-public")]
    public void InvalidRoomBooleanIsRejected(string option)
    {
        Assert.Throws<ArgumentException>(() => StartupManager.Load([option, "invalid"]));
    }

    [Fact]
    public void LoadUsesPendingSessionBeforeDefaultSession()
    {
        var defaultSession = SaveSession("default-session");
        var pendingSession = SaveSession("pending-session");
        StartupManager.Profiles.SaveRunningSettings(
            new RunningSetting
            {
                DefaultSessionId = defaultSession.SessionId,
                PendingSessionId = pendingSession.SessionId
            }
        );

        var startup = StartupManager.Load([]);

        Assert.Equal(defaultSession.SessionId, startup.Settings.DefaultSessionId);
        Assert.Equal(pendingSession.SessionId, startup.Settings.PendingSessionId);
        Assert.Equal(pendingSession.SessionId, startup.Session.SessionId);
    }

    [Fact]
    public void FlatTerrainOverridesRoundTripOnlyWhenSaved()
    {
        var session = SaveSession("flat-automation");
        var args = new[]
        {
            "--session", "flat-automation", "--terrain", "FlatContinent", "--terrain-level", "72"
        };
        var transient = StartupManager.Load(args);
        Assert.Equal(TerrainGenerationMode.FlatContinent, transient.Session.TerrainGenerationMode);
        Assert.Equal(72, transient.Session.TerrainLevel);
        Assert.Null(StartupManager.Profiles.LoadSession(session.SessionId).TerrainGenerationMode);
        StartupManager.Load([.. args, "--save"]);
        var restored = StartupManager.Load(["--session", "flat-automation"]);
        Assert.Equal(TerrainGenerationMode.FlatContinent, restored.Session.TerrainGenerationMode);
        Assert.Equal(72, restored.Session.TerrainLevel);
    }

    [Theory]
    [InlineData("--terrain", "LegacyContinent23")]
    [InlineData("--terrain", "unknown")]
    [InlineData("--terrain-level", "1")]
    [InlineData("--terrain-level", "253")]
    public void InvalidTerrainCreationParametersAreRejected(string option, string value)
    {
        Assert.Throws<ArgumentException>(() => StartupManager.Load(["--session", "flat", option, value]));
    }

    [Fact]
    public void ExplicitSessionOverridesAreTransientWithoutSave()
    {
        var defaultSession = SaveSession("default-session");
        StartupManager.Profiles.SaveRunningSettings(new RunningSetting { DefaultSessionId = defaultSession.SessionId });
        var alpha = SaveSession("alpha", SessionTarget.World, "AlphaWorld");

        var startup = StartupManager.Load(
            [
                "--session", "alpha",
                "--world", "CustomWorld",
                "--seed", "42",
                "--game-mode", "Creative"
            ]
        );

        Assert.Equal(defaultSession.SessionId, startup.Settings.DefaultSessionId);
        Assert.Equal(alpha.SessionId, startup.Session.SessionId);
        Assert.True(startup.Request.HasExplicitSession);
        Assert.Equal("alpha", startup.Request.SessionName);
        Assert.Equal("CustomWorld", startup.Request.World);
        Assert.Equal("42", startup.Request.Seed);
        Assert.Equal(GameMode.Creative, startup.Request.GameMode);
        Assert.Equal(GameMode.Creative, startup.Session.GameMode);
        Assert.Equal("AlphaWorld", StartupManager.Profiles.LoadSession(alpha.SessionId).World);
    }

    [Fact]
    public void ConnectAndPlayerOverridesAreTransientWithoutSave()
    {
        var startup = StartupManager.Load(
            [
                "--session", "client-smoke",
                "--connect", "127.0.0.1:28987",
                "--player", "DebugPlayer",
                "--server-port", "28987",
                "--broadcast-port", "28988"
            ]
        );

        Assert.Equal(SessionTarget.RemoteServer, startup.Session.Target);
        Assert.Equal("127.0.0.1", Assert.IsType<AddressNetworkTarget>(startup.Session.RemoteTarget).Host);
        Assert.Equal(28987, startup.Session.ServerPort);
        Assert.Equal("DebugPlayer", startup.Request.AutoJoinCharacterName);
        Assert.Equal("DebugPlayer", startup.Session.AutoJoinCharacterName);
        Assert.Equal(28988, startup.Session.BroadcastPort);
        Assert.False(startup.Request.Save);
        Assert.Null(StartupManager.Profiles.LoadSessionByName("client-smoke"));
    }

    [Fact]
    public void SessionOverridesRequireExplicitSession()
    {
        var startup = StartupManager.Load(
            [
                "--world", "IgnoredWorld",
                "--seed", "42",
                "--game-mode", "Creative"
            ]
        );

        Assert.Null(startup.Request.World);
        Assert.Null(startup.Request.Seed);
        Assert.Null(startup.Request.GameMode);
        Assert.Null(startup.Session.GameMode);
    }

    [Fact]
    public void SavePersistsEffectiveSessionButDoesNotReplaceDefaultSession()
    {
        var defaultSession = SaveSession("default-session");
        StartupManager.Profiles.SaveRunningSettings(new RunningSetting { DefaultSessionId = defaultSession.SessionId });

        var startup = StartupManager.Load(
            [
                "--session", "saved-session",
                "--connect", "localhost:29987",
                "--game-mode", "Challenging",
                "--save"
            ]
        );

        var persistedSettings = StartupManager.Profiles.LoadRunningSettings();
        var saved = StartupManager.Profiles.LoadSessionByName("saved-session");
        Assert.Equal(defaultSession.SessionId, persistedSettings.DefaultSessionId);
        Assert.True(startup.Request.Save);
        Assert.NotNull(saved);
        Assert.Equal(startup.Session.SessionId, saved!.SessionId);
        Assert.Equal(SessionTarget.RemoteServer, saved.Target);
        Assert.Equal("localhost", Assert.IsType<AddressNetworkTarget>(saved.RemoteTarget).Host);
        Assert.Equal(29987, Assert.IsType<AddressNetworkTarget>(saved.RemoteTarget).Port);
        Assert.Equal(GameMode.Challenging, saved.GameMode);
    }

    [Fact]
    public void GuiHostUsesTransientRequestAndEffectiveSessionPorts()
    {
        var startup = StartupManager.Load(
            [
                "--host",
                "--session", "gui-server",
                "--world", "GuiServerWorld",
                "--player", "HostPlayer",
                "--server-port", "30987",
                "--broadcast-port", "30988"
            ]
        );

        Assert.True(startup.Request.ForceWorldRunServer);
        Assert.Equal("HostPlayer", startup.Request.AutoJoinCharacterName);
        Assert.Equal("HostPlayer", startup.Session.AutoJoinCharacterName);
        Assert.Equal(SessionTarget.World, startup.Session.Target);
        Assert.Equal("GuiServerWorld", startup.Session.World);
        Assert.Equal(30987, startup.Session.ServerPort);
        Assert.Equal(30988, startup.Session.BroadcastPort);
        Assert.Null(StartupManager.Profiles.LoadSessionByName("gui-server"));
    }

    [Fact]
    public void ConnectTakesPriorityOverHost()
    {
        var startup = StartupManager.Load(
            [
                "--host",
                "--session", "remote-client",
                "--connect", "127.0.0.1:31987"
            ]
        );

        Assert.False(startup.Request.ForceWorldRunServer);
        Assert.Equal(SessionTarget.RemoteServer, startup.Session.Target);
        Assert.Equal("127.0.0.1", Assert.IsType<AddressNetworkTarget>(startup.Session.RemoteTarget).Host);
        Assert.Equal(31987, Assert.IsType<AddressNetworkTarget>(startup.Session.RemoteTarget).Port);
    }

    [Fact]
    public void MissingSessionNameDoesNotCreateExplicitRequest()
    {
        var startup = StartupManager.Load(["--session"]);

        Assert.False(startup.Request.HasExplicitSession);
        Assert.Equal(string.Empty, startup.Request.SessionName);
        Assert.True(Guid.TryParse(startup.Session.SessionId, out _));
    }

    [Fact]
    public void LoadWithoutConfiguredSessionGeneratesTransientSession()
    {
        StartupManager.Profiles.SaveRunningSettings(new RunningSetting());

        var startup = StartupManager.Load([]);

        Assert.True(Guid.TryParse(startup.Session.SessionId, out _));
        Assert.Equal(string.Empty, StartupManager.Profiles.LoadRunningSettings().DefaultSessionId);
    }

    [Fact]
    public void LoadUsesConfiguredDefaultSession()
    {
        var defaultSession = SaveSession("default-session");
        StartupManager.Profiles.SaveRunningSettings(new RunningSetting { DefaultSessionId = defaultSession.SessionId });

        var startup = StartupManager.Load([]);

        Assert.Equal(defaultSession.SessionId, startup.Session.SessionId);
    }

    [Fact]
    public void SaveWithoutSpecifiedSessionPersistsGeneratedSession()
    {
        var startup = StartupManager.Load(["--save"]);

        Assert.True(Guid.TryParse(startup.Session.SessionId, out _));
        var saved = StartupManager.Profiles.LoadSession(startup.Session.SessionId);
        Assert.Equal(startup.Session.SessionId, saved.SessionId);
        Assert.Equal(SessionTarget.MainMenu, saved.Target);
    }

    [Fact]
    public void RestartCommandWithoutSessionUsesDefaultRestartLogic()
    {
        try
        {
            StartupManager.Profiles.SaveRunningSettings(new RunningSetting());

            GameExitManager.BeginSession();
            var registry = new CommandRegistry();
            Fixtures.BuiltInCommandTestRegistry.Register(registry, new ModId("game"));
            registry.Freeze();

            var result = new CommandDispatcher(registry).Execute(
                new RestartApplicationCommand(),
                new CommandContext(
                    CommandInvocationChannel.UserInterface,
                    CommandPrincipal.LocalOperator,
                    null,
                    "run-mode-test"
                )
            );

            var settings = StartupManager.Profiles.LoadRunningSettings();
            Assert.True(result.Success, Game.Localization.ClientLanguage.Resolver.Resolve(result.Message));
            Assert.Equal(GameExitAction.Restart, GameExitManager.ExitAction);

            Assert.Equal(string.Empty, settings.PendingSessionId);
        }
        finally
        {
            GameExitManager.BeginSession();
        }
    }

    [Fact]
    public void RestartCommandUsesProvidedRestartSession()
    {
        try
        {
            StartupManager.Profiles.SaveRunningSettings(new RunningSetting());

            GameExitManager.BeginSession();
            var registry = new CommandRegistry();
            Fixtures.BuiltInCommandTestRegistry.Register(registry, new ModId("game"));
            registry.Freeze();

            var result = new CommandDispatcher(registry).Execute(
                new RestartApplicationCommand(
                    new SessionInfo
                    {
                        Target = SessionTarget.World,
                        World = "RestartWorld"
                    }
                ),
                new CommandContext(
                    CommandInvocationChannel.UserInterface,
                    CommandPrincipal.LocalOperator,
                    null,
                    "run-mode-session-test"
                )
            );

            var settings = StartupManager.Profiles.LoadRunningSettings();
            var pendingSession = StartupManager.Profiles.LoadSession(settings.PendingSessionId);
            Assert.True(result.Success, Game.Localization.ClientLanguage.Resolver.Resolve(result.Message));
            Assert.Equal(GameExitAction.Restart, GameExitManager.ExitAction);

            Assert.Equal(SessionTarget.World, pendingSession.Target);
            Assert.Equal("RestartWorld", pendingSession.World);
        }
        finally
        {
            GameExitManager.BeginSession();
        }
    }

    [Fact]
    public void LoadClearsInvalidOrMissingPersistedSessionReferences()
    {
        StartupManager.Profiles.SaveRunningSettings(
            new RunningSetting
            {
                DefaultSessionId = "not-a-guid",
                PendingSessionId = Guid.NewGuid().ToString("N")
            }
        );

        var startup = StartupManager.Load([]);

        Assert.Equal(string.Empty, startup.Settings.DefaultSessionId);
        Assert.Equal(string.Empty, startup.Settings.PendingSessionId);
        Assert.True(Guid.TryParse(startup.Session.SessionId, out _));
    }

    public void Dispose()
    {
        _runningSettingBackup.Dispose();
        _sessionInfoBackup.Dispose();
    }

    private static SessionInfo SaveSession(
        string name,
        SessionTarget target = SessionTarget.MainMenu,
        string world = ""
    )
    {
        var session = new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Name = name,
            Target = target,
            World = world
        };
        StartupManager.Profiles.SaveSession(session);
        return session;
    }

    private static void DeleteIfExists(string path)
    {
        if (Storage.FileExists(path))
        {
            Storage.DeleteFile(path);
        }
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
                Storage.CopyFile(path, backupPath);
            }

            return new FileBackup(path, backupPath, hadOriginal);
        }

        public void Dispose()
        {
            DeleteIfExists(_path);
            if (_hadOriginal && Storage.FileExists(_backupPath))
            {
                Storage.CopyFile(_backupPath, _path);
            }

            DeleteIfExists(_backupPath);
        }
    }
}

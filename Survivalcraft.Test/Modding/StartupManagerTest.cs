using System.Reflection;

using Engine.Core.Runtime;
using Engine.FileStorage;
using Engine.Network;

using Game;
using Game.Commands;
using Game.Managers;
using Game.Modding;
using Game.Startup;
using Game.Terrains;
using Game.World;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class StartupManagerTest : IDisposable
{
    private readonly FileBackup _runningSettingBackup = FileBackup.Create(RunningSettingManager.RunningSettingPath);

    private readonly FileBackup _sessionInfoBackup = FileBackup.Create(SessionInfoManager.SessionInfoPath);

    public StartupManagerTest()
    {
        DeleteIfExists(RunningSettingManager.RunningSettingPath);
        DeleteIfExists(SessionInfoManager.SessionInfoPath);
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
        var restored = SessionInfoManager.Load(pending.SessionId);
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
        var previousMode = RunMode.Value;
        var pending = string.Empty;
        try
        {
            currentField.SetValue(null, null);
            var startup = StartupManager.Load(
                ["--session", "mod-restart", "--connect", "game.example:28887", "--player", "ResumeTester"]
            );
            RunMode.Value = RunModeType.HeadlessServer;
            GameExitManager.BeginSession();
            GameExitManager.RequestRestart(startup.Session, new ModProfile());
            pending = RunningSettingManager.Current.PendingSessionId;
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
            RunMode.Value = previousMode;
            GameExitManager.BeginSession();
            if (pending.Length > 0) { ModProfileManager.DeleteSessionProfile(pending); }
        }
    }

    [Fact]
    public void UnreadableAdmissionSessionDoesNotFallBackToOpenDefaults()
    {
        var session = SaveSession("locked-policy");
        using var locked = new FileStream(
            Storage.GetSystemPath(SessionInfoManager.SessionInfoPath),
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None
        );
        Assert.Throws<Game.Admission.AdmissionConfigurationException>(() => SessionInfoManager.Load(session.SessionId));
        Assert.Throws<Game.Admission.AdmissionConfigurationException>(() => SessionInfoManager.LoadByName(session.Name)
        );
        Assert.Throws<Game.Admission.AdmissionConfigurationException>(() =>
            SessionInfoManager.SessionExists(session.SessionId)
        );
    }

    [Fact]
    public void MalformedSessionDoesNotCreateAnOpenReplacement()
    {
        var session = SaveSession("malformed-policy");
        File.WriteAllText(Storage.GetSystemPath(SessionInfoManager.SessionInfoPath), "<Sessions><SessionInfo");
        Assert.Throws<Game.Admission.AdmissionConfigurationException>(() =>
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
        Assert.Null(SessionInfoManager.Load(session.SessionId).P2POverrides.Enabled);
        StartupManager.Load([.. args, "--save"]);
        var restored = StartupManager.Load(["--session", "room-overrides"]);
        Assert.Equal(transient.Session.P2POverrides, restored.Session.P2POverrides);
        var changed = StartupManager.Load(["--session", "room-overrides", "--p2p", "true"]);
        Assert.True(changed.Session.P2POverrides.Enabled);
        Assert.Equal("Room", changed.Session.P2POverrides.Name);
        Assert.False(SessionInfoManager.Load(session.SessionId).P2POverrides.Enabled);
        var restart = SessionInfoManager.PrepareRestartSession(changed.Session);
        Assert.Equal(changed.Session.P2POverrides, SessionInfoManager.Load(restart.SessionId).P2POverrides);
    }

    [Fact]
    public void AdmissionSessionOverridesAreRecoverableOnlyWhenExplicitlySaved()
    {
        var saved = SaveSession("admission-config");
        var path = Path.Combine(Path.GetTempPath(), "scnet-password-reference");
        var args = new[] { "--session", "admission-config", "--admission", "password", "--server-password-file", path };
        var temporary = StartupManager.Load(args);
        Assert.Equal(Game.Protocol.Connection.ServerAdmissionMode.Password, temporary.Session.AdmissionOverrides.Mode);
        Assert.Equal(path, temporary.Session.AdmissionOverrides.PasswordFile);
        Assert.True(SessionInfoManager.Load(saved.SessionId).AdmissionOverrides.IsEmpty);
        StartupManager.Load([.. args, "--save"]);
        var restored = StartupManager.Load(["--session", "admission-config"]);
        Assert.Equal(path, restored.Session.AdmissionOverrides.PasswordFile);
        var changed = StartupManager.Load(["--session", "admission-config", "--admission", "open"]);
        Assert.Equal(Game.Protocol.Connection.ServerAdmissionMode.Open, changed.Session.AdmissionOverrides.Mode);
        Assert.Null(changed.Session.AdmissionOverrides.PasswordFile);
        Assert.Equal(path, SessionInfoManager.Load(saved.SessionId).AdmissionOverrides.PasswordFile);
        var pending = SessionInfoManager.PrepareRestartSession(restored.Session);
        Assert.Equal(
            restored.Session.AdmissionOverrides,
            SessionInfoManager.Load(pending.SessionId).AdmissionOverrides
        );
        var running = Engine.FileStorage.Storage.ReadAllText(RunningSettingManager.RunningSettingPath);
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
                Game.Protocol.Connection.ServerAdmissionMode.Password,
                inherited.Session.AdmissionOverrides.Mode
            );
            var explicitOpen = StartupManager.Load(["--admission", "open"]);
            Assert.Equal(
                Game.Protocol.Connection.ServerAdmissionMode.Open,
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
        RunningSettingManager.Save(
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
        Assert.Null(SessionInfoManager.Load(session.SessionId).TerrainGenerationMode);
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
        RunningSettingManager.Save(new RunningSetting { DefaultSessionId = defaultSession.SessionId });
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
        Assert.Equal("AlphaWorld", SessionInfoManager.Load(alpha.SessionId).World);
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
        Assert.Null(SessionInfoManager.LoadByName("client-smoke"));
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
        RunningSettingManager.Save(new RunningSetting { DefaultSessionId = defaultSession.SessionId });

        var startup = StartupManager.Load(
            [
                "--session", "saved-session",
                "--connect", "localhost:29987",
                "--game-mode", "Challenging",
                "--save"
            ]
        );

        var persistedSettings = RunningSettingManager.Load();
        var saved = SessionInfoManager.LoadByName("saved-session");
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
                "--gui", "--host",
                "--session", "gui-server",
                "--world", "GuiServerWorld",
                "--player", "HostPlayer",
                "--server-port", "30987",
                "--broadcast-port", "30988"
            ]
        );

        Assert.Equal(RunModeType.Gui, startup.Settings.RunMode);
        Assert.True(startup.Request.ForceWorldRunServer);
        Assert.Equal("HostPlayer", startup.Request.AutoJoinCharacterName);
        Assert.Equal("HostPlayer", startup.Session.AutoJoinCharacterName);
        Assert.Equal(SessionTarget.World, startup.Session.Target);
        Assert.Equal("GuiServerWorld", startup.Session.World);
        Assert.Equal(30987, startup.Session.ServerPort);
        Assert.Equal(30988, startup.Session.BroadcastPort);
        Assert.Null(SessionInfoManager.LoadByName("gui-server"));
    }

    [Fact]
    public void ConnectTakesPriorityOverHost()
    {
        var startup = StartupManager.Load(
            [
                "--gui", "--host",
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

    [Theory]
    [InlineData(RunModeType.Gui)]
    [InlineData(RunModeType.HeadlessServer)]
    public void LoadWithoutConfiguredSessionGeneratesTransientSession(RunModeType runMode)
    {
        RunningSettingManager.Save(new RunningSetting { RunMode = runMode });

        var startup = StartupManager.Load([]);

        Assert.True(Guid.TryParse(startup.Session.SessionId, out _));
        Assert.Equal(string.Empty, RunningSettingManager.Load().DefaultSessionId);
    }

    [Fact]
    public void LoadUsesConfiguredDefaultSession()
    {
        var defaultSession = SaveSession("default-session");
        RunningSettingManager.Save(new RunningSetting { DefaultSessionId = defaultSession.SessionId });

        var startup = StartupManager.Load([]);

        Assert.Equal(defaultSession.SessionId, startup.Session.SessionId);
    }

    [Fact]
    public void SaveWithoutSpecifiedSessionPersistsGeneratedSession()
    {
        var startup = StartupManager.Load(["--save"]);

        Assert.True(Guid.TryParse(startup.Session.SessionId, out _));
        var saved = SessionInfoManager.Load(startup.Session.SessionId);
        Assert.Equal(startup.Session.SessionId, saved.SessionId);
        Assert.Equal(SessionTarget.MainMenu, saved.Target);
    }

    [Fact]
    public void RunModeCommandWithoutSessionUsesDefaultRestartLogic()
    {
        var previousRunMode = RunMode.Value;
        try
        {
            RunningSettingManager.Save(new RunningSetting { RunMode = RunModeType.HeadlessServer });
            RunMode.Value = RunModeType.HeadlessServer;
            GameExitManager.BeginSession();
            var registry = new CommandRegistry();
            BuiltInCommands.Register(registry, new ModId("game"));
            registry.Freeze();

            var result = new CommandDispatcher(registry).Execute(
                new SetRunModeCommand(RunModeType.Gui),
                new CommandContext(
                    CommandInvocationChannel.UserInterface,
                    CommandPrincipal.LocalOperator,
                    null,
                    "run-mode-test"
                )
            );

            var settings = RunningSettingManager.Load();
            Assert.True(result.Success, result.Message);
            Assert.Equal(GameExitAction.Restart, GameExitManager.ExitAction);
            Assert.Equal(RunModeType.Gui, settings.RunMode);
            Assert.Equal(string.Empty, settings.PendingSessionId);
        }
        finally
        {
            RunMode.Value = previousRunMode;
            GameExitManager.BeginSession();
        }
    }

    [Fact]
    public void RunModeCommandUsesProvidedRestartSession()
    {
        var previousRunMode = RunMode.Value;
        try
        {
            RunningSettingManager.Save(new RunningSetting { RunMode = RunModeType.HeadlessServer });
            RunMode.Value = RunModeType.HeadlessServer;
            GameExitManager.BeginSession();
            var registry = new CommandRegistry();
            BuiltInCommands.Register(registry, new ModId("game"));
            registry.Freeze();

            var result = new CommandDispatcher(registry).Execute(
                new SetRunModeCommand(
                    RunModeType.Gui,
                    new SessionInfo
                    {
                        Target = SessionTarget.World,
                        World = "HeadlessWorld"
                    }
                ),
                new CommandContext(
                    CommandInvocationChannel.UserInterface,
                    CommandPrincipal.LocalOperator,
                    null,
                    "run-mode-session-test"
                )
            );

            var settings = RunningSettingManager.Load();
            var pendingSession = SessionInfoManager.Load(settings.PendingSessionId);
            Assert.True(result.Success, result.Message);
            Assert.Equal(GameExitAction.Restart, GameExitManager.ExitAction);
            Assert.Equal(RunModeType.Gui, settings.RunMode);
            Assert.Equal(SessionTarget.World, pendingSession.Target);
            Assert.Equal("HeadlessWorld", pendingSession.World);
        }
        finally
        {
            RunMode.Value = previousRunMode;
            GameExitManager.BeginSession();
        }
    }

    [Fact]
    public void LoadClearsInvalidOrMissingPersistedSessionReferences()
    {
        RunningSettingManager.Save(
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
        SessionInfoManager.Save(session);
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

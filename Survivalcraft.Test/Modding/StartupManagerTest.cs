using Engine.Core;
using Engine.FileStorage;

using Game;
using Game.Commands;
using Game.Managers;
using Game.Modding;
using Game.Network.Enums;

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
    public void LoadUsesPendingSessionBeforeDefaultSession()
    {
        var defaultSession = SaveSession("default-session");
        var pendingSession = SaveSession("pending-session");
        RunningSettingManager.Save(new RunningSetting
        {
            DefaultSessionId = defaultSession.SessionId,
            PendingSessionId = pendingSession.SessionId
        });

        var startup = StartupManager.Load([]);

        Assert.Equal(defaultSession.SessionId, startup.Settings.DefaultSessionId);
        Assert.Equal(pendingSession.SessionId, startup.Settings.PendingSessionId);
        Assert.Equal(pendingSession.SessionId, startup.Session.SessionId);
    }

    [Fact]
    public void ExplicitSessionOverridesAreTransientWithoutSave()
    {
        var defaultSession = SaveSession("default-session");
        RunningSettingManager.Save(new RunningSetting { DefaultSessionId = defaultSession.SessionId });
        var alpha = SaveSession("alpha", SessionTarget.World, "AlphaWorld");

        var startup = StartupManager.Load([
            "--session", "alpha",
            "--world", "CustomWorld",
            "--seed", "42",
            "--game-mode", "Creative"
        ]);

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
        var startup = StartupManager.Load([
            "--session", "client-smoke",
            "--connect", "127.0.0.1:28987",
            "--player", "DebugPlayer",
            "--server-port", "28987",
            "--broadcast-port", "28988"
        ]);

        Assert.Equal(SessionTarget.RemoteServer, startup.Session.Target);
        Assert.Equal("127.0.0.1", startup.Session.ServerHost);
        Assert.Equal(28987, startup.Session.ServerPort);
        Assert.Equal("DebugPlayer", startup.Request.PlayerName);
        Assert.Equal(28988, startup.Session.BroadcastPort);
        Assert.False(startup.Request.Save);
        Assert.Null(SessionInfoManager.LoadByName("client-smoke"));
    }

    [Fact]
    public void SessionOverridesRequireExplicitSession()
    {
        var startup = StartupManager.Load([
            "--world", "IgnoredWorld",
            "--seed", "42",
            "--game-mode", "Creative"
        ]);

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

        var startup = StartupManager.Load([
            "--session", "saved-session",
            "--connect", "localhost:29987",
            "--game-mode", "Challenging",
            "--save"
        ]);

        var persistedSettings = RunningSettingManager.Load();
        var saved = SessionInfoManager.LoadByName("saved-session");
        Assert.Equal(defaultSession.SessionId, persistedSettings.DefaultSessionId);
        Assert.True(startup.Request.Save);
        Assert.NotNull(saved);
        Assert.Equal(startup.Session.SessionId, saved!.SessionId);
        Assert.Equal(SessionTarget.RemoteServer, saved.Target);
        Assert.Equal("localhost", saved.ServerHost);
        Assert.Equal(29987, saved.ServerPort);
        Assert.Equal(GameMode.Challenging, saved.GameMode);
    }

    [Fact]
    public void GuiHostUsesTransientRequestAndEffectiveSessionPorts()
    {
        var startup = StartupManager.Load([
            "--gui", "--host",
            "--session", "gui-server",
            "--world", "GuiServerWorld",
            "--player", "HostPlayer",
            "--server-port", "30987",
            "--broadcast-port", "30988"
        ]);

        Assert.Equal(RunModeType.Gui, startup.Settings.RunMode);
        Assert.True(startup.Request.ForceWorldRunServer);
        Assert.Equal("HostPlayer", startup.Request.PlayerName);
        Assert.Equal(SessionTarget.World, startup.Session.Target);
        Assert.Equal("GuiServerWorld", startup.Session.World);
        Assert.Equal(30987, startup.Session.ServerPort);
        Assert.Equal(30988, startup.Session.BroadcastPort);
        Assert.Null(SessionInfoManager.LoadByName("gui-server"));
    }

    [Fact]
    public void ConnectTakesPriorityOverHost()
    {
        var startup = StartupManager.Load([
            "--gui", "--host",
            "--session", "remote-client",
            "--connect", "127.0.0.1:31987"
        ]);

        Assert.False(startup.Request.ForceWorldRunServer);
        Assert.Equal(SessionTarget.RemoteServer, startup.Session.Target);
        Assert.Equal("127.0.0.1", startup.Session.ServerHost);
        Assert.Equal(31987, startup.Session.ServerPort);
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
                new CommandContext(CommandInvocationChannel.UserInterface,
                    CommandPrincipal.ApplicationUser, null, "run-mode-test"));

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
                    }),
                new CommandContext(CommandInvocationChannel.UserInterface,
                    CommandPrincipal.ApplicationUser, null, "run-mode-session-test"));

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
        RunningSettingManager.Save(new RunningSetting
        {
            DefaultSessionId = "not-a-guid",
            PendingSessionId = Guid.NewGuid().ToString("N")
        });

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

    private static SessionInfo SaveSession(string name,
        SessionTarget target = SessionTarget.MainMenu, string world = "")
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

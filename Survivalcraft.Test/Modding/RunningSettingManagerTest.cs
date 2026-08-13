using System.Xml.Linq;

using Engine.Core;
using Engine.FileStorage;

using Game;
using Game.Commands;
using Game.Managers;
using Game.Modding;
using Game.Network.Enums;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class RunningSettingManagerTest : IDisposable
{
    private readonly FileBackup _runningSettingBackup = FileBackup.Create(RunningSettingManager.RunningSettingPath);
    private readonly FileBackup _sessionInfoBackup = FileBackup.Create(SessionInfoManager.SessionInfoPath);

    public RunningSettingManagerTest()
    {
        DeleteIfExists(RunningSettingManager.RunningSettingPath);
        DeleteIfExists(SessionInfoManager.SessionInfoPath);
    }

    [Fact]
    public void LoadUsesPendingSessionBeforeDefaultSession()
    {
        var defaultSessionId = Guid.NewGuid().ToString("N");
        var pendingSessionId = Guid.NewGuid().ToString("N");
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = defaultSessionId,
            Name = "default-session"
        });
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = pendingSessionId,
            Name = "pending-session"
        });
        SaveRunningSetting(new RunningSetting
        {
            RunMode = RunModeType.Gui,
            DefaultSessionId = defaultSessionId,
            PendingSessionId = pendingSessionId
        });

        var setting = RunningSettingManager.Load([]);

        Assert.Equal(defaultSessionId, setting.DefaultSessionId);
        Assert.Equal(pendingSessionId, setting.PendingSessionId);
        Assert.Equal(pendingSessionId, setting.ActiveSessionId);
    }

    [Fact]
    public void LoadTreatsExplicitSessionWithoutSaveAsTransient()
    {
        var defaultSessionId = Guid.NewGuid().ToString("N");
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = defaultSessionId,
            Name = "default-session"
        });
        SaveRunningSetting(new RunningSetting
        {
            DefaultSessionId = defaultSessionId
        });
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Name = "alpha",
            Target = SessionTarget.World,
            World = "AlphaWorld"
        });

        var setting = RunningSettingManager.Load(["--session", "alpha", "--world", "CustomWorld", "--seed", "42"]);

        Assert.Equal(defaultSessionId, setting.DefaultSessionId);
        Assert.Equal(SessionInfoManager.LoadByName("alpha")!.SessionId, setting.ActiveSessionId);
        Assert.True(setting.HasExplicitSessionRequest);
        Assert.Equal("alpha", setting.RequestedSessionName);
        Assert.Equal("CustomWorld", setting.SessionWorldOverride);
        Assert.Equal("42", setting.SessionSeedOverride);
    }

    [Fact]
    public void ConnectAndPlayerOverridesAreTransientWithoutSave()
    {
        var setting = RunningSettingManager.Load([
            "--session", "client-smoke",
            "--connect", "127.0.0.1:28987",
            "--player", "DebugPlayer",
            "--server-port", "28987",
            "--broadcast-port", "28988"
        ]);

        var session = SessionInfoManager.ResolveStartupSession(setting);
        Assert.Equal(SessionTarget.RemoteServer, session.Target);
        Assert.Equal("127.0.0.1", session.ServerHost);
        Assert.Equal(28987, session.ServerPort);
        Assert.Equal("DebugPlayer", setting.PlayerOverride);
        Assert.Equal(28987, session.ServerPort);
        Assert.Equal(28988, session.BroadcastPort);
        Assert.False(setting.SaveRequested);
        Assert.Null(SessionInfoManager.LoadByName("client-smoke"));
    }

    [Fact]
    public void ConnectOverrideIsSavedOnlyWhenRequested()
    {
        var setting = RunningSettingManager.Load([
            "--session", "saved-client",
            "--connect", "localhost:29987",
            "--save"
        ]);

        var saved = SessionInfoManager.LoadByName("saved-client");
        Assert.NotNull(saved);
        Assert.Equal(SessionTarget.RemoteServer, saved!.Target);
        Assert.Equal("localhost", saved.ServerHost);
        Assert.Equal(29987, saved.ServerPort);
        Assert.True(setting.SaveRequested);
    }

    [Fact]
    public void GuiHostOverridesAreTransientAndUseSessionPorts()
    {
        var setting = RunningSettingManager.Load([
            "--gui",
            "--host",
            "--session", "gui-server",
            "--world", "GuiServerWorld",
            "--player", "HostPlayer",
            "--server-port", "30987",
            "--broadcast-port", "30988"
        ]);

        var session = SessionInfoManager.ResolveStartupSession(setting);
        Assert.Equal(RunModeType.Gui, setting.RunMode);
        Assert.True(setting.ForceWorldRunServer);
        Assert.Equal(SessionTarget.World, session.Target);
        Assert.Equal("GuiServerWorld", session.World);
        Assert.Equal("HostPlayer", setting.PlayerOverride);
        Assert.Equal(30987, session.ServerPort);
        Assert.Equal(30988, session.BroadcastPort);
        Assert.Null(SessionInfoManager.LoadByName("gui-server"));
    }

    [Fact]
    public void ConnectTakesPriorityOverHost()
    {
        var setting = RunningSettingManager.Load([
            "--gui",
            "--host",
            "--session", "remote-client",
            "--connect", "127.0.0.1:31987"
        ]);

        var session = SessionInfoManager.ResolveStartupSession(setting);
        Assert.False(setting.ForceWorldRunServer);
        Assert.Equal(SessionTarget.RemoteServer, session.Target);
        Assert.Equal("127.0.0.1", session.ServerHost);
        Assert.Equal(31987, session.ServerPort);
    }

    [Fact]
    public void LoadIgnoresSessionArgumentWhenNameIsMissing()
    {
        SaveRunningSetting(new RunningSetting
        {
            RunMode = RunModeType.Gui
        });

        var setting = RunningSettingManager.Load(["--session"]);

        Assert.False(setting.HasExplicitSessionRequest);
        Assert.Equal(string.Empty, setting.RequestedSessionName);
        Assert.True(Guid.TryParse(setting.ActiveSessionId, out _));
    }

    [Fact]
    public void LoadSaveDoesNotChangeDefaultSessionWhenExplicitSessionIsSaved()
    {
        var defaultSessionId = Guid.NewGuid().ToString("N");
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = defaultSessionId,
            Name = "default-session"
        });
        SaveRunningSetting(new RunningSetting
        {
            DefaultSessionId = defaultSessionId
        });

        var setting = RunningSettingManager.Load(["--session", "saved-session", "--save"]);

        Assert.Equal(defaultSessionId, setting.DefaultSessionId);
        Assert.True(Guid.TryParse(setting.ActiveSessionId, out _));

        var reloaded = RunningSettingManager.Load([]);
        Assert.Equal(defaultSessionId, reloaded.DefaultSessionId);

        var savedSession = SessionInfoManager.LoadByName("saved-session");
        Assert.NotNull(savedSession);
        Assert.Equal(setting.ActiveSessionId, savedSession!.SessionId);
        Assert.Equal("saved-session", savedSession.Name);
    }

    [Fact]
    public void LoadGuiWithoutConfiguredSessionGeneratesTransientActiveSession()
    {
        SaveRunningSetting(new RunningSetting
        {
            RunMode = RunModeType.Gui
        });

        var setting = RunningSettingManager.Load([]);

        Assert.True(Guid.TryParse(setting.ActiveSessionId, out _));
    }

    [Fact]
    public void LoadHeadlessWithoutConfiguredSessionGeneratesTransientActiveSession()
    {
        SaveRunningSetting(new RunningSetting
        {
            RunMode = RunModeType.HeadlessServer
        });

        var setting = RunningSettingManager.Load([]);

        Assert.True(Guid.TryParse(setting.ActiveSessionId, out _));
    }

    [Fact]
    public void LoadUsesDefaultSessionWhenConfigured()
    {
        var defaultSessionId = Guid.NewGuid().ToString("N");
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = defaultSessionId,
            Name = "default-session"
        });
        SaveRunningSetting(new RunningSetting
        {
            RunMode = RunModeType.Gui,
            DefaultSessionId = defaultSessionId
        });

        var setting = RunningSettingManager.Load([]);

        Assert.Equal(defaultSessionId, setting.ActiveSessionId);
    }

    [Fact]
    public void LoadSaveWithoutSpecifiedSessionPersistsGeneratedSession()
    {
        SaveRunningSetting(new RunningSetting
        {
            RunMode = RunModeType.Gui
        });

        var setting = RunningSettingManager.Load(["--save"]);

        Assert.True(Guid.TryParse(setting.ActiveSessionId, out _));

        var savedSession = SessionInfoManager.Load(setting.ActiveSessionId);
        Assert.Equal(setting.ActiveSessionId, savedSession.SessionId);
        Assert.Equal(string.Empty, savedSession.Name);
        Assert.Equal(SessionTarget.MainMenu, savedSession.Target);
    }

    [Fact]
    public void LoadKeepsPendingSessionUntilItIsConsumedByRestore()
    {
        var pendingSessionId = Guid.NewGuid().ToString("N");
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = pendingSessionId,
            Name = string.Empty
        });
        SaveRunningSetting(new RunningSetting
        {
            RunMode = RunModeType.Gui,
            PendingSessionId = pendingSessionId
        });

        var setting = RunningSettingManager.Load([]);

        Assert.Equal(pendingSessionId, setting.PendingSessionId);
        Assert.Equal(pendingSessionId, setting.ActiveSessionId);
    }

    [Fact]
    public void RunModeCommandPersistsModeAndRestartsToMainMenu()
    {
        var previousRunMode = RunMode.Value;
        try
        {
            RunningSettingManager.Save(new RunningSetting
            {
                RunMode = RunModeType.HeadlessServer
            });
            RunMode.Value = RunModeType.HeadlessServer;
            GameExitManager.BeginSession();

            var registry = new CommandRegistry();
            var owner = new ModId("game");
            BuiltInCommands.Register(registry, owner);
            registry.Freeze();
            var result = new CommandDispatcher(registry).Execute(
                new SetRunModeCommand(RunModeType.Gui),
                new CommandContext(
                    CommandInvocationChannel.UserInterface,
                    CommandPrincipal.ApplicationUser,
                    null,
                    "run-mode-test"));

            Assert.True(result.Success);
            Assert.Equal("application.run_mode.restarting", result.Code);
            Assert.Equal(GameExitAction.Restart, GameExitManager.ExitAction);

            var runningSetting = RunningSettingManager.Load([]);
            Assert.Equal(RunModeType.Gui, runningSetting.RunMode);
            Assert.False(string.IsNullOrWhiteSpace(runningSetting.PendingSessionId));
            Assert.Equal(
                SessionTarget.MainMenu,
                SessionInfoManager.Load(runningSetting.PendingSessionId).Target);
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
        SaveRunningSetting(new RunningSetting
        {
            DefaultSessionId = "not-a-guid",
            PendingSessionId = Guid.NewGuid().ToString("N")
        });

        var setting = RunningSettingManager.Load([]);

        Assert.Equal(string.Empty, setting.DefaultSessionId);
        Assert.Equal(string.Empty, setting.PendingSessionId);
        Assert.True(Guid.TryParse(setting.ActiveSessionId, out _));
    }

    public void Dispose()
    {
        _runningSettingBackup.Dispose();
        _sessionInfoBackup.Dispose();
    }

    private static void SaveRunningSetting(RunningSetting setting)
    {
        EnsureConfigDirectory();
        using var stream = Storage.OpenFile(RunningSettingManager.RunningSettingPath, OpenFileMode.Create);
        var root = new XElement("RunningSetting",
            new XAttribute(nameof(RunningSetting.RunMode), setting.RunMode),
            new XAttribute(nameof(RunningSetting.LogLevel), setting.LogLevel),
            new XAttribute(nameof(RunningSetting.DefaultSessionId), setting.DefaultSessionId),
            new XAttribute(nameof(RunningSetting.PendingSessionId), setting.PendingSessionId));
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

        private static void EnsureParentDirectory(string path)
        {
            var directory = Storage.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Storage.DirectoryExists(directory))
            {
                Storage.CreateDirectory(directory);
            }
        }
    }
}

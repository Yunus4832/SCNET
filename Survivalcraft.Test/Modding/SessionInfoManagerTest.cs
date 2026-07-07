using Engine.FileStorage;
using Engine.Core;

using Game;
using Game.Managers;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class SessionInfoManagerTest : IDisposable
{
    private readonly FileBackup _sessionInfoBackup = FileBackup.Create(SessionInfoManager.SessionInfoPath);

    public SessionInfoManagerTest()
    {
        if (Storage.FileExists(SessionInfoManager.SessionInfoPath))
        {
            Storage.DeleteFile(SessionInfoManager.SessionInfoPath);
        }
    }

    [Fact]
    public void SavePersistsMultipleNamedSessions()
    {
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Name = "Alpha",
            Target = SessionTarget.World,
            World = "AlphaWorld",
            Seed = "11"
        });
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Name = "Beta",
            Target = SessionTarget.World,
            World = "BetaWorld",
            Seed = "22"
        });

        var alpha = SessionInfoManager.LoadByName("Alpha");
        var beta = SessionInfoManager.LoadByName("Beta");

        Assert.NotNull(alpha);
        Assert.NotNull(beta);
        Assert.Equal("AlphaWorld", alpha!.World);
        Assert.Equal("Alpha", alpha.Name);
        Assert.Equal("11", alpha.Seed);
        Assert.Equal(SessionTarget.World, alpha.Target);

        Assert.Equal("BetaWorld", beta!.World);
        Assert.Equal("Beta", beta.Name);
        Assert.Equal("22", beta.Seed);
        Assert.Equal(SessionTarget.World, beta.Target);
    }

    [Fact]
    public void SaveAssignsGuidWhenSessionIdIsMissing()
    {
        var sessionInfo = new SessionInfo
        {
            Target = SessionTarget.World,
            World = "AlphaWorld"
        };

        SessionInfoManager.Save(sessionInfo);

        Assert.True(Guid.TryParse(sessionInfo.SessionId, out _));
        Assert.Equal(string.Empty, sessionInfo.Name);

        var reloaded = SessionInfoManager.Load(sessionInfo.SessionId);
        Assert.Equal("AlphaWorld", reloaded.World);
        Assert.Equal(string.Empty, reloaded.Name);
        Assert.Equal(SessionTarget.World, reloaded.Target);
    }

    [Fact]
    public void LoadByNameFindsNamedSession()
    {
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Name = "named-session",
            Target = SessionTarget.World,
            World = "NamedWorld"
        });

        var sessionInfo = SessionInfoManager.LoadByName("named-session");

        Assert.NotNull(sessionInfo);
        Assert.True(Guid.TryParse(sessionInfo!.SessionId, out _));
        Assert.Equal("named-session", sessionInfo.Name);
    }

    [Fact]
    public void ResolveStartupSessionCreatesMainMenuSessionForGuiWhenNotEnteringSession()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var sessionInfo = SessionInfoManager.ResolveStartupSession(new RunningSetting
        {
            RunMode = RunModeType.Gui,
            ActiveSessionId = sessionId
        });

        Assert.Equal(sessionId, sessionInfo.SessionId);
        Assert.Equal(string.Empty, sessionInfo.Name);
        Assert.Equal(SessionTarget.MainMenu, sessionInfo.Target);
    }

    [Fact]
    public void ResolveStartupSessionCreatesWorldLoadingSessionForExplicitGuiSessionWithWorldOverride()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var sessionInfo = SessionInfoManager.ResolveStartupSession(new RunningSetting
        {
            RunMode = RunModeType.Gui,
            ActiveSessionId = sessionId,
            HasExplicitSessionRequest = true,
            RequestedSessionName = "alpha",
            SessionWorldOverride = "CustomWorld"
        });

        Assert.Equal(sessionId, sessionInfo.SessionId);
        Assert.Equal("alpha", sessionInfo.Name);
        Assert.Equal(SessionTarget.World, sessionInfo.Target);
        Assert.Equal("CustomWorld", sessionInfo.World);
    }

    [Fact]
    public void ResolveStartupSessionCreatesNamedWorldWhenExplicitGuiSessionDoesNotExistAndWorldIsMissing()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var sessionInfo = SessionInfoManager.ResolveStartupSession(new RunningSetting
        {
            RunMode = RunModeType.Gui,
            ActiveSessionId = sessionId,
            HasExplicitSessionRequest = true,
            RequestedSessionName = "alpha"
        });

        Assert.Equal(sessionId, sessionInfo.SessionId);
        Assert.Equal("alpha", sessionInfo.Name);
        Assert.Equal(SessionTarget.World, sessionInfo.Target);
        Assert.Equal("alpha", sessionInfo.World);
    }

    [Fact]
    public void ResolveStartupSessionUpgradesExistingNamedGuiSessionToWorldLoadingWhenWorldOverrideExists()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        SessionInfoManager.Save(new SessionInfo
        {
            SessionId = sessionId,
            Name = "alpha",
            Target = SessionTarget.MainMenu
        });

        var sessionInfo = SessionInfoManager.ResolveStartupSession(new RunningSetting
        {
            RunMode = RunModeType.Gui,
            ActiveSessionId = sessionId,
            HasExplicitSessionRequest = true,
            RequestedSessionName = "alpha",
            SessionWorldOverride = "CustomWorld"
        });

        Assert.Equal(sessionId, sessionInfo.SessionId);
        Assert.Equal("alpha", sessionInfo.Name);
        Assert.Equal(SessionTarget.World, sessionInfo.Target);
        Assert.Equal("CustomWorld", sessionInfo.World);
    }

    public void Dispose()
    {
        _sessionInfoBackup.Dispose();
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

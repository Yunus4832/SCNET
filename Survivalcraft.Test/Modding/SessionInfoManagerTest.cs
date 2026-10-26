using Engine.Core.Runtime;
using Engine.FileStorage;
using Engine.Network;

using Game;
using Game.Managers;
using Game.Servers;
using Game.Startup;
using Game.World;

using Rendezvous.Protocol;

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoomRestartPreservesLogicalInvitationTarget(bool prepared)
    {
        var room = new RendezvousRoom(
            new Uri("https://signal.example.test/"),
            "Installed service",
            new RoomSummary(Guid.NewGuid(), "Room", "Description", "001234", ["holePunch", "relay"])
        );
        NetworkTarget target = prepared
            ? new InvitationSessionNetworkTarget(room.ControlUrl, room.Summary.Id, RelayPolicy.Required)
            : room.CreateTarget(RelayPolicy.Required);
        var restart = SessionInfoManager.PrepareRestartSession(
            new SessionInfo
            {
                Target = SessionTarget.RemoteServer,
                RemoteTarget = target
            }
        );
        var restored = SessionInfoManager.Load(restart.SessionId);
        Assert.Equal(SessionTarget.RemoteServer, restored.Target);
        Assert.Equal(target, restored.RemoteTarget);
        var xml = Storage.ReadAllText(SessionInfoManager.SessionInfoPath);
        Assert.DoesNotContain("Credential", xml);
        Assert.DoesNotContain("Ticket", xml);
        Assert.DoesNotContain("Description", xml);
    }

    [Fact]
    public void InvitationSessionRoundTripPreservesLocatorAndRelayPolicy()
    {
        var target = new InvitationSessionNetworkTarget(
            new Uri("https://signal.example.test/"),
            Guid.NewGuid(),
            RelayPolicy.Required
        );
        var session = new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Name = "Invitation",
            Target = SessionTarget.RemoteServer,
            RemoteTarget = target
        };
        SessionInfoManager.Save(session);
        var restored = SessionInfoManager.LoadByName("Invitation");
        Assert.NotNull(restored);
        Assert.Equal(target, restored.RemoteTarget);
        Assert.Equal(SessionTarget.RemoteServer, restored.Target);
    }

    [Fact]
    public void DnsTargetRoundTripDoesNotResolveOrStoreAListenerPortOverride()
    {
        var target = new AddressNetworkTarget("unresolved.example.test", 28887);
        SessionInfoManager.Save(
            new SessionInfo
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Name = "DnsClient",
                Target = SessionTarget.RemoteServer,
                RemoteTarget = target,
                ServerPort = 31987
            }
        );
        var restored = SessionInfoManager.LoadByName("DnsClient");
        Assert.NotNull(restored);
        Assert.Equal(target, restored.RemoteTarget);
        Assert.Equal(31987, restored.ServerPort);
    }

    [Fact]
    public void SavePersistsMultipleNamedSessions()
    {
        SessionInfoManager.Save(
            new SessionInfo
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Name = "Alpha",
                Target = SessionTarget.World,
                World = "AlphaWorld",
                Seed = "11",
                GameMode = GameMode.Creative
            }
        );
        SessionInfoManager.Save(
            new SessionInfo
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Name = "Beta",
                Target = SessionTarget.World,
                World = "BetaWorld",
                Seed = "22"
            }
        );

        var alpha = SessionInfoManager.LoadByName("Alpha");
        var beta = SessionInfoManager.LoadByName("Beta");

        Assert.NotNull(alpha);
        Assert.NotNull(beta);
        Assert.Equal("AlphaWorld", alpha!.World);
        Assert.Equal("Alpha", alpha.Name);
        Assert.Equal("11", alpha.Seed);
        Assert.Equal(GameMode.Creative, alpha.GameMode);
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
    public void SaveRoundTripsRecoverableStartupOverrides()
    {
        var sessionInfo = new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Name = "client",
            AutoJoinCharacterName = " DebugPlayer ",
            HttpCommandEnabled = true,
            HttpCommandPort = 30989,
            HttpCommandAccessToken = "access-token"
        };

        SessionInfoManager.Save(sessionInfo);

        var reloaded = SessionInfoManager.Load(sessionInfo.SessionId);
        Assert.Equal("DebugPlayer", reloaded.AutoJoinCharacterName);
        Assert.True(reloaded.HttpCommandEnabled);
        Assert.Equal(30989, reloaded.HttpCommandPort);
        Assert.Equal("access-token", reloaded.HttpCommandAccessToken);
    }

    [Fact]
    public void CreateRemoteClientSessionPreservesEffectiveTransientOverrides()
    {
        var startup = StartupManager.Load(
            [
                "--gui",
                "--session", "client",
                "--player", "DebugPlayer",
                "--http-command",
                "--http-command-port", "30989",
                "--http-command-access-token", "access-token"
            ]
        );

        var restartSession = SessionInfoManager.CreateRemoteClientSession(new AddressNetworkTarget("127.0.0.1", 30987));

        Assert.Equal(startup.Session.SessionId, restartSession.SessionId);
        Assert.Equal(SessionTarget.RemoteServer, restartSession.Target);
        Assert.Equal("127.0.0.1", Assert.IsType<AddressNetworkTarget>(restartSession.RemoteTarget).Host);
        Assert.Equal(30987, Assert.IsType<AddressNetworkTarget>(restartSession.RemoteTarget).Port);
        Assert.Equal("DebugPlayer", restartSession.AutoJoinCharacterName);
        Assert.True(restartSession.HttpCommandEnabled);
        Assert.Equal(30989, restartSession.HttpCommandPort);
        Assert.Equal("access-token", restartSession.HttpCommandAccessToken);
    }

    [Fact]
    public void LoadByNameFindsNamedSession()
    {
        SessionInfoManager.Save(
            new SessionInfo
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Name = "named-session",
                Target = SessionTarget.World,
                World = "NamedWorld"
            }
        );

        var sessionInfo = SessionInfoManager.LoadByName("named-session");

        Assert.NotNull(sessionInfo);
        Assert.True(Guid.TryParse(sessionInfo!.SessionId, out _));
        Assert.Equal("named-session", sessionInfo.Name);
    }

    [Fact]
    public void ResolveStartupSessionCreatesMainMenuSessionForGuiWhenNotEnteringSession()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var sessionInfo = SessionInfoManager.ResolveStartupSession(
            new RunningSetting { RunMode = RunModeType.Gui },
            new StartupRequest(),
            sessionId
        );

        Assert.Equal(sessionId, sessionInfo.SessionId);
        Assert.Equal(string.Empty, sessionInfo.Name);
        Assert.Equal(SessionTarget.MainMenu, sessionInfo.Target);
    }

    [Fact]
    public void ResolveStartupSessionCreatesWorldLoadingSessionForExplicitGuiSessionWithWorldOverride()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var sessionInfo = SessionInfoManager.ResolveStartupSession(
            new RunningSetting { RunMode = RunModeType.Gui },
            new StartupRequest
            {
                HasExplicitSession = true,
                SessionName = "alpha",
                World = "CustomWorld"
            },
            sessionId
        );

        Assert.Equal(sessionId, sessionInfo.SessionId);
        Assert.Equal("alpha", sessionInfo.Name);
        Assert.Equal(SessionTarget.World, sessionInfo.Target);
        Assert.Equal("CustomWorld", sessionInfo.World);
    }

    [Fact]
    public void ResolveStartupSessionCreatesNamedWorldWhenExplicitGuiSessionDoesNotExistAndWorldIsMissing()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var sessionInfo = SessionInfoManager.ResolveStartupSession(
            new RunningSetting { RunMode = RunModeType.Gui },
            new StartupRequest
            {
                HasExplicitSession = true,
                SessionName = "alpha"
            },
            sessionId
        );

        Assert.Equal(sessionId, sessionInfo.SessionId);
        Assert.Equal("alpha", sessionInfo.Name);
        Assert.Equal(SessionTarget.World, sessionInfo.Target);
        Assert.Equal("alpha", sessionInfo.World);
    }

    [Fact]
    public void ResolveStartupSessionUpgradesExistingNamedGuiSessionToWorldLoadingWhenWorldOverrideExists()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        SessionInfoManager.Save(
            new SessionInfo
            {
                SessionId = sessionId,
                Name = "alpha",
                Target = SessionTarget.MainMenu
            }
        );

        var sessionInfo = SessionInfoManager.ResolveStartupSession(
            new RunningSetting { RunMode = RunModeType.Gui },
            new StartupRequest
            {
                HasExplicitSession = true,
                SessionName = "alpha",
                World = "CustomWorld"
            },
            sessionId
        );

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

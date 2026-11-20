using Engine.Core.FileStorage;
using Engine.Network;

using Game.FileStorage;
using Game.Managers;
using Game.Servers;
using Game.Startup;
using Game.World;

using Rendezvous.Protocol;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class SessionInfoManagerTest : IDisposable
{
    private readonly FileBackup _sessionInfoBackup = FileBackup.Create(StartupManager.Profiles.SessionsPath);

    public SessionInfoManagerTest()
    {
        if (Storage.FileExists(StartupManager.Profiles.SessionsPath))
        {
            Storage.DeleteFile(StartupManager.Profiles.SessionsPath);
        }
    }

    [Fact]
    public void HostedWorldRestartPreservesTargetAndExplicitSessionSelection()
    {
        var restart = SessionInfoManager.PrepareRestartSession(
            new SessionInfo
            {
                Name = "HostedTest", Target = SessionTarget.HostedWorld, World = "HostedWorld"
            }
        );
        var restored = StartupManager.Profiles.LoadSession(restart.SessionId);
        Assert.Equal(SessionTarget.HostedWorld, restored.Target);
        Assert.Equal("HostedWorld", restored.World);
        restored.Name = "HostedTest";
        StartupManager.Profiles.SaveSession(restored);
        var selected = StartupManager.Profiles.ResolveStartupSession(
            new RunningSetting(),
            new StartupRequest { HasExplicitSession = true, SessionName = "HostedTest" },
            restart.SessionId
        );
        Assert.Equal(SessionTarget.HostedWorld, selected.Target);
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
        var restored = StartupManager.Profiles.LoadSession(restart.SessionId);
        Assert.Equal(SessionTarget.RemoteServer, restored.Target);
        Assert.Equal(target, restored.RemoteTarget);
        var xml = Storage.ReadAllText(StartupManager.Profiles.SessionsPath);
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
        StartupManager.Profiles.SaveSession(session);
        var restored = StartupManager.Profiles.LoadSessionByName("Invitation");
        Assert.NotNull(restored);
        Assert.Equal(target, restored.RemoteTarget);
        Assert.Equal(SessionTarget.RemoteServer, restored.Target);
    }

    [Fact]
    public void DnsTargetRoundTripDoesNotResolveOrStoreAListenerPortOverride()
    {
        var target = new AddressNetworkTarget("unresolved.example.test", 28887);
        StartupManager.Profiles.SaveSession(
            new SessionInfo
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Name = "DnsClient",
                Target = SessionTarget.RemoteServer,
                RemoteTarget = target,
                ServerPort = 31987
            }
        );
        var restored = StartupManager.Profiles.LoadSessionByName("DnsClient");
        Assert.NotNull(restored);
        Assert.Equal(target, restored.RemoteTarget);
        Assert.Equal(31987, restored.ServerPort);
    }

    [Fact]
    public void SavePersistsMultipleNamedSessions()
    {
        StartupManager.Profiles.SaveSession(
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
        StartupManager.Profiles.SaveSession(
            new SessionInfo
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Name = "Beta",
                Target = SessionTarget.World,
                World = "BetaWorld",
                Seed = "22"
            }
        );

        var alpha = StartupManager.Profiles.LoadSessionByName("Alpha");
        var beta = StartupManager.Profiles.LoadSessionByName("Beta");

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

        StartupManager.Profiles.SaveSession(sessionInfo);

        Assert.True(Guid.TryParse(sessionInfo.SessionId, out _));
        Assert.Equal(string.Empty, sessionInfo.Name);

        var reloaded = StartupManager.Profiles.LoadSession(sessionInfo.SessionId);
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

        StartupManager.Profiles.SaveSession(sessionInfo);

        var reloaded = StartupManager.Profiles.LoadSession(sessionInfo.SessionId);
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
        StartupManager.Profiles.SaveSession(
            new SessionInfo
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Name = "named-session",
                Target = SessionTarget.World,
                World = "NamedWorld"
            }
        );

        var sessionInfo = StartupManager.Profiles.LoadSessionByName("named-session");

        Assert.NotNull(sessionInfo);
        Assert.True(Guid.TryParse(sessionInfo!.SessionId, out _));
        Assert.Equal("named-session", sessionInfo.Name);
    }

    [Fact]
    public void ResolveStartupSessionCreatesMainMenuSessionForGuiWhenNotEnteringSession()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var sessionInfo = StartupManager.Profiles.ResolveStartupSession(
            new RunningSetting(),
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
        var sessionInfo = StartupManager.Profiles.ResolveStartupSession(
            new RunningSetting(),
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
        var sessionInfo = StartupManager.Profiles.ResolveStartupSession(
            new RunningSetting(),
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
        StartupManager.Profiles.SaveSession(
            new SessionInfo
            {
                SessionId = sessionId,
                Name = "alpha",
                Target = SessionTarget.MainMenu
            }
        );

        var sessionInfo = StartupManager.Profiles.ResolveStartupSession(
            new RunningSetting(),
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

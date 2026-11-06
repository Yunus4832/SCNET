using Engine.Core.FileStorage;

using Game.Admission;
using Game.Startup;

namespace Survivalcraft.Hosting.Test.Startup;

public sealed class StartupProfileStoreTest : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "scnet-startup-store-" + Guid.NewGuid().ToString("N")
    );

    private readonly string _rootName = "startup-test-" + Guid.NewGuid().ToString("N");

    public StartupProfileStoreTest()
    {
        Directory.CreateDirectory(_directory);
        Storage.RegisterFileSystemRoot(_rootName, _directory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitPathsSupportRootFilesAndCreateMissingParentDirectories(bool nested)
    {
        var store = CreateStore(nested);
        store.LoadStartup([], () => new ServerAdmissionOverrides());
        Assert.True(Storage.FileExists(store.RunningSettingsPath));
        var session = new SessionInfo { Name = "Saved" };
        store.SaveSession(session);
        Assert.True(store.SessionExists(session.SessionId));
        Assert.Equal(session.SessionId, store.LoadSessionByName("Saved")!.SessionId);
    }

    [Fact]
    public void EntrySuppliesANewSessionTargetWithoutPersistingAnApplicationMode()
    {
        var store = CreateStore();
        var server = store.LoadStartup([], () => new ServerAdmissionOverrides(), SessionTarget.World);
        Assert.Equal(SessionTarget.World, server.Session.Target);
        Assert.Equal(
            SessionTarget.MainMenu,
            store.LoadStartup([], () => new ServerAdmissionOverrides()).Session.Target
        );
        Assert.False(Storage.FileExists(store.SessionsPath));
    }

    [Fact]
    public void ExplicitSessionWinsOverPendingWhichWinsOverDefault()
    {
        var store = CreateStore();
        var first = new SessionInfo { Name = "Default" };
        var pending = new SessionInfo { Name = "Pending" };
        var selected = new SessionInfo { Name = "Explicit" };
        store.SaveSession(first);
        store.SaveSession(pending);
        store.SaveSession(selected);
        store.SaveRunningSettings(
            new RunningSetting { DefaultSessionId = first.SessionId, PendingSessionId = pending.SessionId }
        );
        Assert.Equal(
            selected.SessionId,
            store.LoadStartup(["--session", "Explicit"], () => new ServerAdmissionOverrides()).Session.SessionId
        );
        Assert.Equal(pending.SessionId, store.LoadStartup([], () => new ServerAdmissionOverrides()).Session.SessionId);
        Assert.Equal(pending.SessionId, store.LoadRunningSettings().PendingSessionId);
        store.ClearPendingSession();
        Assert.Equal(first.SessionId, store.LoadStartup([], () => new ServerAdmissionOverrides()).Session.SessionId);
    }

    [Fact]
    public void TemporaryOverridesDoNotChangePersistedSettingsOrStoredSessionWithoutSave()
    {
        var store = CreateStore();
        var saved = new SessionInfo { Name = "Saved", ServerPort = 30001 };
        store.SaveSession(saved);
        store.SaveRunningSettings(new RunningSetting());
        var context = store.LoadStartup(
            ["--session", "Saved", "--server-port", "30002"],
            () => new ServerAdmissionOverrides()
        );

        Assert.Equal(30002, context.Session.ServerPort);


        Assert.Equal(30001, store.LoadSession(saved.SessionId).ServerPort);
        store.LoadStartup(
            ["--session", "Saved", "--server-port", "30002", "--save"],
            () => new ServerAdmissionOverrides()
        );

        Assert.Equal(30002, store.LoadSession(saved.SessionId).ServerPort);
    }

    [Fact]
    public void InvalidPersistedReferencesAreClearedAndMissingSessionsDoNotBecomeSavedProfiles()
    {
        var store = CreateStore();
        store.SaveRunningSettings(
            new RunningSetting { DefaultSessionId = Guid.NewGuid().ToString("N"), PendingSessionId = "bad" }
        );
        var context = store.LoadStartup([], () => new ServerAdmissionOverrides());
        Assert.Empty(context.Settings.DefaultSessionId);
        Assert.Empty(context.Settings.PendingSessionId);
        Assert.True(Guid.TryParse(context.Session.SessionId, out _));
        Assert.False(store.SessionExists(context.Session.SessionId));
        Assert.False(Storage.FileExists(store.SessionsPath));
    }

    [Fact]
    public void BrokenRunningSettingsFallBackButBrokenSessionXmlFailsClosed()
    {
        var store = CreateStore(false);
        File.WriteAllText(Path.Combine(_directory, "RunningSetting.xml"), "<broken");

        File.WriteAllText(Path.Combine(_directory, "Sessions.xml"), "<broken");
        Assert.Throws<InvalidDataException>(() => store.LoadSession(Guid.NewGuid().ToString("N")));
        Assert.Throws<InvalidDataException>(() => store.LoadSessionByName("Saved"));
        Assert.Throws<InvalidDataException>(() => store.SessionExists(Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public void InvalidStoredAdmissionKeepsItsSpecificFailureClassification()
    {
        var store = CreateStore(false);
        File.WriteAllText(
            Path.Combine(_directory, "Sessions.xml"),
            "<Sessions><SessionInfo Name='Saved'><AdmissionOverrides Password='plaintext' /></SessionInfo></Sessions>"
        );
        Assert.Throws<AdmissionConfigurationException>(() => store.LoadSessionByName("Saved"));
    }

    [Fact]
    public void DeletingOneSessionKeepsOtherProfilesAndDoesNotConsumePendingImplicitly()
    {
        var store = CreateStore();
        var first = new SessionInfo { Name = "First" };
        var second = new SessionInfo { Name = "Second" };
        store.SaveSession(first);
        store.SaveSession(second);
        store.SaveRunningSettings(new RunningSetting { PendingSessionId = second.SessionId });
        store.DeleteSession(first.SessionId);
        Assert.False(store.SessionExists(first.SessionId));
        Assert.True(store.SessionExists(second.SessionId));
        Assert.Equal(second.SessionId, store.LoadRunningSettings().PendingSessionId);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    private StartupProfileStore CreateStore(bool nested = true)
    {
        var prefix = _rootName + (nested ? ":profiles/" : ":");
        return new StartupProfileStore(prefix + "RunningSetting.xml", prefix + "Sessions.xml");
    }
}

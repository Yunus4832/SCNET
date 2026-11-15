using System.Collections.Concurrent;
using System.Reflection;

using Engine.Core.FileStorage;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.FileStorage;
using Game.Managers;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Protocol;
using Game.Startup;
using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Server.Test;

[Collection(ServerStarterCollection.Name)]
public sealed class ServerStopRequestTest
{
    [Fact]
    public async Task StopRequestedDuringOwnerWorkSavesAndDisposesOnlyOnTheAuthorityThread()
    {
        await using var scope = await StopScope.CreateAsync();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var blocked = scope.Runtime.AuthorityExecution!.InvokeAsync(_ =>
            {
                entered.Set();
                release.Wait();
                return true;
            }
        );
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            ServerStarter.BeginStopRequestHandling();
            ServerStarter.RequestStop();
            Assert.False(scope.ProcessStopTick());
            Assert.Empty(scope.Observer.SaveThreads);
            Assert.Empty(scope.Observer.DisposeThreads);
            Assert.False(scope.Project.IsDisposed);
        }
        finally
        {
            release.Set();
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
        }

        await scope.WaitForStopAsync();
        Assert.Equal([scope.OwnerThread], scope.Observer.SaveThreads);
        Assert.Equal([scope.OwnerThread], scope.Observer.DisposeThreads);
        Assert.True(scope.World.IsClosed);
        Assert.Equal(NodeRole.Inactive, scope.Node.Role);
    }

    [Fact]
    public async Task EarlyRequestSurvivesInitializationAndFailedSaveRequiresAnExplicitRetry()
    {
        await using var scope = await StopScope.CreateAsync();
        scope.Observer.FailSave = true;
        ServerStarter.RequestStop();
        ServerStarter.BeginStopRequestHandling();
        await scope.WaitForFailedSaveAsync();
        Assert.False(scope.ProcessStopTick());
        Assert.Single(scope.Observer.SaveThreads);
        Assert.Empty(scope.Observer.DisposeThreads);
        Assert.False(scope.Project.IsDisposed);
        Assert.False(scope.World.IsClosed);
        scope.Observer.FailSave = false;
        ServerStarter.RequestStop();
        await scope.WaitForStopAsync();
        Assert.Equal(2, scope.Observer.SaveThreads.Count);
        Assert.Single(scope.Observer.DisposeThreads);
        await scope.CloseWorldAsync();
        // A late request after shutdown cannot stop the next run.
        ServerStarter.RequestStop();
        ServerStarter.BeginStopRequestHandling();
        Assert.False(ServerStarter.ProcessStopRequest());
    }

    [Fact]
    public async Task CancelledNewOwnerStopsEvenWhenItsTaskStartsAfterThePreviousRunEnded()
    {
        await using var scope = await StopScope.CreateAsync();
        ServerStarter.EndStopRequestHandling();
        using var owner = new CancellationTokenSource();
        owner.Cancel();
        using var registration = ServerStarter.BeginStopRequestHandling(owner.Token);
        await scope.WaitForStopAsync();
        Assert.Single(scope.Observer.SaveThreads);
        Assert.Single(scope.Observer.DisposeThreads);
    }

    [Fact]
    public async Task DestroyingAnOldOwnerCannotStopTheNextRun()
    {
        await using var scope = await StopScope.CreateAsync();
        using var oldOwner = new CancellationTokenSource();
        var oldRegistration = ServerStarter.BeginStopRequestHandling(oldOwner.Token);
        oldRegistration.Dispose();
        ServerStarter.EndStopRequestHandling();
        using var newOwner = new CancellationTokenSource();
        using var registration = ServerStarter.BeginStopRequestHandling(newOwner.Token);
        oldOwner.Cancel();
        Assert.False(scope.ProcessStopTick());
        Assert.Empty(scope.Observer.SaveThreads);
        Assert.False(scope.Project.IsDisposed);
        newOwner.Cancel();
        await scope.WaitForStopAsync();
        Assert.Single(scope.Observer.DisposeThreads);
    }

    [Fact]
    public async Task FailedSaveRejectsTheActualRestartCallbackBeforePublishingIntent()
    {
        await using var scope = await StopScope.CreateAsync();
        var previousAction = ServerStarter.ExitAction;
        scope.Observer.FailSave = true;
        ServerStarter.BeginStopRequestHandling();
        ServerStarter.ProcessCommands.Restart(new SessionInfo());
        Assert.Equal(previousAction, ServerStarter.ExitAction);
        await scope.WaitForFailedSaveAsync();
        Assert.Equal(previousAction, ServerStarter.ExitAction);
        Assert.False(scope.ProcessStopTick());
        Assert.False(scope.Project.IsDisposed);
        Assert.Single(scope.Observer.SaveThreads);
    }

    [Fact]
    public async Task ActualRestartSavesAndRetiresTheWorldBeforePublishingItsRecoverableSession()
    {
        await using var scope = await StopScope.CreateAsync();
        var paths = new[] { GamePaths.RunningSettingFile, GamePaths.SessionInfoFile };
        var backups = paths.ToDictionary(
            path => path,
            path => Storage.FileExists(path) ? File.ReadAllBytes(Storage.GetSystemPath(path)) : null
        );
        var previousAction = ServerStarter.ExitAction;
        try
        {
            ServerStarter.BeginStopRequestHandling();
            var source = new SessionInfo { World = "RestartWorld", ServerPort = 43971, HttpCommandEnabled = true };
            ServerStarter.ProcessCommands.Restart(source);
            Assert.Equal(previousAction, ServerStarter.ExitAction);
            await scope.WaitForStopAsync();
            Assert.Equal(GameExitAction.Restart, ServerStarter.ExitAction);
            Assert.Equal([scope.OwnerThread], scope.Observer.SaveThreads);
            Assert.Equal([scope.OwnerThread], scope.Observer.DisposeThreads);
            Assert.True(scope.Project.IsDisposed);
            var settings = StartupManager.Profiles.LoadRunningSettings();
            Assert.False(string.IsNullOrWhiteSpace(settings.PendingSessionId));
            var pending = StartupManager.Profiles.LoadSession(settings.PendingSessionId);
            Assert.Equal(source.World, pending.World);
            Assert.Equal(source.ServerPort, pending.ServerPort);
            Assert.Equal(source.HttpCommandEnabled, pending.HttpCommandEnabled);
            Assert.Equal(string.Empty, source.SessionId);
        }
        finally
        {
            foreach (var (path, bytes) in backups)
            {
                if (bytes is not null) { File.WriteAllBytes(Storage.GetSystemPath(path), bytes); }
                else if (Storage.FileExists(path)) { Storage.DeleteFile(path); }
            }

            StartupManager.Profiles.LoadRunningSettings();
            if (backups[GamePaths.RunningSettingFile] is null) { Storage.DeleteFile(GamePaths.RunningSettingFile); }

            typeof(ServerStarter).GetProperty(nameof(ServerStarter.ExitAction))!.SetValue(null, previousAction);
        }
    }

    private sealed class StopScope : IAsyncDisposable
    {
        private static FieldInfo Field(string name) => typeof(ServerStarter).GetField(
            name,
            BindingFlags.Static | BindingFlags.NonPublic
        )!;

        private readonly GameWorldController _controller = (GameWorldController)Field("_worlds").GetValue(null)!;

        private readonly Dictionary<FieldInfo, object?> _previous =
            new[] { "_running", "_stopRequested", "_pendingWorldStop", "_commitAfterStop" }
                .Select(Field).ToDictionary(field => field, field => field.GetValue(null));

        private readonly string _directory = Path.Combine(
            Path.GetTempPath(),
            "scnet-stop-" + Guid.NewGuid().ToString("N")
        );

        private readonly string _scheme = "server-stop-" + Guid.NewGuid().ToString("N");

        internal NetNode Node { get; } = new(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );

        internal WorldRuntimeContext World { get; private set; } = null!;

        internal ServerTestProject Project { get; private set; } = null!;

        internal GameRuntime Runtime { get; } = new(false);

        internal ThreadObserver Observer { get; } = new();

        internal int OwnerThread { get; private set; }

        internal static async Task<StopScope> CreateAsync()
        {
            var scope = new StopScope();
            Assert.Null(scope._controller.Runtime);
            Field("_stopRequested").SetValue(null, 0);
            Field("_pendingWorldStop").SetValue(null, null);
            Field("_commitAfterStop").SetValue(null, null);
            Storage.RegisterFileSystemRoot(scope._scheme, scope._directory);
            try
            {
                await scope.Runtime.StartAuthorityAsync(cancellation =>
                    {
                        scope.OwnerThread = Environment.CurrentManagedThreadId;
                        scope.World = scope.Node.Runtime;
                        scope.Project = new ServerTestProject(scope.World);
                        var catalog = new BlockContentCatalog(
                            BlockRuntimeCatalog.Compile(
                                [
                                    new(
                                        ResourceId.Parse("game:air"),
                                        new BlockRegistration(static () => new AirBlock())
                                    )
                                ]
                            )
                        );
                        _ = new WorldContentBinding(scope.World, catalog, catalog.Dispose);
                        scope.Project.AddSubsystem(
                            new SubsystemGameInfo
                            {
                                WorldSettings = new WorldSettings(), DirectoryName = scope._scheme + ":/world"
                            }
                        );
                        scope.Project.AddSubsystem(scope.Observer);
                        var lifecycle = new SubsystemWorldLifecycle();
                        scope.Project.AddSubsystem(lifecycle);
                        lifecycle.Load(new ValuesDictionary());
                        return scope.World;
                    }
                );
                typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!.SetValue(
                    scope._controller,
                    scope.Runtime
                );
                return scope;
            }
            catch
            {
                await scope.DisposeAsync();
                throw;
            }
        }

        internal bool ProcessStopTick() => ServerStarter.ProcessStopRequest();

        internal async Task WaitForStopAsync()
        {
            for (var i = 0; i < 500; i++)
            {
                if (ProcessStopTick()) { return; }

                await Task.Delay(2);
            }

            Assert.Fail("The host did not observe authority retirement.");
        }

        internal async Task WaitForFailedSaveAsync()
        {
            for (var i = 0; i < 500; i++)
            {
                Assert.False(ProcessStopTick());
                if (!Observer.SaveThreads.IsEmpty &&
                    Runtime.AuthorityExecution!.Status.State == AuthorityExecutionState.Running &&
                    Field("_pendingWorldStop").GetValue(null) == null &&
                    (int)Field("_stopRequested").GetValue(null)! == 0) { return; }

                await Task.Delay(2);
            }

            Assert.Fail("The host did not observe the failed final save.");
        }

        internal Task CloseWorldAsync() => _controller.CloseAsync();

        public async ValueTask DisposeAsync()
        {
            ServerStarter.EndStopRequestHandling();
            try
            {
                await _controller.CloseAsync();
                await Runtime.DisposeAsync();
            }
            finally
            {
                foreach (var (field, value) in _previous) { field.SetValue(null, value); }

                if (Directory.Exists(_directory)) { Directory.Delete(_directory, true); }
            }
        }
    }

    private sealed class ThreadObserver : Subsystem
    {
        private volatile bool _failSave;

        internal bool FailSave { get => _failSave; set => _failSave = value; }

        internal ConcurrentQueue<int> SaveThreads { get; } = new();

        internal ConcurrentQueue<int> DisposeThreads { get; } = new();

        public override void Save(ValuesDictionary valuesDictionary)
        {
            SaveThreads.Enqueue(Environment.CurrentManagedThreadId);
            if (FailSave) { throw new IOException("Simulated server world save failure."); }
        }

        public override void Dispose() => DisposeThreads.Enqueue(Environment.CurrentManagedThreadId);
    }
}

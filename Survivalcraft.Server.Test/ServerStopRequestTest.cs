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
    public void StopRequestedDuringATickSavesAndDisposesOnlyOnTheServerThread()
    {
        using var scope = new StopScope();
        using var tickStarted = new ManualResetEventSlim();
        using var finishTick = new ManualResetEventSlim();
        Exception? failure = null;
        var stopped = false;
        var server = new Thread(() =>
            {
                try
                {
                    ServerStarter.BeginStopRequestHandling();
                    tickStarted.Set();
                    finishTick.Wait();
                    stopped = scope.ProcessStopTick();
                }
                catch (Exception exception) { failure = exception; }
            }
        );
        server.Start();
        try
        {
            Assert.True(tickStarted.Wait(TimeSpan.FromSeconds(5)));
            ServerStarter.RequestStop();
            Assert.Empty(scope.Observer.SaveThreads);
            Assert.Empty(scope.Observer.DisposeThreads);
            Assert.False(scope.Project.IsDisposed);
        }
        finally
        {
            finishTick.Set();
            Assert.True(server.Join(TimeSpan.FromSeconds(5)));
        }

        Assert.Null(failure);
        Assert.True(stopped);
        Assert.Equal([server.ManagedThreadId], scope.Observer.SaveThreads);
        Assert.Equal([server.ManagedThreadId], scope.Observer.DisposeThreads);
        Assert.True(scope.Node.Runtime.IsClosed);
    }

    [Fact]
    public void EarlyRequestSurvivesInitializationAndFailedSaveRequiresAnExplicitRetry()
    {
        using var scope = new StopScope();
        scope.Observer.FailSave = true;
        ServerStarter.RequestStop();
        ServerStarter.BeginStopRequestHandling();

        Assert.False(scope.ProcessStopTick());
        Assert.False(scope.ProcessStopTick());
        Assert.Single(scope.Observer.SaveThreads);
        Assert.Empty(scope.Observer.DisposeThreads);
        Assert.False(scope.Project.IsDisposed);
        Assert.False(scope.Node.Runtime.IsClosed);

        scope.Observer.FailSave = false;
        ServerStarter.RequestStop();
        Assert.True(scope.ProcessStopTick());
        Assert.Equal(2, scope.Observer.SaveThreads.Count);
        Assert.Single(scope.Observer.DisposeThreads);

        // A late Activity.OnDestroy after shutdown must not stop the next run.
        ServerStarter.RequestStop();
        ServerStarter.BeginStopRequestHandling();
        Assert.False(ServerStarter.ProcessStopRequest());
    }

    [Fact]
    public void CancelledNewOwnerStopsEvenWhenItsTaskStartsAfterThePreviousRunEnded()
    {
        using var scope = new StopScope();
        ServerStarter.EndStopRequestHandling();
        using var owner = new CancellationTokenSource();
        owner.Cancel();

        using var registration = ServerStarter.BeginStopRequestHandling(owner.Token);
        Assert.True(scope.ProcessStopTick());
        Assert.Single(scope.Observer.SaveThreads);
        Assert.Single(scope.Observer.DisposeThreads);
    }

    [Fact]
    public void DestroyingAnOldOwnerCannotStopTheNextRun()
    {
        using var scope = new StopScope();
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
        Assert.True(scope.ProcessStopTick());
        Assert.Single(scope.Observer.DisposeThreads);
    }

    [Fact]
    public void FailedSaveRejectsTheActualRestartCallbackBeforePublishingIntent()
    {
        using var scope = new StopScope();
        var previousAction = ServerStarter.ExitAction;
        scope.Observer.FailSave = true;
        ServerStarter.BeginStopRequestHandling();
        Assert.Throws<WorldSaveException>(() => ServerStarter.ProcessCommands.Restart(new SessionInfo()));
        Assert.Equal(previousAction, ServerStarter.ExitAction);
        Assert.False(scope.ProcessStopTick());
        Assert.False(scope.Project.IsDisposed);
        Assert.Single(scope.Observer.SaveThreads);
    }

    [Fact]
    public void ActualRestartCallbackSavesTheWorldAndPersistsItsRecoverableSessionBeforeStopping()
    {
        using var scope = new StopScope();
        var paths = new[] { GamePaths.RunningSettingFile, GamePaths.SessionInfoFile };
        var backups = paths.ToDictionary(
            path => path,
            path =>
                Storage.FileExists(path) ? File.ReadAllBytes(Storage.GetSystemPath(path)) : null
        );
        var previousAction = ServerStarter.ExitAction;
        try
        {
            ServerStarter.BeginStopRequestHandling();
            var source = new SessionInfo { World = "RestartWorld", ServerPort = 43971, HttpCommandEnabled = true };
            ServerStarter.ProcessCommands.Restart(source);
            Assert.Equal(GameExitAction.Restart, ServerStarter.ExitAction);
            Assert.Single(scope.Observer.SaveThreads);
            Assert.False(scope.Project.IsDisposed);
            var settings = StartupManager.Profiles.LoadRunningSettings();
            Assert.False(string.IsNullOrWhiteSpace(settings.PendingSessionId));
            var pending = StartupManager.Profiles.LoadSession(settings.PendingSessionId);
            Assert.Equal(source.World, pending.World);
            Assert.Equal(source.ServerPort, pending.ServerPort);
            Assert.Equal(source.HttpCommandEnabled, pending.HttpCommandEnabled);
            Assert.Equal(string.Empty, source.SessionId);
            Assert.True(scope.ProcessStopTick());
            Assert.True(scope.Project.IsDisposed);
            Assert.Single(scope.Observer.DisposeThreads);
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

    private sealed class StopScope : IDisposable
    {
        private static readonly FieldInfo _worldsField =
            typeof(ServerStarter).GetField("_worlds", BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _runningField =
            typeof(ServerStarter).GetField("_running", BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _stopRequestedField =
            typeof(ServerStarter).GetField("_stopRequested", BindingFlags.Static | BindingFlags.NonPublic)!;

        private readonly GameWorldController _controller = (GameWorldController)_worldsField.GetValue(null)!;


        private readonly object? _previousRunning = _runningField.GetValue(null);

        private readonly object? _previousStopRequested = _stopRequestedField.GetValue(null);

        private readonly string _directory = Path.Combine(
            Path.GetTempPath(),
            "scnet-stop-" + Guid.NewGuid().ToString("N")
        );

        private readonly SubsystemWorldLifecycle _lifecycle;

        internal NetNode Node { get; } = new(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );

        internal ServerTestProject Project { get; }

        internal GameRuntime Runtime { get; } = new(false);

        internal ThreadObserver Observer { get; } = new();

        internal StopScope()
        {
            Assert.Null(_controller.Runtime);

            _stopRequestedField.SetValue(null, 0);
            var scheme = "server-stop-" + Guid.NewGuid().ToString("N");
            Storage.RegisterFileSystemRoot(scheme, _directory);
            Project = new ServerTestProject(Node.Runtime);
            var catalog = new BlockContentCatalog(
                BlockRuntimeCatalog.Compile(
                    [
                        new(ResourceId.Parse("game:air"), new BlockRegistration(static () => new AirBlock()))
                    ]
                )
            );
            _ = new WorldContentBinding(Node.Runtime, catalog, catalog.Dispose);
            Project.AddSubsystem(
                new SubsystemGameInfo
                    { WorldSettings = new WorldSettings(), DirectoryName = scheme + ":/world" }
            );
            Project.AddSubsystem(Observer);
            _lifecycle = new SubsystemWorldLifecycle();
            Project.AddSubsystem(_lifecycle);
            _lifecycle.Load(new ValuesDictionary());
            _lifecycle.Stopped += _ => ServerStarter.EndStopRequestHandling();
            Runtime.Attach(Node.Runtime);
            typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!
                .SetValue(_controller, Runtime);
        }

        internal bool ProcessStopTick()
        {
            var immediate = ServerStarter.ProcessStopRequest();
            Node.Update();
            _lifecycle.Update(0);
            return immediate || Project.IsDisposed;
        }

        public void Dispose()
        {
            ServerStarter.EndStopRequestHandling();
            _controller.Close();
            Project.Dispose();

            _runningField.SetValue(null, _previousRunning);
            _stopRequestedField.SetValue(null, _previousStopRequested);
            if (Directory.Exists(_directory)) { Directory.Delete(_directory, true); }
        }
    }

    private sealed class ThreadObserver : Subsystem
    {
        internal bool FailSave { get; set; }

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

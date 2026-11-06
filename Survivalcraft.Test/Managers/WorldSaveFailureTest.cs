using System.Collections.Concurrent;
using System.Reflection;

using Engine.Core.Logging;
using Engine.Core.Runtime;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Managers;
using Game.Network;
using Game.Startup;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldSaveFailureTest
{
    [Theory]
    [InlineData(true, false, false, SaveTransition.Save, false)]
    [InlineData(false, true, false, SaveTransition.Save, false)]
    [InlineData(true, true, true, SaveTransition.Save, false)]
    [InlineData(false, true, false, SaveTransition.Close, false)]
    [InlineData(false, true, false, SaveTransition.LeaveLocal, false)]
    [InlineData(false, true, false, SaveTransition.Exit, false)]
    [InlineData(false, false, false, SaveTransition.SaveAsync, false)]
    [InlineData(false, false, false, SaveTransition.SaveAsync, true)]
    [InlineData(false, false, false, SaveTransition.Save, true)]
    public void FailedSavesKeepTheirWorldAliveAndOnlyReportWithinTheirSession(
        bool hasPresentation,
        bool showDialog,
        bool replaceWorld,
        SaveTransition transition,
        bool captureFailure
    )
    {
        var controller = (GameWorldController)typeof(GameManager)
            .GetField("_worlds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var runtimeProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!;
        var generationField = typeof(GameManager).GetField(
            "_runtimeGeneration",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var previousGeneration = (long)generationField.GetValue(null)!;
        var dispatcherThread = typeof(Dispatcher).GetField(
            "_mainThreadId",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        var previousRuntime = GameManager.Runtime;

        var previousThread = dispatcherThread.GetValue(null);
        var sink = new MemorySink();
        using var scope = new WorldCatalogScope(
            new NetNode(
                NetNodeOptions.Default,
                hasPresentation,
                Game.Network.Handling.PackageHandlerCatalog.Empty,
                NodeRole.Authority
            )
        );
        using var savingRuntime = new GameRuntime(false);
        using var clientProject = new RuntimeTestProject(RuntimeSide.Client);
        using var nextProject = new RuntimeTestProject(RuntimeSide.Authority);
        using var nextRuntime = new GameRuntime(false);
        var name = "save-failure-" + Guid.NewGuid().ToString("N");
        scope.Project.AddSubsystem(
            new SubsystemGameInfo
            {
                WorldSettings = new WorldSettings { Name = name },
                DirectoryName = "missing-" + name + ":world"
            }
        );
        using var saveReported = new ManualResetEventSlim();
        var lifecycle = new SubsystemWorldLifecycle();
        scope.Project.AddSubsystem(lifecycle);
        lifecycle.Load(new ValuesDictionary());
        savingRuntime.Attach(WorldRuntimeContext.For(scope.Project));
        if (captureFailure) { scope.Project.AddSubsystem(new FailingSaveSubsystem()); }

        if (transition == SaveTransition.LeaveLocal) { savingRuntime.Attach(WorldRuntimeContext.For(clientProject)); }

        nextRuntime.Attach(WorldRuntimeContext.For(nextProject));
        var exitRequests = 0;
        void OnExit(GameExitAction action) => exitRequests++;
        GameExitManager.ExitRequested += OnExit;
        try
        {
            Dispatcher.Initialize();
            Log.AddLogSink(sink);
            runtimeProperty.SetValue(controller, savingRuntime);
            typeof(GameManager).GetMethod("BindWorldLifecycle", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [scope.Project]);
            lifecycle.Persistence.SaveCompleted += _ => saveReported.Set();

            Action save = transition switch
            {
                SaveTransition.Close => GameManager.CloseWorld,
                SaveTransition.LeaveLocal => GameManager.LeaveLocalPlayer,
                SaveTransition.Exit => GameExitManager.RequestExit,
                SaveTransition.SaveAsync => () => GameManager.SaveProject(false, showDialog),
                _ => () => GameManager.SaveProject(true, showDialog)
            };
            if (transition == SaveTransition.SaveAsync)
            {
                save();
                GameManager.WaitForPendingSave();
                Assert.True(saveReported.Wait(TimeSpan.FromSeconds(5)));
            }
            else { Assert.Throws<WorldSaveException>(save); }

            Assert.Equal(0, exitRequests);
            if (transition == SaveTransition.LeaveLocal)
            {
                Assert.Same(clientProject, savingRuntime.Client!.Project);
                Assert.False(clientProject.IsDisposed);
            }

            Assert.False(scope.Project.IsDisposed);
            Assert.False(WorldRuntimeContext.For(scope.Project).IsClosed);
            if (replaceWorld)
            {
                runtimeProperty.SetValue(controller, nextRuntime);
                generationField.SetValue(null, previousGeneration + 1);
            }

            Dispatcher.BeforeFrame();

            Assert.Contains(sink.Messages, item => item.Type == LogType.Error && item.Message.Contains(name));
            Assert.DoesNotContain(sink.Messages, item => item.Message.Contains("Dispatched action failed."));
            Assert.DoesNotContain(sink.Messages, item => item.Message.StartsWith("Saved project,"));
            Assert.Same(replaceWorld ? nextRuntime : savingRuntime, GameManager.Runtime);
        }
        finally
        {
            GameExitManager.ExitRequested -= OnExit;
            Log.RemoveLogSink(sink);
            dispatcherThread.SetValue(null, previousThread);

            runtimeProperty.SetValue(controller, previousRuntime);
            generationField.SetValue(null, previousGeneration);
        }
    }

    public enum SaveTransition
    {
        Save = 0,

        Close = 1,

        LeaveLocal = 2,

        Exit = 3,

        SaveAsync = 4
    }

    private sealed class FailingSaveSubsystem : Subsystem
    {
        public override void Save(ValuesDictionary valuesDictionary)
        {
            throw new IOException("Simulated snapshot capture failure.");
        }
    }

    private sealed class MemorySink : ILogSink
    {
        internal ConcurrentQueue<(LogType Type, string Message)> Messages { get; } = [];

        public void Log(LogType type, string message) => Messages.Enqueue((type, message));
    }
}

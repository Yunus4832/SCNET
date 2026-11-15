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
        var previousRuntime = GameManager.Runtime;
        using var frame = new FrameTestLoop();
        var sink = new MemorySink();
        WorldCatalogScope? scope = null;
        var savingRuntime = new GameRuntime(false, Dispatcher.InvokeAsync);
        using var clientProject = new RuntimeTestProject(RuntimeSide.Client);
        var nextRuntime = new GameRuntime(false, Dispatcher.InvokeAsync);
        var name = "save-failure-" + Guid.NewGuid().ToString("N");
        using var saveReported = new ManualResetEventSlim();
        frame.Complete(
            savingRuntime.StartAuthorityAsync(_ =>
                {
                    scope = new WorldCatalogScope(
                        new NetNode(
                            NetNodeOptions.Default,
                            hasPresentation,
                            Game.Network.Handling.PackageHandlerCatalog.Empty,
                            NodeRole.Authority
                        )
                    );
                    scope!.Project.AddSubsystem(
                        new SubsystemGameInfo
                        {
                            WorldSettings = new WorldSettings { Name = name },
                            DirectoryName = "missing-" + name + ":world"
                        }
                    );
                    var lifecycle = new SubsystemWorldLifecycle();
                    scope!.Project.AddSubsystem(lifecycle);
                    lifecycle.Load(new ValuesDictionary());
                    lifecycle.Persistence.SaveCompleted += _ => saveReported.Set();
                    if (captureFailure) { scope!.Project.AddSubsystem(new FailingSaveSubsystem()); }

                    return WorldRuntimeContext.For(scope!.Project);
                }
            )
        );
        if (transition == SaveTransition.LeaveLocal)
        {
            savingRuntime.AttachClient(WorldRuntimeContext.For(clientProject));
        }

        var exitRequests = 0;
        void OnExit(GameExitAction action) => exitRequests++;
        GameExitManager.ExitRequested += OnExit;
        try
        {
            Log.AddLogSink(sink);
            runtimeProperty.SetValue(controller, savingRuntime);
            frame.Complete(
                savingRuntime.AuthorityExecution!.InvokeAsync(_ =>
                    {
                        typeof(GameManager).GetMethod(
                                "BindWorldLifecycle",
                                BindingFlags.Static | BindingFlags.NonPublic
                            )!
                            .Invoke(null, [scope!.Project]);
                        return true;
                    }
                )
            );

            Task save;
            if (transition == SaveTransition.Exit)
            {
                GameExitManager.RequestExit();
                save = GameExitManager.PendingTransition;
            }
            else
            {
                save = transition switch
                {
                    SaveTransition.Close => GameManager.CloseWorldAsync(),
                    SaveTransition.LeaveLocal => GameManager.LeaveLocalPlayerAsync(),
                    _ => GameManager.SaveProjectAsync(showDialog)
                };
            }

            Assert.Throws<WorldSaveException>(() => frame.Complete(save));
            Assert.True(saveReported.IsSet);

            Assert.Equal(0, exitRequests);
            if (transition == SaveTransition.LeaveLocal)
            {
                Assert.Same(clientProject, savingRuntime.Client!.Project);
                Assert.False(clientProject.IsDisposed);
            }

            Assert.False(scope!.Project.IsDisposed);
            Assert.False(WorldRuntimeContext.For(scope!.Project).IsClosed);
            if (replaceWorld)
            {
                runtimeProperty.SetValue(controller, nextRuntime);
                generationField.SetValue(null, previousGeneration + 1);
            }

            Dispatcher.BeforeFrame();

            Assert.Contains(sink.Messages, item => item.Type == LogType.Error && item.Message.Contains(name));
            Assert.DoesNotContain(sink.Messages, item => item.Message.Contains("Dispatched action failed."));
            Assert.Same(replaceWorld ? nextRuntime : savingRuntime, GameManager.Runtime);
        }
        finally
        {
            GameExitManager.ExitRequested -= OnExit;
            Log.RemoveLogSink(sink);
            frame.Complete(savingRuntime.DisposeAsync().AsTask());
            frame.Complete(nextRuntime.DisposeAsync().AsTask());
            scope?.Dispose();
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

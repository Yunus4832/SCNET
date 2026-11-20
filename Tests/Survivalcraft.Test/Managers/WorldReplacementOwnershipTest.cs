using System.Reflection;

using Engine.Core.Runtime;

using EntitySystem.TemplatesDatabase;

using Game.Managers;
using Game.Network;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldReplacementOwnershipTest
{
    [Fact]
    public async Task AQueuedStopNotificationCannotRetireAReplacementWorld()
    {
        using var frame = new FrameTestLoop();
        var controller =
            typeof(GameManager).GetField("_worlds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var runtimeProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!;
        var previous = GameManager.Runtime;
        var first = new GameRuntime(false, Dispatcher.InvokeAsync);
        var second = new GameRuntime(false, Dispatcher.InvokeAsync);
        WorldCatalogScope? scope = null;
        using var replacement = new RuntimeTestProject(RuntimeSide.Client);
        try
        {
            frame.Complete(
                first.StartAuthorityAsync(_ =>
                    {
                        scope = new WorldCatalogScope();
                        var lifecycle = new SubsystemWorldLifecycle();
                        scope.Project.AddSubsystem(lifecycle);
                        lifecycle.Load(new ValuesDictionary());
                        return WorldRuntimeContext.For(scope.Project);
                    }
                )
            );
            runtimeProperty.SetValue(controller, first);
            frame.Complete(
                first.AuthorityExecution!.InvokeAsync(world =>
                    {
                        typeof(GameManager).GetMethod(
                                "BindWorldLifecycle",
                                BindingFlags.Static | BindingFlags.NonPublic
                            )!
                            .Invoke(null, [world.Project]);
                        return true;
                    }
                )
            );
            // Deliberately withhold frame dispatch until the replacement is installed.
            await first.AuthorityExecution.StopAsync(false).WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.Initialize();
            second.AttachClient(WorldRuntimeContext.For(replacement));
            runtimeProperty.SetValue(controller, second);
            var transition = GameManager.PendingWorldTransition;
            Dispatcher.BeforeFrame();
            Assert.Same(transition, GameManager.PendingWorldTransition);
            Assert.Same(second, GameManager.Runtime);
            Assert.False(replacement.IsDisposed);
            Assert.False(second.IsDisposed);
        }
        finally
        {
            runtimeProperty.SetValue(controller, previous);
            frame.Complete(first.DisposeAsync().AsTask());
            frame.Complete(second.DisposeAsync().AsTask());
            scope?.Dispose();
        }
    }

    [Fact]
    public async Task LoadingAnotherWorldCannotDisposeAnExistingAuthorityWithoutListeningPorts()
    {
        var controller = (GameWorldController)typeof(GameManager)
            .GetField("_worlds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var runtimeProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!;
        var previousRuntime = GameManager.Runtime;
        var previousRole = CommonLib.Net.Role;
        RuntimeTestProject? project = null;
        await using var session = new GameRuntime(false);
        await session.StartAuthorityAsync(_ =>
            {
                project = new RuntimeTestProject(RuntimeSide.Authority);
                return WorldRuntimeContext.For(project);
            }
        );
        try
        {
            CommonLib.Net.Role = NodeRole.Authority;
            runtimeProperty.SetValue(controller, session);
            Assert.False(CommonLib.Net.IsTransportRunning);

            await Assert.ThrowsAsync<InvalidOperationException>(() => controller.LoadAuthorityAsync(null!));

            Assert.Same(session, GameManager.Runtime);
            Assert.Same(project, session.Authority!.BoundProject);
            Assert.False(project!.IsDisposed);
            Assert.False(session.Authority.IsClosed);
        }
        finally
        {
            runtimeProperty.SetValue(controller, previousRuntime);
            CommonLib.Net.Role = previousRole;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparingAnAuthorityCannotResetSessionsWhileItsProjectIsStillOwned(bool closed)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var node = WorldNetworkBinding.For(project).Node!;
        var player = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        node.AddClient(player);
        if (closed) { node.Runtime.Close(); }

        Assert.Throws<InvalidOperationException>(node.PrepareAuthorityWorld);

        Assert.Same(player, Assert.Single(node.Clients).Value);
        Assert.False(project!.IsDisposed);
        project.Dispose();
        node.PrepareAuthorityWorld();
        Assert.Empty(node.Clients);
    }
}

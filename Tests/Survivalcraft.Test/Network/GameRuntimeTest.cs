using System.Reflection;

using Engine.Network;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Transport;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class GameRuntimeTest
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ClientPumpDoesNotDriveTheAuthority(bool hasAuthority, bool hasClient)
    {
        await using var game = new GameRuntime(false, Inline);
        var authorityCalls = 0;
        if (hasAuthority)
        {
            await game.StartAuthorityAsync(_ => CreateAuthority(() => Interlocked.Increment(ref authorityCalls)));
            await Wait(() => Volatile.Read(ref authorityCalls) > 0);
        }

        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var calls = new List<string>();
        if (hasClient)
        {
            AddUpdate(client, () => calls.Add("client"));
            var world = WorldRuntimeContext.For(client);
            game.AttachClient(world);
            var node = WorldNetworkBinding.For(world).RequireNode();
            node.OnReceive += (_, _) => calls.Add("message");
            Assert.True(
                WorldNetworkBinding.For(world).Inbox.TryPostOwned(
                    new ReceivedPackage(new SubsystemTimePackage(1, 0), node.CreateReceiveContext())
                )
            );
        }

        game.UpdateClient(.016f, Engine.Core.Runtime.Time.RealTime);
        Assert.Equal(hasClient ? new[] { "message", "client" } : [], calls);
        if (hasAuthority)
        {
            var before = Volatile.Read(ref authorityCalls);
            await Wait(() => Volatile.Read(ref authorityCalls) > before);
        }
    }

    [Fact]
    public async Task LocalAdministrationRequiresTheLiveClientOfThisRuntime()
    {
        await using var game = new GameRuntime(false, Inline);
        await game.StartAuthorityAsync(_ => CreateAuthority());
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        using var unrelated = new RuntimeTestProject(RuntimeSide.Client);
        game.AttachClient(WorldRuntimeContext.For(client));
        Assert.Same(game.Authority, game.GetAuthorityForClient(client));
        Assert.Null(game.GetAuthorityForClient(unrelated));
        Assert.Null(game.GetAuthorityForClient(game.Authority!.Project!));
        WorldRuntimeContext.For(client).Close();
        Assert.Null(game.GetAuthorityForClient(client));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PauseIsAppliedByEachOwnerWithoutStoppingClientMessages(bool allowed)
    {
        var authorityCalls = 0;
        await using var game = new GameRuntime(allowed, Inline);
        await game.StartAuthorityAsync(_ => CreateAuthority(() => Interlocked.Increment(ref authorityCalls)));
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var clientCalls = 0;
        AddUpdate(client, () => clientCalls++);
        var world = WorldRuntimeContext.For(client);
        game.AttachClient(world);
        var node = WorldNetworkBinding.For(world).RequireNode();
        var received = 0;
        node.OnReceive += (_, _) => received++;
        WorldNetworkBinding.For(world).Inbox.TryPostOwned(
            new ReceivedPackage(new SubsystemTimePackage(1, 0), node.CreateReceiveContext())
        );
        game.SetLocalPause(true);
        await Wait(() => game.AuthorityExecution!.Status.Paused == allowed);
        game.UpdateClient(.016f, Engine.Core.Runtime.Time.RealTime);
        Assert.Equal(allowed, game.IsPaused);
        Assert.Equal(allowed ? 0 : 1, clientCalls);
        Assert.Equal(1, received);
        var factor = await game.AuthorityExecution!.InvokeAsync(owner =>
            owner.Project!.FindSubsystem<SubsystemTime>(true)!.GameTimeFactor
        );
        Assert.Equal(allowed ? 0f : 1f, factor);
        if (allowed)
        {
            var before = Volatile.Read(ref authorityCalls);
            await Task.Delay(100);
            Assert.Equal(before, Volatile.Read(ref authorityCalls));
        }

        game.SetLocalPause(false);
        await Wait(() => !game.AuthorityExecution!.Status.Paused);
    }

    [Fact]
    public async Task ReplacingClientResumesAndPreservesTheAuthority()
    {
        await using var game = new GameRuntime(true, Inline);
        await game.StartAuthorityAsync(_ => CreateAuthority());
        using var first = new RuntimeTestProject(RuntimeSide.Client);
        using var second = new RuntimeTestProject(RuntimeSide.Client);
        AddUpdate(first, () => { });
        game.AttachClient(WorldRuntimeContext.For(first));
        game.SetLocalPause(true);
        await Wait(() => game.AuthorityExecution!.Status.Paused);
        await game.ReleaseClientAsync();
        Assert.True(first.IsDisposed);
        Assert.False(game.Authority!.IsClosed);
        await Wait(() => !game.AuthorityExecution!.Status.Paused);
        game.AttachClient(WorldRuntimeContext.For(second));
        Assert.Same(second, game.Client!.Project);
        Assert.Null(game.GetAuthorityForClient(first));
        Assert.NotNull(game.GetAuthorityForClient(second));
    }

    [Fact]
    public async Task ClientCancellationFailureDoesNotLeakAuthorityOrClientResources()
    {
        var authorityReleased = false;
        await using var game = new GameRuntime(false, Inline);
        await game.StartAuthorityAsync(_ =>
            {
                var world = CreateAuthority();
                world.Project!.Disposed += _ => authorityReleased = true;
                return world;
            }
        );
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var context = WorldRuntimeContext.For(client);
        game.AttachClient(context);
        using var cancellation =
            context.Lifetime.Register(() => throw new InvalidOperationException("Cancellation failed."));
        await Assert.ThrowsAsync<AggregateException>(() => game.CloseAsync(false));
        Assert.True(authorityReleased);
        Assert.True(client.IsDisposed);
        Assert.True(context.IsClosed);
        Assert.True(game.IsDisposed);
    }

    [Fact]
    public async Task AHostCannotAttachAnUnownedAuthorityAsAClient()
    {
        await using var game = new GameRuntime(false, Inline);
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        Assert.Throws<ArgumentException>(() => game.AttachClient(WorldRuntimeContext.For(authority)));
    }

    [Fact]
    public async Task SlowAuthorityDoesNotPreventClientUpdates()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        await using var game = new GameRuntime(false, Inline);
        await game.StartAuthorityAsync(_ => CreateAuthority());
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var calls = 0;
        AddUpdate(client, () => calls++);
        game.AttachClient(WorldRuntimeContext.For(client));
        var blocked = game.AuthorityExecution!.InvokeAsync(_ =>
            {
                entered.Set();
                release.Wait();
                return 0;
            }
        );
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            for (var i = 0; i < 30; i++) { game.UpdateClient(.016f, Engine.Core.Runtime.Time.RealTime); }

            Assert.Equal(30, calls);
            Assert.False(blocked.IsCompleted);
        }
        finally
        {
            release.Set();
            await blocked;
        }
    }

    [Fact]
    public async Task AuthorityTransportShutdownStillRetiresItsBoundProject()
    {
        await using var game = new GameRuntime(false, Inline);
        RuntimeTestProject? project = null;
        await game.StartAuthorityAsync(_ =>
            {
                var world = CreateAuthority();
                project = (RuntimeTestProject)world.Project!;
                return world;
            }
        );
        await game.AuthorityExecution!.InvokeAsync(world =>
            {
                WorldNetworkBinding.For(world).RequireNode().StopImmediate();
                return 0;
            }
        );
        await game.AuthorityExecution.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(project!.IsDisposed);
        Assert.True(game.Authority!.IsClosed);
    }

    [Fact]
    public async Task ReleasingAnAlreadyDisposedClientStopsItsEndpoint()
    {
        await using var game = new GameRuntime(false, Inline);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var world = WorldRuntimeContext.For(client);
        var node = WorldNetworkBinding.For(world).RequireNode();
        game.AttachClient(world);
        client.Dispose();
        await game.ReleaseClientAsync();
        Assert.True(world.IsClosed);
        Assert.Equal(NodeRole.Inactive, node.Role);
        Assert.Null(game.Client);
    }

    [Fact]
    public void PendingTransportShutdownDoesNotBlockFramesOrDisposeTheProjectEarly()
    {
        using var frame = new FrameTestLoop();
        var game = new GameRuntime(false, Engine.Core.Runtime.Dispatcher.InvokeAsync);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var world = WorldRuntimeContext.For(client);
        var node = WorldNetworkBinding.For(world).RequireNode();
        var connecting =
            new TaskCompletionSource<NetworkConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        typeof(NetNode).GetField("_connecting", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            node,
            connecting.Task
        );
        game.AttachClient(world);
        var retiring = game.ReleaseClientAsync();
        try
        {
            Assert.False(retiring.IsCompleted);
            Assert.True(world.IsClosed);
            Assert.False(client.IsDisposed);
            Assert.Same(retiring, game.ReleaseClientAsync());
            var closing = game.CloseAsync(false);
            Assert.False(closing.IsCompleted);
            var frameCalls = 0;
            Engine.Core.Runtime.Dispatcher.Dispatch(() => frameCalls++);
            Engine.Core.Runtime.Dispatcher.BeforeFrame();
            Assert.Equal(1, frameCalls);
            connecting.SetCanceled();
            frame.Complete(closing);
            Assert.True(client.IsDisposed);
            Assert.True(game.IsDisposed);
        }
        finally
        {
            connecting.TrySetCanceled();
            frame.Complete(game.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public void ForcedPlatformRetirementCompletesAnAlreadyQueuedClientRelease()
    {
        using var frame = new FrameTestLoop();
        var owner = Environment.CurrentManagedThreadId;
        var game = new GameRuntime(false, Engine.Core.Runtime.Dispatcher.InvokeAsync);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var disposedOn = 0;
        client.Disposed += _ => disposedOn = Environment.CurrentManagedThreadId;
        game.AttachClient(WorldRuntimeContext.For(client));
        Task? retiring = null;
        using var requested = new ManualResetEventSlim();
        var requester = Task.Run(() =>
            {
                retiring = game.ReleaseClientAsync();
                requested.Set();
            }
        );
        Assert.True(requested.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(retiring!.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => game.AttachClient(WorldRuntimeContext.For(client)));
        game.ReleaseClientForHostShutdown();
        frame.Complete(retiring);
        Assert.True(client.IsDisposed);
        Assert.Equal(owner, disposedOn);
        frame.Complete(requester);
        frame.Complete(game.DisposeAsync().AsTask());
    }

    private static WorldRuntimeContext CreateAuthority(Action? update = null)
    {
        var project = new RuntimeTestProject(RuntimeSide.Authority);
        if (update != null) { AddUpdate(project, update); }

        return WorldRuntimeContext.For(project);
    }

    private static Task Inline(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private static async Task Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 200; i++)
        {
            if (predicate()) { return; }

            await Task.Delay(10);
        }

        Assert.True(predicate());
    }

    private static void AddUpdate(RuntimeTestProject project, Action action)
    {
        var update = new SubsystemUpdate { UpdatesPerTick = 1 };
        var time = new SubsystemTime();
        project.AddSubsystem(new SubsystemCharacters());
        project.AddSubsystem(time);
        project.AddSubsystem(update);
        time.Load(new ValuesDictionary());
        update.Load(new ValuesDictionary());
        var pending = (Dictionary<IUpdateable, bool>)typeof(SubsystemUpdate)
            .GetField("_toAddOrRemove", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(update)!;
        pending[new UpdateProbe(action)] = true;
    }

    private sealed class UpdateProbe(Action action) : IUpdateable
    {
        public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public void Update(float dt) => action();
    }
}

using EntitySystem.Core;

using Game.Modding;
using Game.World;

namespace Survivalcraft.Test.Modding;

public sealed class ModWorldStateTest
{
    [Fact]
    public void MutableStateIsIndependentAndSurvivesUntilItsOwnProjectIsDisposed()
    {
        var mod = new TestMod();
        var host = Start(mod);
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var authority = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var client = new WorldRuntimeContext(
            RuntimeSide.Client,
            (true)
        );
        authority.BindProject(authorityProject);
        client.BindProject(clientProject);
        var serverState = mod.Primary.Get(authority);
        var clientState = mod.Primary.Get(client);
        clientState.OnDispose = () => Assert.Same(client, WorldRuntimeContext.For(clientProject));
        serverState.Value = 7;

        Assert.Same(serverState, mod.Primary.Get(authority));
        Assert.NotSame(serverState, clientState);
        Assert.Equal(0, clientState.Value);
        Assert.Throws<InvalidOperationException>(host.StopAll);
        Assert.False(mod.Stopped);

        client.Close();
        Assert.True(client.Lifetime.IsCancellationRequested);
        Assert.Equal(0, clientState.Disposals);
        Assert.Throws<ObjectDisposedException>(() => mod.Primary.Get(client));
        clientProject.Dispose();
        Assert.Equal(1, clientState.Disposals);
        Assert.Equal(0, serverState.Disposals);
        Assert.Same(serverState, mod.Primary.Get(authority));

        authorityProject.Dispose();
        Assert.Equal(1, serverState.Disposals);
        host.StopAll();
        Assert.True(mod.Stopped);
        Assert.Throws<ObjectDisposedException>(() => mod.Primary.Get(
                new WorldRuntimeContext(
                    RuntimeSide.Client,
                    (true)
                )
            )
        );
    }

    [Fact]
    public void PreProjectStateIsCancelledAndReleasedOnCloseAndNotReusedByNextRuntime()
    {
        var mod = new TestMod();
        var host = Start(mod);
        var first = new WorldRuntimeContext(
            RuntimeSide.Client,
            (true)
        );
        var state = mod.Primary.Get(first);
        first.Close();
        first.Close();

        Assert.Equal(1, state.Disposals);
        Assert.True(state.CancelledWhenDisposed);
        var next = new WorldRuntimeContext(
            RuntimeSide.Client,
            (true)
        );
        Assert.NotSame(state, mod.Primary.Get(next));
        next.Close();
        host.StopAll();
    }

    [Fact]
    public void FailedResourceDisposalStillReleasesOtherModStates()
    {
        var mod = new TestMod();
        var host = Start(mod);
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var first = mod.Primary.Get(runtime);
        var second = mod.Secondary.Get(runtime);
        second.ThrowOnDispose = true;

        Assert.Throws<AggregateException>(runtime.Close);
        Assert.Equal(1, first.Disposals);
        Assert.Equal(1, second.Disposals);
        Assert.True(first.CancelledWhenDisposed);
        runtime.Close();
        host.StopAll();
    }

    [Fact]
    public void FactoryClosingItsRuntimeCannotLeakItsNewState()
    {
        var mod = new TestMod { CloseDuringFactory = true };
        var host = Start(mod);
        var runtime = new WorldRuntimeContext(
            RuntimeSide.Client,
            (true)
        );

        Assert.Throws<ObjectDisposedException>(() => mod.Primary.Get(runtime));
        Assert.Equal(1, mod.Created.Single().Disposals);
        host.StopAll();
    }

    [Fact]
    public void RecursiveFactoryFailsWithoutRetainingAnActiveWorld()
    {
        var mod = new TestMod { ResolveDuringFactory = true };
        var host = Start(mod);
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);

        Assert.Throws<InvalidOperationException>(() => mod.Primary.Get(runtime));
        runtime.Close();
        host.StopAll();
    }

    [Fact]
    public void SingletonFactoryCannotShareMutableStateBetweenWorlds()
    {
        var mod = new TestMod { ReuseState = true };
        var host = Start(mod);
        var authority = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var client = new WorldRuntimeContext(
            RuntimeSide.Client,
            (true)
        );
        var state = mod.Primary.Get(authority);

        Assert.Throws<InvalidOperationException>(() => mod.Primary.Get(client));
        Assert.Equal(0, state.Disposals);
        client.Close();
        authority.Close();
        Assert.Equal(1, state.Disposals);
        host.StopAll();
    }

    private static ModHost Start(TestMod mod)
    {
        var host = new ModHost();
        host.LoadAndStart([new ModDescriptor(new ModManifest("example.state", "State", "1.0.0"), () => mod)]);
        return host;
    }

    private sealed class TestMod : IMod
    {
        public ModWorldState<State> Primary { get; private set; } = null!;

        public ModWorldState<State> Secondary { get; private set; } = null!;

        public bool Stopped;

        public bool CloseDuringFactory;

        public bool ResolveDuringFactory;

        public bool ReuseState;

        public List<State> Created { get; } = [];

        public void Configure(IModContext context)
        {
            Primary = context.Worlds.Register(
                new ResourceId(context.Manifest.ModId, "primary"),
                runtime =>
                {
                    if (ResolveDuringFactory) { return Primary.Get(runtime); }

                    if (ReuseState && Created.Count > 0) { return Created[0]; }

                    var state = new State(runtime);
                    Created.Add(state);
                    if (CloseDuringFactory) { runtime.Close(); }

                    return state;
                }
            );
            Secondary = context.Worlds.Register(
                new ResourceId(context.Manifest.ModId, "secondary"),
                runtime => new State(runtime)
            );
        }

        public void Start(IModContext context)
        {
        }

        public void Stop() => Stopped = true;
    }

    private sealed class State(WorldRuntimeContext runtime) : IDisposable
    {
        public int Value;

        public int Disposals;

        public bool ThrowOnDispose;

        public bool CancelledWhenDisposed;

        public Action? OnDispose;

        public void Dispose()
        {
            Disposals++;
            OnDispose?.Invoke();
            CancelledWhenDisposed = runtime.Lifetime.IsCancellationRequested;
            if (ThrowOnDispose) { throw new InvalidOperationException("Test disposal failure."); }
        }
    }
}

using EntitySystem.Core;

using Game.Network.Transport;
using Game.World;

namespace Survivalcraft.Core.Test.World;

public sealed class WorldRuntimeContextTest
{
    [Fact]
    public void PresentationIsAnExplicitClientCapability()
    {
        Assert.Throws<ArgumentException>(() => new WorldRuntimeContext(RuntimeSide.Authority, true));
        var client = new WorldRuntimeContext(RuntimeSide.Client, true);
        Assert.True(client.HasPresentation);
        var authority = new WorldRuntimeContext(RuntimeSide.Authority, false);
        Assert.False(authority.HasPresentation);
        client.Close();
        authority.Close();
    }

    [Fact]
    public void CancellationImmediatelyReleasesOwnedInboxMessagesExactlyOnce()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        var message = new Resource();
        var inbox = new OwnedMessageInbox<Resource>(() => world.IsClosed, value => value.Dispose());
        using var registration = world.Lifetime.Register(inbox.Clear);
        world.OwnWorldResource(new Resource(inbox.Clear));
        Assert.True(inbox.TryPostOwned(message, new object(), 1));
        world.Close();
        world.Close();
        Assert.Equal(0, inbox.Count);
        Assert.Equal(1, message.Count);
    }

    [Fact]
    public void ProjectDisposalReleasesOrdinaryThenFinalResourcesAfterImmediateClose()
    {
        using var project = new Project();
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        world.BindProject(project);
        var calls = new List<string>();
        using var registration = world.Lifetime.Register(() => calls.Add("cancel"));
        world.OwnFinalWorldResource(new Resource(() => calls.Add("catalog")));
        world.OwnWorldResource(new Resource(() => calls.Add("first")));
        world.OwnWorldResource(new Resource(() => calls.Add("second")));
        world.Close();
        Assert.Null(world.Project);
        Assert.Same(project, world.BoundProject);
        Assert.Equal(new[] { "cancel" }, calls);
        project.Dispose();
        world.Close();
        Assert.Equal(new[] { "cancel", "second", "first", "catalog" }, calls);
        Assert.Null(world.BoundProject);
        Assert.Throws<InvalidOperationException>(() => WorldRuntimeContext.For(project));
    }

    [Fact]
    public void FailedResourceDoesNotSkipOtherResourcesOrTheFinalPhase()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var calls = new List<string>();
        world.OwnWorldResource(new Resource(() => calls.Add("ordinary")));
        world.OwnWorldResource(new Resource(() => throw new InvalidOperationException("ordinary failed")));
        world.OwnFinalWorldResource(new Resource(() => calls.Add("final")));
        Assert.Throws<AggregateException>(world.Close);
        world.Close();
        Assert.Equal(new[] { "ordinary", "final" }, calls);
        Assert.Throws<ObjectDisposedException>(() => world.OwnWorldResource(new Resource()));
    }

    [Fact]
    public void ClosedWorldDiscardsDelayedActionsAndNeverRebinds()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        using var project = new Project();
        world.BindProject(project);
        var calls = 0;
        world.QueueTimeDelayedExecution(1, () => calls++);
        world.Close();
        world.RunTimeDelayedExecutions(10);
        Assert.Equal(0, calls);
        Assert.Throws<ObjectDisposedException>(() => world.BindProject(new Project()));
    }

    private sealed class Resource(Action? release = null) : IDisposable
    {
        public int Count { get; private set; }

        public void Dispose()
        {
            Count++;
            release?.Invoke();
        }
    }
}

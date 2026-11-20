using EntitySystem.Core;

using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class WorldRuntimeDelayedExecutionTest
{
    [Fact]
    public void EachWorldRunsOnlyItsOwnDueActions()
    {
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var authority = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var client = new WorldRuntimeContext(
            RuntimeSide.Client,
            (true)
        );
        authority.BindProject(authorityProject);
        client.BindProject(clientProject);
        var calls = new List<string>();
        authority.QueueTimeDelayedExecution(2, () => calls.Add("authority"));
        client.QueueTimeDelayedExecution(2, () => calls.Add("client"));

        authority.RunTimeDelayedExecutions(1);
        Assert.Empty(calls);
        authority.RunTimeDelayedExecutions(2);
        Assert.Equal(["authority"], calls);
        client.RunTimeDelayedExecutions(2);
        Assert.Equal(["authority", "client"], calls);
        authority.RunTimeDelayedExecutions(3);
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public void ActionsCannotRunAfterCloseOrLeakIntoTheNextWorld()
    {
        using var oldProject = new Project();
        var oldRuntime = new WorldRuntimeContext(
            RuntimeSide.Client,
            (true)
        );
        oldRuntime.BindProject(oldProject);
        var calls = 0;
        oldRuntime.QueueTimeDelayedExecution(2, () => calls++);
        oldRuntime.Close();
        oldRuntime.QueueTimeDelayedExecution(0, () => calls++);
        using var nextProject = new Project();
        var next = new WorldRuntimeContext(
            RuntimeSide.Client,
            (true)
        );
        next.BindProject(nextProject);
        next.QueueTimeDelayedExecution(2, () => calls += 10);

        oldRuntime.RunTimeDelayedExecutions(3);
        next.RunTimeDelayedExecutions(3);
        Assert.Equal(10, calls);
    }

    [Fact]
    public void NewActionsAndRecursiveDrainsWaitForTheNextWorldUpdate()
    {
        using var project = new Project();
        var runtime = new WorldRuntimeContext(
            RuntimeSide.Client,
            (true)
        );
        runtime.BindProject(project);
        var calls = new List<int>();
        runtime.QueueTimeDelayedExecution(
            0,
            () =>
            {
                calls.Add(1);
                runtime.QueueTimeDelayedExecution(0, () => calls.Add(3));
                runtime.RunTimeDelayedExecutions(1);
            }
        );
        runtime.QueueTimeDelayedExecution(0, () => calls.Add(2));

        runtime.RunTimeDelayedExecutions(1);
        Assert.Equal([1, 2], calls);
        runtime.RunTimeDelayedExecutions(1);
        Assert.Equal([1, 2, 3], calls);
    }

    [Fact]
    public void ClosingOrDisposingTheWorldDuringACallbackStopsTheReadyBatch()
    {
        using var project = new Project();
        var runtime = new WorldRuntimeContext(RuntimeSide.Authority, false);
        runtime.BindProject(project);
        var calls = 0;
        runtime.QueueTimeDelayedExecution(0, project.Dispose);
        runtime.QueueTimeDelayedExecution(0, () => calls++);

        runtime.RunTimeDelayedExecutions(1);
        Assert.True(runtime.IsClosed);
        Assert.Equal(0, calls);
    }
}

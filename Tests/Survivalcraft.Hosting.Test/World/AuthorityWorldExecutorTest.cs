using EntitySystem.Core;

using Game.Hosting;
using Game.World;

namespace Survivalcraft.Hosting.Test.World;

public sealed class AuthorityWorldExecutorTest
{
    [Fact]
    public async Task ActiveProjectEditsAreDispatchedToTheirWorldOwnerAndSkipRetiredWorlds()
    {
        var activity = new WorldProjectActivity();
        await using var owner = Create();
        await owner.Ready;
        var owningThread = await owner.InvokeAsync(world =>
            {
                activity.Track(
                    world,
                    null,
                    operation => owner.InvokeAsync(_ =>
                        {
                            operation();
                            return true;
                        }
                    )
                );
                return Environment.CurrentManagedThreadId;
            }
        );
        var editingThread = 0;
        var calls = 0;
        await activity.ApplyToProjectsAsync(project =>
            {
                WorldRuntimeContext.For(project).VerifyExecutionAccess();
                editingThread = Environment.CurrentManagedThreadId;
                calls++;
            }
        );
        Assert.Equal(owningThread, editingThread);
        Assert.Equal(1, calls);
        await owner.StopAsync(false);
        await activity.ApplyToProjectsAsync(_ => calls++);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task LoadingRequestsAndRetirementHaveOneIndependentOwner()
    {
        var caller = Environment.CurrentManagedThreadId;
        var loadingThread = 0;
        var releasedThread = 0;
        WorldRuntimeContext? context = null;
        var owner = new AuthorityWorldExecutor(
            _ =>
            {
                loadingThread = Environment.CurrentManagedThreadId;
                var project = new Project();
                project.Disposed += _ => releasedThread = Environment.CurrentManagedThreadId;
                context = new WorldRuntimeContext(RuntimeSide.Authority, false);
                context.BindProject(project);
                return context;
            },
            false
        );
        await owner.Ready.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.NotEqual(caller, loadingThread);
            var requestThread = await owner.InvokeAsync(world =>
                {
                    world.VerifyExecutionAccess();
                    return Environment.CurrentManagedThreadId;
                }
            );
            Assert.Equal(loadingThread, requestThread);
            Assert.Equal(AuthorityExecutionState.Running, owner.Status.State);
            Assert.Throws<InvalidOperationException>(context!.VerifyExecutionAccess);
            var nested = await owner.InvokeAsync(_ => owner.InvokeAsync(_ => 17).GetAwaiter().GetResult());
            Assert.Equal(17, nested);
        }
        finally { await owner.StopAsync(false).WaitAsync(TimeSpan.FromSeconds(5)); }

        Assert.Equal(loadingThread, releasedThread);
        Assert.True(context!.IsClosed);
        Assert.Equal(AuthorityExecutionState.Stopped, owner.Status.State);
        Assert.True(owner.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task BlockedAuthorityDoesNotPreventCallerProgressAndCancelledWorkDoesNotRun()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        await using var owner = Create();
        await owner.Ready.WaitAsync(TimeSpan.FromSeconds(5));
        var blocked = owner.InvokeAsync(_ =>
            {
                entered.Set();
                release.Wait();
                return 1;
            }
        );
        var changed = false;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            var cancelled = owner.InvokeAsync(_ => changed = true, cancellation.Token);
            cancellation.Cancel();
            var pulses = 0;
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(1);
                pulses++;
            }

            Assert.Equal(10, pulses);
            Assert.False(blocked.IsCompleted);
            release.Set();
            await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            await owner.InvokeAsync(_ => 0);
            Assert.False(changed);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task AdmissionIsBoundedAndRetirementDoesNotLoseAcceptedRequests()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        await using var owner = Create();
        await owner.Ready.WaitAsync(TimeSpan.FromSeconds(5));
        var blocked = owner.InvokeAsync(_ =>
            {
                entered.Set();
                release.Wait();
                return 0;
            }
        );
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            var accepted = Enumerable.Range(0, 256).Select(i => owner.InvokeAsync(_ => i)).ToArray();
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InvokeAsync(_ => -1));
            var stop = owner.StopAsync(false);
            Assert.False(stop.IsCompleted);
            release.Set();
            await blocked;
            var results = await Task.WhenAll(accepted).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Enumerable.Range(0, 256), results);
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InvokeAsync(_ => -1));
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task CancellingLoadingWaitsForTheInitializerToExit()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var owner = new AuthorityWorldExecutor(
            token =>
            {
                entered.Set();
                release.Wait();
                token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Unreachable initialization.");
            },
            false
        );
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            var stop = owner.StopAsync(false);
            Assert.False(stop.IsCompleted);
            release.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.Ready);
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AuthorityExecutionState.Stopped, owner.Status.State);
        }
        finally
        {
            release.Set();
            await owner.DisposeAsync();
        }
    }

    private static AuthorityWorldExecutor Create() => new(
        _ =>
        {
            var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
            world.BindProject(new Project());
            return world;
        },
        false
    );
}

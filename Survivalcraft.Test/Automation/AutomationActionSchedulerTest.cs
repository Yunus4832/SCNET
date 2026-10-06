using Engine.Input;

using Game.Automation;

namespace Survivalcraft.Test.Automation;

public sealed class AutomationActionSchedulerTest
{
    [Fact]
    public void GestureCompletesAfterExactlyItsDuration()
    {
        var scheduler = new AutomationActionScheduler();
        var id = scheduler.Start(new AutomationInputAction([Key.W, Key.Shift], [], 3));
        Assert.True(scheduler.Advance());
        Assert.True(scheduler.Advance());
        Assert.False(scheduler.Advance());
        Assert.Equal(3, scheduler.ElapsedFrames);
        Assert.Equal("completed", scheduler.Status);
        Assert.Null(scheduler.Action);
        Assert.Equal(id, scheduler.Id);
    }

    [Fact]
    public void ActiveGestureCannotBeSilentlyReplaced()
    {
        var scheduler = new AutomationActionScheduler();
        var action = new AutomationInputAction([], [], 1);
        scheduler.Start(action);
        Assert.Throws<InvalidOperationException>(() => scheduler.Start(action));
        scheduler.Cancel();
        Assert.Equal("cancelled", scheduler.Status);
        Assert.False(scheduler.Advance());
        Assert.Equal(2, scheduler.Start(action));
    }

    [Fact]
    public void SubmittedArraysCannotMutateRunningGesture()
    {
        var keys = new[] { Key.W, Key.W };
        var scheduler = new AutomationActionScheduler();
        scheduler.Start(new AutomationInputAction(keys, [], 2));
        keys[0] = Key.S;
        Assert.Equal(new[] { Key.W }, scheduler.Action!.Keys);
        scheduler.Cancel("character_changed");
        Assert.Equal("character_changed", scheduler.CancellationReason);
        scheduler.Start(new AutomationInputAction([], [], 1));
        Assert.Null(scheduler.CancellationReason);
    }

    [Fact]
    public void InvalidKeyAndExcessiveMouseMotionAreRejected()
    {
        var scheduler = new AutomationActionScheduler();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scheduler.Start(new AutomationInputAction([(Key)999], [], 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scheduler.Start(new AutomationInputAction([], [], 1, int.MinValue)));
    }

    [Fact]
    public void TimedGestureDoesNotDependOnFrameCount()
    {
        var scheduler = new AutomationActionScheduler();
        scheduler.Start(new AutomationInputAction([Key.W], [], 1, DurationSeconds: 1));
        for (var index = 0; index < 100; index++)
        {
            Assert.True(scheduler.Advance(0.005));
        }

        Assert.True(scheduler.Advance(0.4));
        Assert.False(scheduler.Advance(0.11));
        Assert.Equal("completed", scheduler.Status);
        Assert.Equal(102, scheduler.ElapsedFrames);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public void UnboundedGesturesAreRejected(int frames)
    {
        var scheduler = new AutomationActionScheduler();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scheduler.Start(new AutomationInputAction([], [], frames)));
        Assert.Equal("idle", scheduler.Status);
    }
}

using Engine.Network;

using Game.Network;
using Game.Servers;

namespace Survivalcraft.Test.Network;

public class P2PRoomControllerTest
{
    private static readonly P2PRoomSettings _configuration = new()
    {
        Name = "Room",
        ControlUrl = "http://localhost/",
        Enabled = true
    };

    [Fact]
    public async Task StopCollectsLateSuccessAndCannotResurrectRoom()
    {
        await using var host = new NetworkHost();
        var opening =
            new TaskCompletionSource<NetworkRoomRegistration>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = 0;
        var registration = CreateRegistration(() => closed++);
        var controller = new P2PRoomController((_, _, _, _, _) => opening.Task);
        controller.Bind(host, _configuration, 32);
        controller.Start(_configuration);
        controller.Stop();
        Assert.Equal(P2PRoomState.Stopping, controller.State);
        Assert.Throws<InvalidOperationException>(() => controller.Start(_configuration));
        opening.SetResult(registration);
        await UntilAsync(controller, () => controller.State is P2PRoomState.Disabled);
        Assert.Equal(1, closed);
        Assert.Null(controller.Registration);
        Assert.False(controller.DesiredEnabled);
        Assert.False(controller.AutomaticPending);
        Assert.Equal(0, host.Resources.Connections);
        controller.Stop();
        controller.Update();
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task StoppingDuringRetrySuppressesRetryAndKeepsListener()
    {
        await using var host = new NetworkHost();
        var now = DateTimeOffset.UtcNow;
        var calls = 0;
        var controller = new P2PRoomController(
            (_, _, _, _, _) =>
            {
                calls++;
                return Task.FromException<NetworkRoomRegistration>(new HttpRequestException("Offline"));
            },
            () => now
        );
        controller.Bind(host, _configuration, 8);
        controller.Start(_configuration);
        controller.Update();
        Assert.Equal(P2PRoomState.RetryWaiting, controller.State);
        Assert.Equal("Offline", controller.LastError);
        controller.Stop();
        await UntilAsync(controller, () => controller.State is P2PRoomState.Disabled);
        now += TimeSpan.FromMinutes(1);
        controller.Update();
        Assert.Equal(1, calls);
        Assert.True(host.LocalPort > 0);
    }

    [Fact]
    public async Task ShutdownClosesRegistrationButDoesNotOwnListener()
    {
        await using var host = new NetworkHost();
        var closed = 0;
        var registration = CreateRegistration(() => closed++);
        var controller = new P2PRoomController((_, _, _, _, _) => Task.FromResult(registration));
        controller.Bind(host, _configuration, 8);
        controller.Start(_configuration);
        controller.Update();
        Assert.Equal(P2PRoomState.Active, controller.State);
        await controller.ShutdownAsync();
        Assert.Equal(1, closed);
        Assert.True(host.LocalPort > 0);
    }

    [Fact]
    public async Task FailedUnregisterIsObservableAndDoesNotRestart()
    {
        await using var host = new NetworkHost();
        var registration = CreateRegistration(() => throw new HttpRequestException("Cannot unregister"));
        var controller = new P2PRoomController((_, _, _, _, _) => Task.FromResult(registration));
        controller.Bind(host, _configuration, 8);
        controller.Start(_configuration);
        controller.Update();
        controller.Stop();
        await UntilAsync(controller, () => controller.State is P2PRoomState.Disabled);
        Assert.Equal("Cannot unregister", controller.LastError);
        controller.Stop();
        Assert.Equal("Cannot unregister", controller.LastError);
        Assert.Null(controller.NextRetryAt);
    }

    [Fact]
    public async Task ManualStopConsumesPendingAutomaticStart()
    {
        await using var host = new NetworkHost();
        var controller = new P2PRoomController();
        controller.Bind(host, _configuration, 32);
        Assert.True(controller.AutomaticPending);
        controller.Stop();
        Assert.False(controller.AutomaticPending);
        Assert.Equal(P2PRoomState.Disabled, controller.State);
    }

    [Fact]
    public async Task OldFailureDoesNotScheduleRetryAfterStopAndNextStartWorks()
    {
        await using var host = new NetworkHost();
        var opening =
            new TaskCompletionSource<NetworkRoomRegistration>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = CreateRegistration(() => { });
        var calls = 0;
        var controller =
            new P2PRoomController((_, _, _, _, _) => ++calls == 1 ? opening.Task : Task.FromResult(registration));
        controller.Bind(host, _configuration, 32);
        controller.Start(_configuration);
        controller.Stop();
        opening.SetException(new HttpRequestException("Old failure"));
        await UntilAsync(controller, () => controller.State is P2PRoomState.Disabled);
        Assert.Null(controller.NextRetryAt);
        controller.Start(_configuration);
        controller.Update();
        Assert.Equal(P2PRoomState.Active, controller.State);
        Assert.Equal(2, calls);
        await controller.ShutdownAsync();
    }

    private static async Task UntilAsync(P2PRoomController controller, Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition())
        {
            controller.Update();
            await Task.Delay(10, deadline.Token);
        }
    }

    private static NetworkRoomRegistration CreateRegistration(Action close) => new(
        Guid.NewGuid(),
        "012345",
        DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5),
        CancellationToken.None,
        _ => Task.FromResult(DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5)),
        _ =>
        {
            close();
            return Task.CompletedTask;
        },
        (_, _) => { },
        (_, _) => Task.CompletedTask
    );
}

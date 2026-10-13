namespace Engine.Network.Test;

public sealed class NetworkNotificationQueueTest
{
    [Fact]
    public void LifecycleReserveDoesNotReorderDataAndReleasedBytesCanBeReused()
    {
        var queue = new NetworkNotificationQueue(2, 8, 1);
        var order = new List<int>();
        Assert.True(queue.TryEnqueue(() => order.Add(1), 8));
        Assert.False(queue.TryEnqueue(() => order.Add(99), 1));
        Assert.True(queue.TryEnqueue(() => order.Add(2), control: true));
        Assert.False(queue.TryEnqueue(() => order.Add(99), control: true));
        Assert.True(queue.TryDequeue(out var first));
        first!();
        Assert.True(queue.TryEnqueue(() => order.Add(3), 8));
        while (queue.TryDequeue(out var action))
        {
            action!();
        }
        Assert.Equal(new[] { 1, 2, 3 }, order);
        Assert.Equal(0, queue.Count);
        Assert.True(queue.TryEnqueue(() => { }, 8));
        queue.Clear();
        Assert.True(queue.TryEnqueue(() => { }, 8));
    }
}

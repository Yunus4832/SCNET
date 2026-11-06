using Engine.Network;

namespace Game.Network.Transport;

public static class NetworkEventPump
{
    public static async Task PollAsync(
        NetworkHost network,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<bool> completed,
        Action? retry = null
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var nextRetry = TimeSpan.FromMilliseconds(250);
        while (stopwatch.Elapsed < timeout && !completed())
        {
            cancellationToken.ThrowIfCancellationRequested();
            network.DispatchEvents();
            if (retry is not null && !completed() && stopwatch.Elapsed < timeout && stopwatch.Elapsed >= nextRetry)
            {
                cancellationToken.ThrowIfCancellationRequested();
                retry();
                nextRetry = stopwatch.Elapsed + TimeSpan.FromMilliseconds(250);
            }

            await Task.Delay(2, cancellationToken).ConfigureAwait(false);
        }
    }
}

namespace RendezvousServer.Relay;

internal sealed class RelayCleanupHost(RelayRouteRegistry routes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await routes.CleanExpiredAsync();
        }
    }
}

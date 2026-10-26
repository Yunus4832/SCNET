using Engine.Network;

using Game.Network;
using Game.Network.Handling;
using Game.Servers;
using Game.World;

using Survivalcraft.Test.Network;

namespace Survivalcraft.Test.Servers;

[Collection(PackageDecodingCollection.Name)]
public sealed class ServerDiscoveryServiceTest
{
    [Fact]
    public async Task ProbeReadsServerInfoThroughEngineDatagrams()
    {
        var response = new ServerInfoPackage
        {
            ProtocolVersion = 1,
            ClientCount = 2,
            MaxPlayerCount = 20,
            GameMode = ProtocolEnum.Encode(GameMode.Creative)
        };
        PackageManager.RegisterPackage(() => new ServerInfoPackage(), new ServerInfoPackageHandler());
        await using var server = new NetworkHost();
        using var stop = new CancellationTokenSource();
        server.DatagramReceived += (source, payload) =>
        {
            var request = PackageManager.DecodePackage<ServerInfoPackage>(null, payload);
            Assert.True(request.Package.RequestInfo);
            source.Send(NetNode.CreateUnconnectedPayload(response));
        };
        var polling = Task.Run(async () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    server.DispatchEvents();
                    await Task.Delay(2, stop.Token);
                }
            }
        );
        try
        {
            var status = await new ServerDiscoveryService().ProbeAsync(
                $"127.0.0.1:{server.LocalPort}",
                TimeSpan.FromSeconds(2),
                CancellationToken.None
            );
            Assert.Equal(ServerAvailability.Available, status.Availability);
            Assert.Equal(response.ProtocolVersion, status.ProtocolVersion);
            Assert.Equal(response.ClientCount, status.PlayerCount);
            Assert.Equal(GameMode.Creative, status.GameMode);
        }
        finally
        {
            await stop.CancelAsync();
            try
            {
                await polling;
            }
            catch (OperationCanceledException)
            {
            }

            PackageManager.UnRegisterPackage(response);
        }
    }

    [Fact]
    public async Task PollRetriesLostProbeAndStopsWhenCompleted()
    {
        await using var network = new NetworkHost();
        var attempts = 1;
        await ServerDiscoveryService.PollAsync(
            network,
            TimeSpan.FromSeconds(5),
            CancellationToken.None,
            () => attempts == 3,
            () => attempts++
        );
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task CompletedProbeDoesNotRetry()
    {
        await using var network = new NetworkHost();
        await ServerDiscoveryService.PollAsync(
            network,
            TimeSpan.FromSeconds(5),
            CancellationToken.None,
            () => true,
            () => Assert.Fail("Completed probe retried.")
        );
    }

    [Fact]
    public async Task ShortDeadlineDoesNotRetryAfterTimeout()
    {
        await using var network = new NetworkHost();
        await ServerDiscoveryService.PollAsync(
            network,
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None,
            () => false,
            () => Assert.Fail("Expired probe retried.")
        );
    }

    [Fact]
    public async Task CancellationStopsProbeBeforeRetry()
    {
        await using var network = new NetworkHost();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ServerDiscoveryService.PollAsync(
                network,
                TimeSpan.FromSeconds(5),
                cancellation.Token,
                () => false,
                () => Assert.Fail("Cancelled probe retried.")
            )
        );
    }
}

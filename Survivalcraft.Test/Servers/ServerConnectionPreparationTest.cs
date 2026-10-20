using Engine.Network;

using Game.Network;
using Game.Network.Packages;
using Game.Network.Packages.Handlers;
using Game.Network.Serialization;
using Game.Servers;

using Survivalcraft.Test.Network;

namespace Survivalcraft.Test.Servers;

[Collection(PackageDecodingCollection.Name)]
public sealed class ServerConnectionPreparationTest
{
    [Fact]
    public async Task CancellingPreflightReleasesTheBoundSocket()
    {
        await using var server = new NetworkHost();
        using var http = new HttpClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int? inspectedPort = null;
        server.DatagramReceived += (source, _) =>
        {
            inspectedPort = source.TransportEndpoint.Port;
            cancellation.Cancel();
        };
        var preparing = ServerConnectionPreparation.PrepareAsync(
            new AddressNetworkTarget("127.0.0.1", server.LocalPort), http, cancellation.Token);
        while (!preparing.IsCompleted)
        {
            server.DispatchEvents();
            await Task.Delay(2);
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparing);
        Assert.NotNull(inspectedPort);
        await using var rebound = new NetworkHost(inspectedPort.Value);
        Assert.Equal(inspectedPort, rebound.LocalPort);
        Assert.Null(server.Failure);
    }

    [Fact]
    public async Task ServerInfoAndHandshakeShareSocketAndConsumptionTransfersOwnershipOnce()
    {
        PackageManager.RegisterPackage(() => new ServerInfoPackage(), new ServerInfoPackageHandler());
        await using var server = new NetworkHost();
        using var http = new HttpClient();
        using var stop = new CancellationTokenSource();
        int? inspectedPort = null;
        server.DatagramReceived += (source, payload) =>
        {
            var query = PackageManager.DecodePackage<ServerInfoPackage>(null, payload);
            Assert.True(query.Package.RequestInfo);
            inspectedPort = source.TransportEndpoint.Port;
            source.Send(NetNode.CreateUnconnectedPayload(new ServerInfoPackage
            {
                ProtocolVersion = 1,
                ClientCount = 3
            }));
        };
        server.ConnectionRequested += request =>
        {
            Assert.Equal(inspectedPort, request.TransportEndpoint.Port);
            Assert.Equal("game-handshake"u8.ToArray(), request.Handshake.ToArray());
            request.Accept();
        };
        var polling = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                server.DispatchEvents();
                await Task.Delay(2, stop.Token);
            }
        });
        try
        {
            var target = new AddressNetworkTarget("localhost", server.LocalPort);
            await using var prepared = await ServerConnectionPreparation.PrepareAsync(target, http, CancellationToken.None);
            Assert.Equal(target, prepared.Target);
            Assert.Equal(1, prepared.Status.ProtocolVersion);
            Assert.Equal(3, prepared.Status.PlayerCount);
            var transport = prepared.Consume();
            try
            {
                Assert.Throws<InvalidOperationException>(() => prepared.Consume());
                await prepared.DisposeAsync();
                var connection = await transport.Path.ConnectAsync("game-handshake"u8.ToArray(), CancellationToken.None);
                Assert.True(connection.IsConnected);
                Assert.Null(transport.Host.Failure);
            }
            finally
            {
                await transport.Path.DisposeAsync();
                await transport.Host.DisposeAsync();
            }
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
            PackageManager.UnRegisterPackage(new ServerInfoPackage());
        }
    }
}

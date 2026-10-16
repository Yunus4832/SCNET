using System.Net;

using Engine.Network;

using EntitySystem.TemplatesDatabase;

using Game.Modding;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Packages.Handlers;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public class ConnectionBootstrapPackageTest
{
    [Fact]
    public async Task BlockMappingCrossesMtuAsOneReliableBootstrap()
    {
        var mapping = new BlockIndexMapping(Enumerable.Range(0, 128).Select(index =>
            new KeyValuePair<ResourceId, int>(ResourceId.Parse($"test:block_{Guid.NewGuid():N}"), 300 + index)));
        var package = new BootstrapPackage
        {
            Epoch = Guid.NewGuid(),
            BlockMapping = mapping,
            ClientList = new ClientPackage([]),
            ProjectData = [4, 5, 6]
        };
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        var payload = writer.Data();
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        BootstrapPackage? received = null;
        server.Received += (_, message) =>
        {
            using var reader = new PackageStreamReader(message.Payload.ToArray());
            received = new BootstrapPackage();
            received.ReadData(reader);
        };
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!connecting.IsCompleted)
        {
            server.DispatchEvents();
            client.DispatchEvents();
            await Task.Delay(5, deadline.Token);
        }

        var connection = await connecting;
        Assert.True(payload.Length > connection.GetMaxPayloadSize(DeliveryMode.ReliableOrdered));
        connection.Send(payload, channel: PackageTransportPolicy.Bulk.ChannelNumber,
            delivery: DeliveryMode.ReliableOrdered);
        while (received is null)
        {
            server.DispatchEvents();
            client.DispatchEvents();
            await Task.Delay(5, deadline.Token);
        }

        Assert.Equal(package.Epoch, received.Epoch);
        Assert.Equal(package.ProjectData, received.ProjectData);
        Assert.Equal(mapping.Entries.OrderBy(pair => pair.Value),
            received.BlockMapping.Entries.OrderBy(pair => pair.Value));
    }

    [Fact]
    public void BootstrapOnlyAppliesToTheCurrentUninitializedConnectionAttempt()
    {
        var token = Guid.NewGuid();
        var node = new NetNode { TokenId = token, CurrentStage = NetNode.Stage.Bootstrapping };
        var package = new BootstrapPackage
        {
            Epoch = Guid.NewGuid(),
            ClientList = new ClientPackage([new Client(null, 1, token, Guid.NewGuid(), null)])
        };
        Assert.True(BootstrapPackageHandler.CanApply(package, node));
        node.TokenId = Guid.NewGuid();
        Assert.False(BootstrapPackageHandler.CanApply(package, node));
        node.TokenId = token;
        node.ConnectionEpoch = Guid.NewGuid();
        Assert.False(BootstrapPackageHandler.CanApply(package, node));
        node.ConnectionEpoch = Guid.Empty;
        node.CurrentConnectionPhase = ConnectionPhase.BootstrapSent;
        Assert.False(BootstrapPackageHandler.CanApply(package, node));
        node.CurrentConnectionPhase = ConnectionPhase.TransportConnected;
        node.CurrentStage = NetNode.Stage.NotConnected;
        Assert.False(BootstrapPackageHandler.CanApply(package, node));
    }

    [Fact]
    public void BootstrapRoundTripsAsSinglePayload()
    {
        var epoch = Guid.NewGuid();
        var package = new BootstrapPackage
        {
            Epoch = epoch,
            ClientList = new ClientPackage([]),
            TextureData = [1, 2, 3],
            ProjectData = [4, 5, 6]
        };

        var clone = RoundTrip(package, new BootstrapPackage());

        Assert.Equal(epoch, clone.Epoch);
        Assert.Equal(ClientPackage.EventType.SyncList, clone.ClientList.PackageEventType);
        Assert.Equal(package.TextureData, clone.TextureData);
        Assert.Equal(package.ProjectData, clone.ProjectData);
    }

    [Fact]
    public void ConnectionPhaseAckRoundTripsEpochAndPhase()
    {
        var epoch = Guid.NewGuid();
        var clone = RoundTrip(
            new ConnectionPhaseAckPackage(epoch, ConnectionPhase.BootstrapApplied),
            new ConnectionPhaseAckPackage());

        Assert.Equal(epoch, clone.Epoch);
        Assert.Equal(ConnectionPhase.BootstrapApplied, clone.Phase);
    }

    [Fact]
    public void InitialWorldSnapshotKeepsPlayerDataBeforeEntityPayload()
    {
        var playerGuid = Guid.NewGuid();
        var player = new ValuesDictionary();
        player.SetValue("PlayerGUID", playerGuid);
        var package = new InitialWorldSnapshotPackage
        {
            Epoch = Guid.NewGuid(),
            ClientList = new ClientPackage([]),
            EntityData = [7, 8, 9]
        };
        package.Players.Add(player);

        var clone = RoundTrip(package, new InitialWorldSnapshotPackage());

        Assert.Equal(playerGuid, clone.Players.Single().GetValue<Guid>("PlayerGUID"));
        Assert.Equal(package.EntityData, clone.EntityData);
    }

    private static T RoundTrip<T>(T package, T clone) where T : IPackage
    {
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        clone.ReadData(reader);
        return clone;
    }
}

using System.Net;

using Engine.Network;

using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public class ConnectionBootstrapPackageTest
{
    [Fact]
    public async Task BlockMappingCrossesMtuAsOneReliableBootstrap()
    {
        var mapping = new BlockIndexMapping(
            Enumerable.Range(0, 128).Select(index =>
                new KeyValuePair<ResourceId, int>(ResourceId.Parse($"test:block_{Guid.NewGuid():N}"), 300 + index)
            )
        );
        var package = new BootstrapPackage
        {
            World = new WorldMetadata("Protocol test", 123, ProtocolEnum.Encode(Game.GameMode.Creative), 20),
            Epoch = Guid.NewGuid(),
            BlockMapping = mapping.Entries.Select(pair => new BlockIndexEntry(pair.Key, checked((ushort)pair.Value)))
                .ToArray(),
            ClientList = new ClientPackage([]),
            ProjectData = [4, 5, 6]
        };
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        var payload = writer.Data();
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        BootstrapPackage? received = null;
        server.Received += (_, message) =>
        {
            using var reader = new ProtocolReader(message.Payload.ToArray());
            received = (BootstrapPackage)ProtocolMessageCodec.Read((byte)PackageType.Bootstrap, reader);
        };
        var connecting = client.ConnectAsync(
            new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None
        );
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!connecting.IsCompleted)
        {
            server.DispatchEvents();
            client.DispatchEvents();
            await Task.Delay(5, deadline.Token);
        }

        var connection = await connecting;
        Assert.True(payload.Length > connection.GetMaxPayloadSize(DeliveryMode.ReliableOrdered));
        connection.Send(
            payload,
            channel: PackageTransportPolicy.Bulk.ChannelNumber,
            delivery: DeliveryMode.ReliableOrdered
        );
        while (received is null)
        {
            server.DispatchEvents();
            client.DispatchEvents();
            await Task.Delay(5, deadline.Token);
        }

        Assert.Equal(package.Epoch, received.Epoch);
        Assert.Equal(package.ProjectData, received.ProjectData);
        Assert.Equal(
            mapping.Entries.OrderBy(pair => pair.Value),
            received.BlockMapping.Select(entry => new KeyValuePair<ResourceId, int>(entry.Id, entry.Index))
                .OrderBy(pair => pair.Value)
        );
    }

    [Fact]
    public void BootstrapOnlyAppliesToTheCurrentUninitializedConnectionAttempt()
    {
        var token = Guid.NewGuid();
        var node = new NetNode { TokenId = token, CurrentStage = NetNode.Stage.Bootstrapping };
        var package = new BootstrapPackage
        {
            World = new WorldMetadata("Protocol test", 123, ProtocolEnum.Encode(Game.GameMode.Creative), 20),
            Epoch = Guid.NewGuid(),
            ClientList = ClientPackageBuilder.Snapshot([new Client(null, 1, token, Guid.NewGuid(), null)])
        };
        Assert.False(BootstrapPackageHandler.CanApply(package, node));
        typeof(NetNode).GetProperty(nameof(NetNode.AdmissionAccepted))!.SetValue(node, true);
        Assert.True(BootstrapPackageHandler.CanApply(package, node));
        Assert.False(
            BootstrapPackageHandler.CanApply(
                package with { World = package.World with { Mode = byte.MaxValue } },
                node
            )
        );
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
            World = new WorldMetadata("Protocol test", 123, ProtocolEnum.Encode(Game.GameMode.Creative), 20),
            Epoch = epoch,
            ClientList = new ClientPackage([]),
            TextureData = [1, 2, 3],
            ProjectData = [4, 5, 6]
        };

        var clone = RoundTrip(package);

        Assert.Equal(epoch, clone.Epoch);
        Assert.Equal(ClientPackage.EventType.SyncList, clone.ClientList.PackageEventType);
        Assert.Equal(package.TextureData, clone.TextureData);
        Assert.Equal(package.ProjectData, clone.ProjectData);
    }

    [Fact]
    public void ConnectionPhaseAckRoundTripsEpochAndPhase()
    {
        var epoch = Guid.NewGuid();
        var message = new ConnectionPhaseAckPackage(epoch, ConnectionPhase.BootstrapApplied);
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(message, writer);
        using var reader = new ProtocolReader(writer.Data());
        var clone = (ConnectionPhaseAckPackage)ProtocolMessageCodec.Read(message.ID, reader);

        Assert.Equal(epoch, clone.Epoch);
        Assert.Equal(ConnectionPhase.BootstrapApplied, clone.Phase);
    }

    [Fact]
    public void InitialWorldSnapshotKeepsCharacterDataBeforeEntityPayload()
    {
        var playerGuid = Guid.NewGuid();
        var player = new ValuesDictionary();
        player.SetValue("CharacterId", playerGuid);
        var package = new InitialWorldSnapshotPackage
        {
            Epoch = Guid.NewGuid(),
            ClientList = new ClientPackage([]),
            EntityData = [7, 8, 9],
            Characters = [player.ToMessagePack()]
        };

        var clone = RoundTrip(package);

        var restored = new ValuesDictionary();
        restored.ApplyOverridesUseMessagePack(clone.Characters.Single());
        Assert.Equal(playerGuid, restored.GetValue<Guid>("CharacterId"));
        Assert.Equal(package.EntityData, clone.EntityData);
    }

    private static T RoundTrip<T>(T package) where T : IProtocolMessage
    {
        using var writer = new ProtocolWriter();
        ProtocolMessageCodec.Write(package, writer);
        using var reader = new ProtocolReader(writer.Data());
        return (T)ProtocolMessageCodec.Read(package.ID, reader);
    }
}

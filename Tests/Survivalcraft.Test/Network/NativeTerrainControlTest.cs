using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Network;

public sealed class NativeTerrainControlTest
{
    [Fact]
    public void MalformedFragmentIsRejectedWithoutEscapingNativeNormalization()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var server = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        node.Server = server;
        node.AddClient(server);
        var content = new ClientChunkContentTransport(node.Runtime);
        var allocation = new ChunkAllocationId(new Point2(1, 2), 1);
        content.Request([new ChunkContentRequest(allocation)]);
        var native = new NetworkMessageConnection(node, server, null!);
        var wire = new ReceivedPackage(
            new TerrainFragmentPackage
            {
                ChunkFragment = new EncodedTerrainChunkFragment(allocation, 1, 2, 0, 1, [0])
            },
            node.CreateReceiveContext(server)
        );

        Assert.False(node.TryQueueReceivedNetworkMessage(wire, native));
        Assert.Equal(0, WorldNetworkBinding.For(node.Runtime).Inbox.Count);
        Assert.False(node.CanReceiveFromSession(server, wire.Package));
        node.Runtime.Close();
    }

    [Fact]
    public void InvalidNormalizedRepairIsRejectedWithoutEscapingTheReceiveEvent()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty)
        {
            State = ClientState.ProjectLoaded, ConnectionPhase = ConnectionPhase.Live
        };
        node.AddClient(client);
        var native = new NetworkMessageConnection(node, client, null!);
        var wire = new ReceivedPackage(
            new TerrainFragmentsRequestPackage
            {
                FragmentRequests = [new TerrainChunkFragmentRequest(default, 0, 1, [0])]
            },
            node.CreateReceiveContext(client)
        );

        Assert.False(node.TryQueueReceivedNetworkMessage(wire, native));
        Assert.Equal(0, WorldNetworkBinding.For(node.Runtime).Inbox.Count);
        Assert.False(node.CanReceiveFromSession(client, wire.Package));
        node.Runtime.Close();
    }

    [Fact]
    public void InvalidDecodedMessageIsRejectedWithoutEscapingInboxValidation()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
        node.AddClient(client);
        var received = new ReceivedPackage(new TerrainChunksRequestPackage(), node.CreateReceiveContext(client));

        Assert.False(node.TryQueueReceivedNetworkMessage(received, null));
        Assert.Equal(0, WorldNetworkBinding.For(node.Runtime).Inbox.Count);
        Assert.False(node.CanReceiveFromSession(client, received.Package));
        node.Runtime.Close();
    }

    [Fact]
    public void UncachedRepairsBecomeLogicalRequestsOnlyForTheLiveAuthoritySession()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        node.Runtime.BindProject(project);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty)
        {
            State = ClientState.ProjectLoaded, ConnectionPhase = ConnectionPhase.BootstrapSent
        };
        var native = new NetworkMessageConnection(node, client, null!);
        var allocation = new ChunkAllocationId(new Point2(4, 5), 7);
        var wire = new ReceivedPackage(
            new TerrainFragmentsRequestPackage
            {
                FragmentRequests = [new TerrainChunkFragmentRequest(allocation, 9, 3, [0])]
            },
            node.CreateReceiveContext(client)
        );
        Assert.Null(native.NormalizeIncoming(wire));
        client.ConnectionPhase = ConnectionPhase.Live;
        var logical = native.NormalizeIncoming(wire) ?? throw new InvalidOperationException("Missing logical request.");
        Assert.Equal(wire.Context, logical.Context);
        var request = Assert.Single(Assert.IsType<TerrainChunksRequestPackage>(logical.Package).ChunkRequests);
        Assert.Equal(allocation, request.Allocation);
        Assert.Equal(9, request.KnownContentVersion);
        node.Runtime.Close();
        Assert.Null(native.NormalizeIncoming(wire));
    }
}

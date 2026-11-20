using System.Reflection;

using EntitySystem.Core;

using Game.Modding;
using Game.Network;
using Game.Network.Transport;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class ModNetworkAudienceTest
{
    [Theory]
    [InlineData(NodeRole.Authority)]
    [InlineData(NodeRole.Client)]
    public void ClosingWorldDuringDispatchStopsLaterModHandlers(NodeRole role)
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            role
        );
        var hooks = new ModNetworkHooks();
        var owner = new ModId("test.close-dispatch");
        var network = hooks.ForOwner(owner);
        var laterCalls = 0;
        using var first = network.OnMessage("close", message => message.Runtime.Close());
        using var second = network.OnMessage("close", _ => laterCalls++);
        hooks.Freeze();
        var packet = new ModEnvelopePackage(owner.ToString(), "close", ProtocolPayloadCodec.Encode(1));

        hooks.Dispatch(packet, node.CreateReceiveContext());

        Assert.Equal(0, laterCalls);
    }

    [Fact]
    public void RepliesRemainBoundToTheReceivingRuntimeAndWorld()
    {
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        authority.Runtime.BindProject(authorityProject);
        client.Runtime.BindProject(clientProject);
        var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        var second = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        var hooks = new ModNetworkHooks();
        var owner = new ModId("test.runtime");
        var received = new List<ModNetworkMessageContext>();
        using var registration = hooks.ForOwner(owner).OnMessage(
            "request",
            message =>
            {
                received.Add(message);
                message.Reply(17);
            }
        );
        hooks.Freeze();
        var packet = new ModEnvelopePackage(owner.ToString(), "request", ProtocolPayloadCodec.Encode(1));

        hooks.Dispatch(packet, authority.CreateReceiveContext(first));
        hooks.Dispatch(packet, client.CreateReceiveContext(second));

        Assert.Same(authorityProject, received[0].Project);
        Assert.Same(clientProject, received[1].Project);
        Assert.True(received[0].IsServer);
        Assert.False(received[1].IsServer);
        Assert.Equal(1, authority.PendingPackageCount);
        Assert.Equal(1, client.PendingPackageCount);
    }

    [Fact]
    public void ClosingOneModMessageRuntimeDoesNotCloseTheOtherPublishPath()
    {
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var hooks = new ModNetworkHooks();
        var network = hooks.ForOwner(new ModId("test.lifetime"));

        authority.Runtime.Close();
        network.Send(authority.Runtime, "update", 1, PackageAudience.Global);
        network.Send(client.Runtime, "update", 2, PackageAudience.Global);

        Assert.Equal(0, authority.PendingPackageCount);
        Assert.Equal(1, client.PendingPackageCount);
    }

    [Fact]
    public void SendsPreserveExplicitObserversAndReplyUsesOriginalConnection()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var pending = (PendingLogicalMessages)typeof(NetNode)
            .GetField("_pendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node)!;
        pending.Clear();
        try
        {
            var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
            var replacement = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
            var observers = new List<Client> { first };
            var audience = PackageAudience.To(observers);
            observers.Clear();
            var hooks = new ModNetworkHooks();
            var owner = new ModId("test.audience");
            var network = hooks.ForOwner(owner);
            var source = new byte[] { 1, 2 };
            network.Send(node.Runtime, "state", source, audience, ClientState.Playing);
            source[0] = 99;
            network.Send(node.Runtime, "event", 7, PackageAudience.Global);
            var context = new ModNetworkMessageContext(
                hooks,
                owner,
                "reply",
                ReadOnlyMemory<byte>.Empty,
                node.CreateReceiveContext(first)
            );
            context.Reply(8);
            context.Send("empty", 9, PackageAudience.To(Array.Empty<Client>()));

            Assert.Equal(4, pending.Count);
            Assert.True(pending[0].Audience.Includes(first));
            Assert.False(pending[0].Audience.Includes(replacement));
            var package = Assert.IsType<ModEnvelopePackage>(pending[0].Package);
            Assert.Equal(new byte[] { 1, 2 }, package.Payload);
            Assert.Equal(ClientState.Playing, package.RequiredState);
            Assert.True(pending[1].Audience.Includes(replacement));
            Assert.True(pending[2].Audience.Includes(first));
            Assert.False(pending[2].Audience.Includes(replacement));
            Assert.False(pending[3].Audience.Includes(first));
            Assert.Throws<ArgumentNullException>(() => network.Send(node.Runtime, "invalid", Array.Empty<byte>(), null!)
            );
            Assert.Equal(4, pending.Count);
        }
        finally
        {
            pending.Clear();
        }
    }
}

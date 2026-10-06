using System.Reflection;

using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class ModNetworkAudienceTest
{
    [Fact]
    public void SendsPreserveExplicitObserversAndReplyUsesOriginalConnection()
    {
        var node = CommonLib.Net;
        var pending = (List<OutboundPackage>)typeof(NetNode)
            .GetField("_pendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node)!;
        var original = pending.ToArray();
        pending.Clear();
        try
        {
            var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
            var replacement = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
            var observers = new List<Client> { first };
            var audience = PackageAudience.To(observers);
            observers.Clear();
            var hooks = new ModNetworkHooks();
            var owner = new ModId("test.audience");
            var network = hooks.ForOwner(owner);
            network.Send("state", new byte[] { 1, 2 }, audience, ClientState.Playing);
            network.Send("event", writer => writer.Write(7), PackageAudience.Global);
            var context = new ModNetworkMessageContext(hooks, owner, "reply", null!, first, node, true);
            context.Reply(writer => writer.Write(8));
            context.Send("empty", writer => writer.Write(9), PackageAudience.To(Array.Empty<Client>()));

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
            Assert.Throws<ArgumentNullException>(() => network.Send("invalid", Array.Empty<byte>(), null!));
            Assert.Equal(4, pending.Count);
        }
        finally
        {
            pending.Clear();
            pending.AddRange(original);
        }
    }
}

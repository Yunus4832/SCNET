using System.Net;

using Game.Network;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class OutboundPackageIdentityTest
{
    [Fact]
    public void EqualMessagesRemainDifferentWorkButRepeatedRecipientWorkDeduplicates()
    {
        var first = new ConnectionRejectPackage("same-value");
        var second = new ConnectionRejectPackage("same-value");
        Assert.Equal(first, second);
        var client = new Client(new IPEndPoint(IPAddress.Loopback, 30000));
        var work = new HashSet<OutboundPackage>(OutboundPackageIdentityComparer.Instance)
        {
            new(first, PackageAudience.To(client)),
            new(second, PackageAudience.To(client)),
            new(first, PackageAudience.To(client))
        };
        Assert.Equal(2, work.Count);
    }
}

using System.Net;

using ContentServer.Application;

namespace ContentServer.Test;

public sealed class ServerSourceInspectionServiceTest
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.1.1")]
    [InlineData("100.64.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::1")]
    public void RejectsNonPublicAddresses(string value)
    {
        Assert.False(ServerSourceInspectionService.IsPublicAddress(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public void AcceptsPublicAddresses(string value)
    {
        Assert.True(ServerSourceInspectionService.IsPublicAddress(IPAddress.Parse(value)));
    }
}

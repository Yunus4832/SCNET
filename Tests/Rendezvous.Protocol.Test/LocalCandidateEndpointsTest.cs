using System.Net.Sockets;

namespace Rendezvous.Protocol.Test;

public sealed class LocalCandidateEndpointsTest
{
    [Theory]
    [InlineData("127.0.0.1:4000")]
    [InlineData("8.8.8.8:4000")]
    [InlineData("224.0.0.1:4000")]
    [InlineData("192.168.1.2:0")]
    [InlineData("[fe80::1]:4000")]
    [InlineData("[fd00::1%2]:4000")]
    [InlineData("invalid")]
    public void RejectsUnusableOrPublicCandidates(string endpoint)
    {
        Assert.False(LocalCandidateEndpoints.TryParse([endpoint], null, out _));
    }

    [Fact]
    public void IPv4RegistrationCanCarryGlobalIPv6Candidates()
    {
        Assert.True(
            LocalCandidateEndpoints.TryParse(
                ["192.168.1.2:4000", "[240e:1234::2]:4000"],
                null,
                out var endpoints
            )
        );
        Assert.Equal(2, endpoints.Length);
        Assert.True(LocalCandidateEndpoints.IsGlobalIPv6Address(endpoints[1].Address));
        Assert.False(LocalCandidateEndpoints.TryParse(["[ff02::1]:4000"], null, out _));
        Assert.False(LocalCandidateEndpoints.TryParse(["[::ffff:192.168.1.2]:4000"], null, out _));
    }

    [Fact]
    public void BoundsCountRejectsDuplicatesAndEnforcesFamily()
    {
        Assert.False(
            LocalCandidateEndpoints.TryParse(
                Enumerable.Range(1, 5).Select(index => $"10.0.0.{index}:4000").ToArray(),
                null,
                out _
            )
        );
        Assert.False(LocalCandidateEndpoints.TryParse(["10.0.0.1:4000", "10.0.0.1:4000"], null, out _));
        Assert.False(LocalCandidateEndpoints.TryParse(["[fd00::1]:4000"], AddressFamily.InterNetwork, out _));
        Assert.True(LocalCandidateEndpoints.TryParse(["[fd00::1]:4000"], AddressFamily.InterNetworkV6, out _));
    }

    [Fact]
    public void DecodedCandidatesHaveIndependentSnapshot()
    {
        string[] candidates = ["192.168.1.2:4000"];
        Assert.True(LocalCandidateEndpoints.TryParse(candidates, AddressFamily.InterNetwork, out var endpoints));
        candidates[0] = "192.168.1.3:4000";
        Assert.Equal("192.168.1.2:4000", Assert.Single(endpoints).ToString());
        var message = new SignalMessage("candidate", Guid.NewGuid(), LocalEndpoints: candidates);
        Assert.True(SignalCodec.TryDecode(SignalCodec.Encode(message), out var decoded));
        Assert.Equal(candidates, decoded!.LocalEndpoints);
    }
}

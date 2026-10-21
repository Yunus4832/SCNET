using System.Net;

using Rendezvous.Protocol;

namespace Engine.Network.Test;

public sealed class CandidateProbeTest
{
    [Fact]
    public void OnlyAuthorizedSourcesAndMatchingIdentityCanConfirm()
    {
        var observed = new IPEndPoint(IPAddress.Parse("203.0.113.1"), 4000);
        var local = new IPEndPoint(IPAddress.Parse("240e:1234::2"), 4000);
        var candidate = new SignalMessage(
            "candidate",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Challenge: new string('A', 64)
        );
        var probe = new CandidateProbe(candidate, [observed, local]);
        var acknowledgement = candidate with { Operation = "punch-ack" };
        Assert.True(probe.Matches(acknowledgement, observed));
        Assert.True(probe.Matches(acknowledgement, local));
        Assert.False(probe.Matches(acknowledgement, new IPEndPoint(local.Address, 4001)));
        Assert.False(probe.Matches(acknowledgement with { Challenge = new string('B', 64) }, local));
        Assert.False(probe.Matches(acknowledgement with { ParticipantId = Guid.NewGuid() }, local));
        Assert.False(probe.Matches(acknowledgement with { RequestId = Guid.NewGuid() }, local));
        Assert.False(probe.Matches(acknowledgement with { SessionId = Guid.NewGuid() }, local));
        probe.ConfirmReachability(local);
        Assert.Equal(local, probe.Completion.Task.Result);
        Assert.Equal(local, Assert.Single(probe.SendEndpoints));
        Assert.False(probe.Matches(acknowledgement, observed));
        probe.ConfirmReachability(observed);
        Assert.Equal(local, probe.Completion.Task.Result);
    }
}

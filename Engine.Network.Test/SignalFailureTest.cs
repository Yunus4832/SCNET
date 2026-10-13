using System.Net;
using System.Net.Sockets;

using LiteNetLib;

using Rendezvous.Protocol;

namespace Engine.Network.Test;

public sealed class SignalFailureTest
{
    [Theory]
    [InlineData("register", RendezvousFailureKind.UdpUnavailable)]
    [InlineData("relay-probe", RendezvousFailureKind.RelayUnavailable)]
    public async Task MissingUdpResponseIdentifiesTheFailedPath(string operation, RendezvousFailureKind kind)
    {
        using var silent = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var host = new NetworkHost();
        var failure = await Assert.ThrowsAsync<RendezvousException>(() => host.RequestSignalAsync(
            (IPEndPoint)silent.Client.LocalEndPoint!, new SignalMessage(operation, Guid.NewGuid()),
            CancellationToken.None, TimeSpan.FromMilliseconds(100)));
        Assert.Equal(kind, failure.Kind);
        Assert.Equal(operation, failure.Operation);
        Assert.Equal("udp_request_timeout", failure.ErrorCode);
    }

    [Fact]
    public async Task CallerCancellationIsNotReportedAsUdpFailure()
    {
        using var silent = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var host = new NetworkHost();
        using var cancellation = new CancellationTokenSource();
        var request = host.RequestSignalAsync((IPEndPoint)silent.Client.LocalEndPoint!,
            new SignalMessage("register", Guid.NewGuid()), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCandidateAfterRequestEndsCannotCreateAPathOrCompleteTheNextRequest(bool timeout)
    {
        var listener = new EventBasedNetListener();
        var beacon = new NetManager(listener) { UnconnectedMessagesEnabled = true };
        Assert.True(beacon.Start(IPAddress.Loopback, IPAddress.IPv6Loopback, 0));
        await using var host = new NetworkHost();
        var endpoint = new IPEndPoint(IPAddress.Loopback, beacon.LocalPort);
        var session = Guid.NewGuid();
        var participant = Guid.NewGuid();
        SignalMessage? cancelled = null;
        var sendLateCandidate = false;
        listener.NetworkReceiveUnconnectedEvent += (source, reader, _) =>
        {
            try
            {
                Assert.True(SignalCodec.TryDecode(reader.GetRemainingBytes(), out var request));
                if (request!.Operation == "exchange")
                {
                    cancelled = request;
                    return;
                }
                if (sendLateCandidate)
                {
                    beacon.SendUnconnectedMessage(SignalCodec.Encode(cancelled! with
                    {
                        Operation = "candidate",
                        Endpoint = "127.0.0.1:30000",
                        Challenge = new string('a', 64)
                    }), source);
                }
                beacon.SendUnconnectedMessage(SignalCodec.Encode(request with
                {
                    Operation = request.Operation == "confirm" ? "registered" : "challenge",
                    Challenge = "fixture"
                }), source);
            }
            finally
            {
                reader.Recycle();
            }
        };
        try
        {
            var registering = host.RegisterAsync(endpoint, session, participant, "fixture", CancellationToken.None);
            await PollUntilAsync(beacon, () => registering.IsCompleted);
            await registering;
            using var cancellation = new CancellationTokenSource();
            var pending = host.RequestSignalAsync(endpoint,
                new SignalMessage("exchange", Guid.NewGuid(), session, participant), cancellation.Token,
                TimeSpan.FromMilliseconds(150));
            await PollUntilAsync(beacon, () => cancelled is not null);
            if (timeout)
            {
                var failure = await Assert.ThrowsAsync<RendezvousException>(() => pending);
                Assert.Equal(RendezvousFailureKind.UdpUnavailable, failure.Kind);
            }
            else
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            Assert.Equal(0, host.Resources.PendingSignals);
            sendLateCandidate = true;
            var nextId = Guid.NewGuid();
            var next = host.RequestSignalAsync(endpoint, new SignalMessage("register", nextId), CancellationToken.None);
            await PollUntilAsync(beacon, () => next.IsCompleted);
            Assert.Equal(nextId, (await next).RequestId);
            Assert.Equal(0, host.Resources.CandidatePaths);
            Assert.Equal(0, host.Resources.PendingSignals);
        }
        finally
        {
            beacon.Stop();
        }
    }

    private static async Task PollUntilAsync(NetManager beacon, Func<bool> completed)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!completed())
        {
            deadline.Token.ThrowIfCancellationRequested();
            beacon.PollEvents();
            await Task.Delay(2, deadline.Token);
        }
    }

}

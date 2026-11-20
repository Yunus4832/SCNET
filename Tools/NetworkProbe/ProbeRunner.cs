using System.Net;

using Engine.Network;
using Engine.Network.Rendezvous;

namespace NetworkProbe;

internal sealed class ProbeRunner(Uri control, IPEndPoint? beacon, ProbeBinding binding, ProbeReport report)
{
    public async Task RunHostAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        var signaling = new RendezvousClient(http, control);
        await using var session = new ProbeSession(binding, report);
        var host = session.Host;
        report.Record("bound", new { host.LocalPort });
        await using var invitation = beacon is null
            ? await host.RegisterRoomAsync(control, http, 8, cancellationToken)
            : await host.RegisterRoomAsync(signaling, [beacon], 8, cancellationToken);
        await invitation.RenewAsync(cancellationToken);
        report.Record("invitation-ready");
        host.DatagramReceived += (source, payload) => source.Send(payload);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.ConnectionRequested += request =>
        {
            if (request.Handshake.Span.SequenceEqual("probe-handshake"u8))
            {
                var connection = request.Accept();
                report.Record("connected", new { Path = connection?.PathKind.ToString() });
            }
            else
            {
                request.Reject();
            }
        };
        host.Received += (connection, payload) =>
        {
            if (payload.Payload.Span.SequenceEqual("probe-complete"u8))
            {
                completed.TrySetResult();
                report.Record("completion-received");
            }
            else
            {
                session.Send(connection, payload.Payload.Span);
            }
        };
        Console.WriteLine(
            $"READY joinCode={invitation.JoinCode} localPort={host.LocalPort} sessionId={invitation.SessionId}"
        );
        await DispatchUntilAsync(host, completed.Task, cancellationToken);
        await completed.Task;
        report.Record("host-complete");
        Console.WriteLine("PASS host received completion after reliable echo");
    }

    public async Task RunClientAsync(
        string joinCode,
        TimeSpan preparationDelay,
        RelayPolicy relayPolicy,
        CancellationToken cancellationToken,
        bool verifyRecovery = false
    )
    {
        using var http = new HttpClient();
        var signaling = new RendezvousClient(http, control);
        await using var session = new ProbeSession(binding, report);
        var host = session.Host;
        report.Record("bound", new { host.LocalPort });
        NetworkTarget target = Guid.TryParse(joinCode, out var sessionId)
            ? new InvitationSessionNetworkTarget(control, sessionId, relayPolicy)
            : new InvitationCodeNetworkTarget(control, joinCode, relayPolicy);
        report.Record("preparing-path", new { Policy = relayPolicy.ToString() });
        await using var preparation = beacon is null
            ? await host.PrepareConnectionAsync(target, http, cancellationToken)
            : await host.PrepareInvitationAsync(signaling, [beacon], joinCode, relayPolicy, cancellationToken);
        if (beacon is null && preparation.Target is not InvitationSessionNetworkTarget)
        {
            throw new InvalidOperationException("Preparation did not retain a restorable session locator.");
        }

        Console.WriteLine($"PATH challengeConfirmed localPort={host.LocalPort} path={preparation.PathKind}");
        report.Record("path-confirmed", new { Path = preparation.PathKind.ToString(), host.LocalPort });
        using (var holding = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var renewal = RenewPreparationAsync(preparation, holding.Token);
            try
            {
                await Task.Delay(preparationDelay, cancellationToken);
            }
            finally
            {
                await holding.CancelAsync();
                await renewal;
            }
        }

        if (preparation.PathKind == NetworkPathKind.HolePunch && host.PendingPathCount != 1)
        {
            throw new InvalidOperationException("Prepared path was not retained.");
        }

        var probed = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        preparation.DatagramReceived += payload => probed.TrySetResult(payload);
        var probeDeadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (!probed.Task.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow >= probeDeadline)
            {
                throw new TimeoutException("The prepared path did not answer the server-info probe.");
            }

            preparation.SendDatagram("probe-server-info"u8);
            host.DispatchEvents();
            await Task.Delay(250, cancellationToken);
        }

        if (!(await probed.Task).AsSpan().SequenceEqual("probe-server-info"u8))
        {
            throw new InvalidDataException("Preparation datagram mismatch.");
        }

        var echoed = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        report.Record("datagram-echo-confirmed");
        host.Received += (_, payload) => echoed.TrySetResult(payload.Payload.ToArray());
        var connection = await preparation.ConnectAsync("probe-handshake"u8.ToArray(), cancellationToken);
        report.Record("connected", new { Path = connection.PathKind.ToString() });
        if (host.PendingPathCount != 0)
        {
            throw new InvalidOperationException("Prepared path was not released after connection transfer.");
        }

        session.Send(connection, "probe-roundtrip"u8);
        await DispatchUntilAsync(host, echoed.Task, cancellationToken);
        if (!(await echoed.Task).AsSpan().SequenceEqual("probe-roundtrip"u8))
        {
            throw new InvalidDataException("Transport echo mismatch.");
        }

        if (verifyRecovery)
        {
            await VerifyRecoveryAsync(session, connection, cancellationToken);
        }

        report.Record("reliable-echo-confirmed", new { connection.PendingReliableBytes });
        session.Send(connection, "probe-complete"u8);
        // Keep the transport alive until the host has received the final reliable message and disconnected.
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Disconnected += (_, _) => disconnected.TrySetResult();
        await DispatchUntilAsync(host, disconnected.Task, cancellationToken);
        report.Record("client-complete");
        Console.WriteLine("PASS client candidate challenge and reliable echo over its original socket");
    }

    private static async Task VerifyRecoveryAsync(
        ProbeSession session,
        NetworkConnection connection,
        CancellationToken cancellationToken
    )
    {
        var host = session.Host;
        const int count = 80;
        var received = 0;
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Receive(NetworkConnection source, NetworkMessage message)
        {
            if (source.Id != connection.Id)
            {
                return;
            }

            var expected = Enumerable.Repeat((byte)received, 1024).ToArray();
            if (received >= count || !message.Payload.Span.SequenceEqual(expected))
            {
                complete.TrySetException(
                    new InvalidDataException("Recovery echo is missing, duplicated, reordered or corrupted.")
                );
                return;
            }

            if (++received == count)
            {
                complete.TrySetResult();
            }
        }

        host.Received += Receive;
        try
        {
            Console.WriteLine($"STREAM start utc={DateTimeOffset.UtcNow:O} count={count} bytes=1024 intervalMs=100");
            var receipts = new List<Task>();
            for (var index = 0; index < count; index++)
            {
                receipts.Add(
                    session.SendWithReceipt(connection, Enumerable.Repeat((byte)index, 1024).ToArray())
                        .Completion
                );
                host.DispatchEvents();
                await Task.Delay(100, cancellationToken);
            }

            await DispatchUntilAsync(host, complete.Task, cancellationToken);
            await complete.Task;
            await Task.WhenAll(receipts).WaitAsync(cancellationToken);
            if (connection.PendingReliableBytes != 0 || host.Failure is not null)
            {
                throw new InvalidOperationException("Reliable queue did not converge after recovery.");
            }

            Console.WriteLine($"PASS recovery echoed={received} pendingReliableBytes=0 utc={DateTimeOffset.UtcNow:O}");
        }
        finally
        {
            host.Received -= Receive;
        }
    }

    private static async Task RenewPreparationAsync(
        ConnectionPreparation preparation,
        CancellationToken cancellationToken
    )
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
                await preparation.ExtendLeaseAsync(TimeSpan.FromMinutes(2), cancellationToken);
                Console.WriteLine("LEASE renewed prepared path");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task DispatchUntilAsync(NetworkHost host, Task completion, CancellationToken cancellationToken)
    {
        while (!completion.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            host.DispatchEvents();
            await Task.Delay(5, cancellationToken);
        }
    }
}

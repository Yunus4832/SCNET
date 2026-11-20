using System.Collections.Concurrent;
using System.Diagnostics;

using Engine.Network;

namespace NetworkProbe;

internal sealed class ProbeSession : IAsyncDisposable
{
    private readonly ProbeReport _report;

    private readonly ConcurrentDictionary<Guid, NetworkConnection> _connections = new();

    private readonly CancellationTokenSource _stop = new();

    private readonly Task _sampling;

    private long _acceptedPayloadBytes;

    private long _receivedPayloadBytes;

    public NetworkHost Host { get; }

    public ProbeSession(ProbeBinding binding, ProbeReport report)
    {
        _report = report;
        Host = binding.CreateHost();
        Host.Connected += connection => _connections[connection.Id] = connection;
        Host.Received += (_, message) => Interlocked.Add(ref _receivedPayloadBytes, message.Payload.Length);
        _sampling = report.DirectoryPath is null ? Task.CompletedTask : SampleAsync();
    }

    public void Send(NetworkConnection connection, ReadOnlySpan<byte> payload)
    {
        connection.Send(payload);
        Interlocked.Add(ref _acceptedPayloadBytes, payload.Length);
    }

    public DeliveryReceipt SendWithReceipt(NetworkConnection connection, byte[] payload)
    {
        var receipt = connection.SendWithReceipt(payload);
        Interlocked.Add(ref _acceptedPayloadBytes, payload.Length);
        return receipt;
    }

    private async Task SampleAsync()
    {
        using var process = Process.GetCurrentProcess();
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                process.Refresh();
                WriteSample(process);
                await Task.Delay(250, _stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
    }

    private void WriteSample(Process process, string? phase = null)
    {
        _report.Sample(
            new
            {
                Host.Resources,
                HostStopped = Host.Completion.IsCompleted,
                ProcessCpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds,
                process.WorkingSet64,
                AcceptedPayloadBytes = Interlocked.Read(ref _acceptedPayloadBytes),
                ReceivedPayloadBytes = Interlocked.Read(ref _receivedPayloadBytes),
                Connections = _connections.Values.Select(connection => new
                    {
                        connection.Id,
                        Path = connection.PathKind.ToString(),
                        AddressFamily = connection.TransportEndpoint.AddressFamily.ToString(),
                        connection.IsConnected,
                        connection.RoundTripMilliseconds,
                        connection.PacketLossPercent,
                        connection.PendingReliableBytes
                    }
                ).ToArray()
            },
            phase
        );
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Host.DisposeAsync();
        }
        finally
        {
            await _stop.CancelAsync();
            try
            {
                await _sampling;
            }
            finally
            {
                _stop.Dispose();
            }
        }

        _report.Record("host-stopped", new { Host.Resources }, advancePhase: false);
        if (_report.DirectoryPath is not null)
        {
            using var process = Process.GetCurrentProcess();
            WriteSample(process, "host-stopped");
        }

        if (Host.Resources != default || !Host.Completion.IsCompleted)
        {
            throw new InvalidOperationException("Probe host resources did not converge after close.");
        }
    }
}

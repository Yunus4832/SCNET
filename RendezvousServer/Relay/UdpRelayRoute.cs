using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging.Abstractions;

using Rendezvous.Protocol;

namespace RendezvousServer.Relay;

/// <summary>Forwards raw datagrams between two authenticated sides using one dedicated UDP endpoint.</summary>
public sealed class UdpRelayRoute : IAsyncDisposable
{
    private readonly UdpClient _socket;
    private readonly CancellationTokenSource _stop = new();
    private readonly byte[][] _keys = [RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32)];
    private readonly ReplayWindow[] _replay = [new(), new()];
    private readonly IPEndPoint?[] _endpoints = new IPEndPoint?[2];
    private readonly ulong[] _outboundSequences = new ulong[2];
    private readonly Task _worker;
    private int _disposed;
    private long _forwardedBytes;
    private readonly TimeProvider _clock;
    private readonly int _bytesPerSecond;
    private long _lastActivityTicks;
    private readonly ILogger<UdpRelayRoute> _logger;

    public UdpRelayRoute(IPEndPoint bindEndpoint, TimeProvider? clock = null, int bytesPerSecond = 1024 * 1024,
        ILogger<UdpRelayRoute>? logger = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytesPerSecond, 1);
        _bytesPerSecond = bytesPerSecond;
        _clock = clock ?? TimeProvider.System;
        _logger = logger ?? NullLogger<UdpRelayRoute>.Instance;
        _lastActivityTicks = _clock.GetUtcNow().UtcTicks;
        _socket = new UdpClient(bindEndpoint);
        Endpoint = (IPEndPoint)_socket.Client.LocalEndPoint!;
        _worker = Task.Run(ForwardAsync);
    }

    public Guid Id { get; } = Guid.NewGuid();

    public IPEndPoint Endpoint { get; }

    public Task Completion => _worker;

    public DateTimeOffset LastActivity => new(Interlocked.Read(ref _lastActivityTicks), TimeSpan.Zero);

    public long ForwardedBytes => Interlocked.Read(ref _forwardedBytes);

    public RelayRouteCredentials GetCredentials(byte side)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(side, (byte)1);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return new RelayRouteCredentials(Id, side, _keys[side].ToArray());
    }

    private async Task ForwardAsync()
    {
        var traffic = 0;
        try
        {
            var resetAt = _clock.GetUtcNow() + TimeSpan.FromSeconds(1);
            while (!_stop.IsCancellationRequested)
            {
                var received = await _socket.ReceiveAsync(_stop.Token);
                if (_clock.GetUtcNow() >= resetAt)
                {
                    traffic = 0;
                    resetAt = _clock.GetUtcNow() + TimeSpan.FromSeconds(1);
                }
                if (received.Buffer.Length > RelayDatagramCodec.Overhead + RelayDatagramCodec.MaxPayloadSize ||
                    received.Buffer.Length > _bytesPerSecond - traffic)
                {
                    continue;
                }
                traffic += received.Buffer.Length;
                if (!RelayDatagramCodec.TryReadRoute(received.Buffer, out var routeId, out var side) || routeId != Id ||
                    !RelayDatagramCodec.TryDecode(received.Buffer, _keys[side], out var packet) || packet is null ||
                    !_replay[side].TryAccept(packet.Sequence))
                {
                    continue;
                }
                // Lock the first authenticated source. Rebinding requires a new route negotiation.
                _endpoints[side] ??= received.RemoteEndPoint;
                if (!_endpoints[side]!.Equals(received.RemoteEndPoint))
                {
                    continue;
                }
                if (packet.Payload.Length > 1 && SignalCodec.TryDecode(packet.Payload.AsSpan(1), out var control) &&
                    control?.Operation == "relay-close")
                {
                    break;
                }
                Interlocked.Exchange(ref _lastActivityTicks, _clock.GetUtcNow().UtcTicks);
                var destinationSide = (byte)(1 - side);
                var destination = _endpoints[destinationSide];
                if (destination is null)
                {
                    continue;
                }
                var forwarded = RelayDatagramCodec.Encode(Id, destinationSide, ++_outboundSequences[destinationSide],
                    _keys[destinationSide], packet.Payload);
                await _socket.SendAsync(forwarded, destination, _stop.Token);
                Interlocked.Add(ref _forwardedBytes, forwarded.Length);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (SocketException exception)
        {
            _logger.LogWarning("Relay route {RouteId} at {Endpoint} stopped: {SocketError}", Id, Endpoint,
                exception.SocketErrorCode);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Relay route {RouteId} at {Endpoint} failed", Id, Endpoint);
        }
        finally
        {
            _socket.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            await _worker;
            return;
        }
        await _stop.CancelAsync();
        try
        {
            await _worker;
        }
        finally
        {
            _socket.Dispose();
            _stop.Dispose();
            foreach (var key in _keys)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}

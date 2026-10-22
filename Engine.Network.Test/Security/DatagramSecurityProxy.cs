using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

using Rendezvous.Protocol;

namespace Engine.Network.Test.Security;

internal sealed class DatagramSecurityProxy : IAsyncDisposable
{
    private readonly UdpClient _front = new(new IPEndPoint(IPAddress.Loopback, 0));

    private readonly UdpClient _back = new(new IPEndPoint(IPAddress.Loopback, 0));

    private readonly CancellationTokenSource _stop = new();

    private readonly IPEndPoint _server;

    private readonly Task _forward;

    private readonly Task _reverse;

    private IPEndPoint? _client;

    private readonly bool _relay;

    private readonly Guid _route = Guid.NewGuid();

    private readonly byte[] _clientKey = RandomNumberGenerator.GetBytes(32);

    private readonly byte[] _serverKey = RandomNumberGenerator.GetBytes(32);

    private readonly Random _forwardRandom = new(1949);

    private readonly Random _reverseRandom = new(2949);

    private ulong _serverSequence;

    private ulong _clientSequence;

    private volatile byte[]? _held;

    internal bool HasHeldRecord => _held is not null;

    internal int reorderNext;

    internal int tamperNext;

    internal int dropPercent;

    internal int duplicatePercent;

    internal DatagramSecurityProxy(IPEndPoint server, bool relay = false)
    {
        _server = server;
        _relay = relay;
        _forward = ForwardAsync();
        _reverse = ReverseAsync();
    }

    internal IPEndPoint Endpoint => (IPEndPoint)_front.Client.LocalEndPoint!;

    internal IPEndPoint BackEndpoint => (IPEndPoint)_back.Client.LocalEndPoint!;

    internal ConcurrentQueue<byte[]> ClientPackets { get; } = new();

    internal ConcurrentQueue<byte[]> ServerPackets { get; } = new();

    internal long ClientBytes => ClientPackets.Sum(p => (long)p.Length);

    internal long ServerBytes => ServerPackets.Sum(p => (long)p.Length);

    internal void InstallRoutes(NetworkHost client, NetworkHost server)
    {
        client.RegisterRelayRoute(Endpoint, new RelayRouteCredentials(_route, 0, _clientKey));
        server.RegisterRelayRoute(BackEndpoint, new RelayRouteCredentials(_route, 1, _serverKey));
    }

    internal Task ReplayAsync(byte[] packet) => _back.SendAsync(packet, _server).AsTask();

    private async Task ForwardAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var received = await _front.ReceiveAsync(_stop.Token);
                _client = received.RemoteEndPoint;
                var packet = received.Buffer;
                ClientPackets.Enqueue(packet.ToArray());
                if (dropPercent > 0 && _forwardRandom.Next(100) < dropPercent) { continue; }

                if (_relay)
                {
                    if (!RelayDatagramCodec.TryDecode(packet, _clientKey, out var decoded)) { continue; }

                    packet = RelayDatagramCodec.Encode(_route, 1, ++_serverSequence, _serverKey, decoded!.Payload);
                }

                if (packet.Length > 50 &&
                    packet[0] == 8 &&
                    packet[5] == 23 &&
                    Interlocked.CompareExchange(ref tamperNext, 0, 1) == 1)
                {
                    packet[^1] ^= 1;
                }

                if (_held is null &&
                    packet.Length >= 400 &&
                    packet[0] == 8 &&
                    packet[5] == 23 &&
                    Interlocked.CompareExchange(ref reorderNext, 0, 1) == 1)
                {
                    _held = packet;
                    continue;
                }

                await _back.SendAsync(packet, _server, _stop.Token);
                if (_held is { } delayed)
                {
                    _held = null;
                    await _back.SendAsync(delayed, _server, _stop.Token);
                }

                if (duplicatePercent > 0 && _forwardRandom.Next(100) < duplicatePercent)
                {
                    await _back.SendAsync(packet, _server, _stop.Token);
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    private async Task ReverseAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var received = await _back.ReceiveAsync(_stop.Token);
                var packet = received.Buffer;
                ServerPackets.Enqueue(packet.ToArray());
                if (_client is null || dropPercent > 0 && _reverseRandom.Next(100) < dropPercent) { continue; }

                if (_relay)
                {
                    if (!RelayDatagramCodec.TryDecode(packet, _serverKey, out var decoded)) { continue; }

                    packet = RelayDatagramCodec.Encode(_route, 0, ++_clientSequence, _clientKey, decoded!.Payload);
                }

                await _front.SendAsync(packet, _client, _stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await Task.WhenAll(_forward, _reverse);
        _front.Dispose();
        _back.Dispose();
        _stop.Dispose();
    }
}

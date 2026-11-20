using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace NetworkDamageTool;

public sealed class DamageProxy(DamageProxyOptions options) : IAsyncDisposable
{
    private readonly UdpClient _clientSocket = new(options.ListenEndPoint);

    private readonly ConcurrentDictionary<IPEndPoint, UdpClient> _routes = new();

    private readonly ProxyStatistics _statistics = new(options.EventsPath);

    private IPAddress? _clientAddress;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var upstream = new DatagramPump(
            new LinkImpairment(options.Upstream, options.Seed),
            stopwatch,
            true,
            _statistics,
            async (datagram, token) =>
                await datagram.ServerSocket.SendAsync(datagram.Buffer, token).ConfigureAwait(false)
        );
        var downstream = new DatagramPump(
            new LinkImpairment(options.Downstream, unchecked(options.Seed * 397) ^ 0x5f3759df),
            stopwatch,
            false,
            _statistics,
            async (datagram, token) =>
            {
                await _clientSocket.SendAsync(datagram.Buffer, datagram.ClientEndPoint, token).ConfigureAwait(false);
            }
        );

        await using var registration = cancellationToken.Register(() =>
            {
                _clientSocket.Close();
                foreach (var socket in _routes.Values)
                {
                    socket.Close();
                }
            }
        );

        var tasks = new[]
        {
            upstream.RunAsync(cancellationToken),
            downstream.RunAsync(cancellationToken),
            ReceiveClientAsync(upstream, downstream, cancellationToken),
            _statistics.RunReporterAsync(cancellationToken)
        };
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            upstream.Complete();
            downstream.Complete();
        }
    }

    private async Task ReceiveClientAsync(
        DatagramPump upstream,
        DatagramPump downstream,
        CancellationToken cancellationToken
    )
    {
        var receivers = new List<Task>();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var received = await _clientSocket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                _clientAddress ??= received.RemoteEndPoint.Address;
                if (!_clientAddress.Equals(received.RemoteEndPoint.Address))
                {
                    await Console.Error.WriteLineAsync(
                        $"Ignoring datagram from {received.RemoteEndPoint}; this proxy serves {_clientAddress}."
                    );
                    continue;
                }

                if (!_routes.TryGetValue(received.RemoteEndPoint, out var socket))
                {
                    // Bound temporary discovery sockets without allowing arbitrary route growth.
                    if (_routes.Count >= 64)
                    {
                        await Console.Error.WriteLineAsync(
                            $"Ignoring new endpoint {received.RemoteEndPoint}; restart the proxy after 64 routes."
                        );
                        continue;
                    }

                    socket = new UdpClient(AddressFamily.InterNetwork);
                    socket.Connect(options.TargetEndPoint);
                    _routes[received.RemoteEndPoint] = socket;
                    receivers.Add(ReceiveServerAsync(socket, received.RemoteEndPoint, downstream, cancellationToken));
                    Console.WriteLine($"Accepted client endpoint {received.RemoteEndPoint}.");
                }

                _statistics.Received(true, received.Buffer.Length);
                await upstream.EnqueueAsync(
                        new RoutedDatagram(received.Buffer, socket, received.RemoteEndPoint),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (var socket in _routes.Values)
            {
                socket.Close();
            }

            await Task.WhenAll(receivers).ConfigureAwait(false);
        }
    }

    private async Task ReceiveServerAsync(
        UdpClient socket,
        IPEndPoint clientEndPoint,
        DatagramPump pump,
        CancellationToken cancellationToken
    )
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var received = await socket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            _statistics.Received(false, received.Buffer.Length);
            await pump.EnqueueAsync(new RoutedDatagram(received.Buffer, socket, clientEndPoint), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _clientSocket.Dispose();
        foreach (var socket in _routes.Values)
        {
            socket.Dispose();
        }

        await _statistics.DisposeAsync().ConfigureAwait(false);
    }

    private sealed record RoutedDatagram(byte[] Buffer, UdpClient ServerSocket, IPEndPoint ClientEndPoint);

    private sealed class DatagramPump(
        LinkImpairment impairment,
        Stopwatch stopwatch,
        bool upstream,
        ProxyStatistics statistics,
        Func<RoutedDatagram, CancellationToken, ValueTask> forward
    )
    {
        private readonly Channel<RoutedDatagram> _channel = Channel.CreateBounded<RoutedDatagram>(
            new BoundedChannelOptions(65_536)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            }
        );

        public ValueTask EnqueueAsync(RoutedDatagram datagram, CancellationToken cancellationToken) =>
            _channel.Writer.WriteAsync(datagram, cancellationToken);

        public void Complete() => _channel.Writer.TryComplete();

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await foreach (var datagram in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var decision = impairment.Decide(datagram.Buffer.Length, stopwatch.Elapsed);
                if (decision.Drop)
                {
                    statistics.Dropped(upstream);
                    continue;
                }

                statistics.Scheduled(upstream, 1);
                _ = ForwardLaterAsync(datagram, decision.Delay, cancellationToken);
            }
        }

        private async Task ForwardLaterAsync(
            RoutedDatagram datagram,
            TimeSpan delay,
            CancellationToken cancellationToken
        )
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                if (impairment.IsOutage(stopwatch.Elapsed))
                {
                    statistics.Dropped(upstream);
                    return;
                }

                await forward(datagram, cancellationToken).ConfigureAwait(false);
                statistics.Forwarded(upstream);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
                statistics.Scheduled(upstream, -1);
            }
        }
    }
}

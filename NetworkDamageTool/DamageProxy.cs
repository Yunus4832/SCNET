using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace NetworkDamageTool;

public sealed class DamageProxy : IAsyncDisposable
{
    private readonly UdpClient _clientSocket;

    private readonly DamageProxyOptions _options;

    private readonly UdpClient _serverSocket;

    private readonly ProxyStatistics _statistics;

    private IPEndPoint? _clientEndPoint;

    public DamageProxy(DamageProxyOptions options)
    {
        _options = options;
        _clientSocket = new UdpClient(options.ListenEndPoint);
        _serverSocket = new UdpClient(AddressFamily.InterNetwork);
        _serverSocket.Connect(options.TargetEndPoint);
        _statistics = new ProxyStatistics(options.EventsPath);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var upstream = new DatagramPump(
            new LinkImpairment(_options.Upstream, _options.Seed),
            stopwatch,
            true,
            _statistics,
            async (datagram, token) =>
                await _serverSocket.SendAsync(datagram, token).ConfigureAwait(false));
        var downstream = new DatagramPump(
            new LinkImpairment(_options.Downstream, unchecked(_options.Seed * 397) ^ 0x5f3759df),
            stopwatch,
            false,
            _statistics,
            async (datagram, token) =>
            {
                var client = Volatile.Read(ref _clientEndPoint);
                if (client != null)
                {
                    await _clientSocket.SendAsync(datagram, client, token).ConfigureAwait(false);
                }
            });

        using var registration = cancellationToken.Register(() =>
        {
            _clientSocket.Close();
            _serverSocket.Close();
        });

        var tasks = new[]
        {
            upstream.RunAsync(cancellationToken),
            downstream.RunAsync(cancellationToken),
            ReceiveClientAsync(upstream, cancellationToken),
            ReceiveServerAsync(downstream, cancellationToken),
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

    private async Task ReceiveClientAsync(DatagramPump pump, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var received = await _clientSocket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            var knownClient = Volatile.Read(ref _clientEndPoint);
            if (knownClient == null)
            {
                Volatile.Write(ref _clientEndPoint, received.RemoteEndPoint);
                Console.WriteLine($"Accepted client endpoint {received.RemoteEndPoint}.");
            }
            else if (!knownClient.Equals(received.RemoteEndPoint))
            {
                Console.Error.WriteLine(
                    $"Ignoring datagram from {received.RemoteEndPoint}; this proxy instance serves {knownClient}.");
                continue;
            }

            _statistics.Received(true, received.Buffer.Length);
            await pump.EnqueueAsync(received.Buffer, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReceiveServerAsync(DatagramPump pump, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var received = await _serverSocket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            _statistics.Received(false, received.Buffer.Length);
            await pump.EnqueueAsync(received.Buffer, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _clientSocket.Dispose();
        _serverSocket.Dispose();
        await _statistics.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class DatagramPump
    {
        private readonly Channel<byte[]> _channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(65_536)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> _forward;
        private readonly LinkImpairment _impairment;
        private readonly ProxyStatistics _statistics;
        private readonly Stopwatch _stopwatch;
        private readonly bool _upstream;

        public DatagramPump(
            LinkImpairment impairment,
            Stopwatch stopwatch,
            bool upstream,
            ProxyStatistics statistics,
            Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> forward)
        {
            _impairment = impairment;
            _stopwatch = stopwatch;
            _upstream = upstream;
            _statistics = statistics;
            _forward = forward;
        }

        public ValueTask EnqueueAsync(byte[] datagram, CancellationToken cancellationToken) =>
            _channel.Writer.WriteAsync(datagram, cancellationToken);

        public void Complete() => _channel.Writer.TryComplete();

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await foreach (var datagram in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var decision = _impairment.Decide(datagram.Length, _stopwatch.Elapsed);
                if (decision.Drop)
                {
                    _statistics.Dropped(_upstream);
                    continue;
                }

                _statistics.Scheduled(_upstream, 1);
                _ = ForwardLaterAsync(datagram, decision.Delay, cancellationToken);
            }
        }

        private async Task ForwardLaterAsync(
            byte[] datagram,
            TimeSpan delay,
            CancellationToken cancellationToken)
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                await _forward(datagram, cancellationToken).ConfigureAwait(false);
                _statistics.Forwarded(_upstream);
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
                _statistics.Scheduled(_upstream, -1);
            }
        }
    }
}

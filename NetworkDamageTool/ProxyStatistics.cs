using System.Text.Json;

namespace NetworkDamageTool;

public sealed class ProxyStatistics : IAsyncDisposable
{
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    private readonly StreamWriter? _writer;

    private long _downDropped;
    private long _downForwarded;
    private long _downReceived;
    private long _downReceivedBytes;
    private long _downScheduled;
    private long _upDropped;
    private long _upForwarded;
    private long _upReceived;
    private long _upReceivedBytes;
    private long _upScheduled;

    public ProxyStatistics(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _writer = new StreamWriter(fullPath, append: false) { AutoFlush = true };
    }

    public void Received(bool upstream, int bytes)
    {
        if (upstream)
        {
            Interlocked.Increment(ref _upReceived);
            Interlocked.Add(ref _upReceivedBytes, bytes);
        }
        else
        {
            Interlocked.Increment(ref _downReceived);
            Interlocked.Add(ref _downReceivedBytes, bytes);
        }
    }

    public void Dropped(bool upstream) =>
        Interlocked.Increment(ref upstream ? ref _upDropped : ref _downDropped);

    public void Forwarded(bool upstream) =>
        Interlocked.Increment(ref upstream ? ref _upForwarded : ref _downForwarded);

    public void Scheduled(bool upstream, int delta) =>
        Interlocked.Add(ref upstream ? ref _upScheduled : ref _downScheduled, delta);

    public async Task RunReporterAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            var snapshot = new
            {
                timestamp = DateTimeOffset.UtcNow,
                elapsedMs = (long)(DateTimeOffset.UtcNow - _startedAt).TotalMilliseconds,
                upstream = new
                {
                    received = Interlocked.Read(ref _upReceived),
                    receivedBytes = Interlocked.Read(ref _upReceivedBytes),
                    forwarded = Interlocked.Read(ref _upForwarded),
                    dropped = Interlocked.Read(ref _upDropped),
                    scheduled = Interlocked.Read(ref _upScheduled)
                },
                downstream = new
                {
                    received = Interlocked.Read(ref _downReceived),
                    receivedBytes = Interlocked.Read(ref _downReceivedBytes),
                    forwarded = Interlocked.Read(ref _downForwarded),
                    dropped = Interlocked.Read(ref _downDropped),
                    scheduled = Interlocked.Read(ref _downScheduled)
                }
            };
            var json = JsonSerializer.Serialize(snapshot);
            Console.WriteLine(json);
            if (_writer != null)
            {
                await _writer.WriteLineAsync(json).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_writer != null)
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
        }
    }
}

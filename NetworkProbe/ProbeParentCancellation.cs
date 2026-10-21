using System.IO.Pipes;

namespace NetworkProbe;

internal sealed class ProbeParentCancellation : IAsyncDisposable
{
    internal const string environmentKey = "SCNET_PROBE_PARENT_PIPE";

    private readonly CancellationTokenSource _stop = new();

    private readonly NamedPipeClientStream? _pipe;

    private readonly Task _watch;

    public ProbeParentCancellation(CancellationTokenSource operation)
    {
        var name = Environment.GetEnvironmentVariable(environmentKey);
        if (string.IsNullOrEmpty(name))
        {
            _watch = Task.CompletedTask;
            return;
        }

        _pipe = new NamedPipeClientStream(".", name, PipeDirection.In, PipeOptions.Asynchronous);
        _watch = WatchAsync(operation);
    }

    private async Task WatchAsync(CancellationTokenSource operation)
    {
        try
        {
            await _pipe!.ConnectAsync(10000, _stop.Token);
            var message = new byte[1];
            await _pipe.ReadExactlyAsync(message, _stop.Token);
            operation.Cancel();
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or TimeoutException)
        {
            if (!_stop.IsCancellationRequested)
            {
                operation.Cancel();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        try
        {
            await _watch;
        }
        finally
        {
            _pipe?.Dispose();
            _stop.Dispose();
        }
    }
}

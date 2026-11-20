using System.Threading.Channels;

namespace Engine.Audio;

/// <summary>Bounded CPU preparation, shared by buffered assets and streaming reads.</summary>
internal sealed class AudioPreparation
{
    private readonly Channel<IWork> _queue = Channel.CreateBounded<IWork>(
        new BoundedChannelOptions(64)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        }
    );

    private readonly Task[] _workers;

    private readonly SemaphoreSlim _slots = new(64, 64);

    private int _stopped;

    public AudioPreparation()
    {
        _workers = Enumerable.Range(0, 2).Select(_ => Task.Run(Run)).ToArray();
    }

    public Task Completion => Task.WhenAll(_workers);

    public bool TrySubmit<T>(Func<T> prepare, CancellationToken cancellation, out Task<T>? task)
    {
        task = null;
        if (Volatile.Read(ref _stopped) != 0 || !_slots.Wait(0)) { return false; }

        task = EnqueueReserved(prepare, cancellation);
        return true;
    }

    public async Task<T> Submit<T>(Func<T> prepare, CancellationToken cancellation)
    {
        if (Volatile.Read(ref _stopped) != 0)
        {
            throw new OperationCanceledException("Audio preparation has stopped.");
        }

        await _slots.WaitAsync(cancellation).ConfigureAwait(false);
        if (Volatile.Read(ref _stopped) != 0)
        {
            _slots.Release();
            throw new OperationCanceledException("Audio preparation has stopped.");
        }

        return await EnqueueReserved(prepare, cancellation).ConfigureAwait(false);
    }

    private Task<T> EnqueueReserved<T>(Func<T> prepare, CancellationToken cancellation)
    {
        var work = new Work<T>(prepare, cancellation);
        if (!_queue.Writer.TryWrite(work))
        {
            _slots.Release();
            work.Reject();
        }

        return work.Task;
    }

    public void Stop()
    {
        Interlocked.Exchange(ref _stopped, 1);
        _queue.Writer.TryComplete();
    }

    private async Task Run()
    {
        await foreach (var work in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                if (Volatile.Read(ref _stopped) != 0) { work.Reject(); }
                else { work.Execute(); }
            }
            finally { _slots.Release(); }
        }
    }

    private interface IWork
    {
        void Execute();

        void Reject();
    }

    private sealed class Work<T>(Func<T> prepare, CancellationToken cancellation) : IWork
    {
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Task => _completion.Task;

        public void Execute()
        {
            if (cancellation.IsCancellationRequested)
            {
                _completion.TrySetCanceled(cancellation);
                return;
            }

            try { _completion.TrySetResult(prepare()); }
            catch (Exception error) { _completion.TrySetException(error); }
        }

        public void Reject()
        {
            _completion.TrySetException(new InvalidOperationException("Audio preparation is unavailable or full."));
        }
    }
}

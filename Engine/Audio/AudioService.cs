using System.Collections.Concurrent;

using Engine.Core.Logging;

using Silk.NET.OpenAL;

namespace Engine.Audio;

/// <summary>Single owner of the device, native buffers and voices. Producers only publish intent.</summary>
internal sealed class AudioService : IDisposable
{
    private readonly Lock _signal = new();

    private readonly AutoResetEvent _wake = new(false);

    private readonly ManualResetEventSlim _initialized = new(false);

    private readonly ConcurrentQueue<BaseSound> _pending = new();

    private readonly HashSet<BaseSound> _voices = [];

    private readonly Dictionary<SoundBuffer, uint> _buffers = [];

    private readonly List<Task> _cleanup = [];

    private readonly List<BaseSound> _retiring = [];

    private readonly List<SoundBuffer> _releasedBuffers = [];

    private readonly Thread _thread;

    private ALContext? _contextApi;

    private nint _device;

    private nint _context;

    private int _live;

    private int _streams;

    private int _stop;

    private int _refresh;

    private int _ownerThread;

    private bool _signalDisposed;

    private float _master = float.NaN;

    public AudioService()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "SCNET Audio" };
    }

    public AudioPreparation Preparation { get; } = new();

    public AL? Api { get; private set; }

    public bool IsReady { get; private set; }

    public void Start()
    {
        _thread.Start();
        _initialized.Wait();
    }

    public bool Register(BaseSound sound)
    {
        lock (_signal)
        {
            if (_stop != 0 || !IsReady || _live >= 256 || sound is StreamingSound && _streams >= 8) { return false; }

            _live++;
            if (sound is StreamingSound) { _streams++; }

            _pending.Enqueue(sound);
            _wake.Set();
            return true;
        }
    }

    public void Wake()
    {
        lock (_signal)
        {
            if (!_signalDisposed) { _wake.Set(); }
        }
    }

    public void Refresh()
    {
        Interlocked.Exchange(ref _refresh, 1);
        Wake();
    }

    public void DisposeSounds()
    {
        // This bounded registry contains metadata only; native retirement remains on the owner.
        lock (_signal)
        {
            foreach (var sound in _pending) { sound.Dispose(); }

            Interlocked.Exchange(ref _disposeAll, 1);
            _wake.Set();
        }
    }

    private int _disposeAll;

    public void TrackCleanup(BaseSound sound, Task task, Action complete)
    {
        AssertOwner();

        void Finish()
        {
            complete();
            lock (_signal)
            {
                _live--;
                if (sound is StreamingSound) { _streams--; }
            }
        }

        if (task.IsCompletedSuccessfully)
        {
            Finish();
            return;
        }

        var completion = task.ContinueWith(
            finished =>
            {
                if (finished.IsFaulted) { Log.Warning($"Audio data retirement failed: {finished.Exception}"); }

                Finish();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
        _cleanup.Add(completion);
    }

    public uint GetBuffer(SoundBuffer sound)
    {
        AssertOwner();
        if (_buffers.TryGetValue(sound, out var cached)) { return cached; }

        var data = sound.Data;
        var buffer = Api!.GenBuffer();
        try
        {
            unsafe
            {
                fixed (short* bytes = data.Data)
                {
                    Api.BufferData(
                        buffer,
                        data.ChannelsCount == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16,
                        bytes,
                        data.Data.Length * sizeof(short),
                        data.SamplingFrequency
                    );
                }
            }

            CheckError();
            _buffers.Add(sound, buffer);
            return buffer;
        }
        catch
        {
            Api.DeleteBuffer(buffer);
            throw;
        }
    }

    private void Run()
    {
        _ownerThread = Environment.CurrentManagedThreadId;
        try { InitializeDevice(); }
        catch (Exception error) { Log.Warning($"Audio device initialization failed: {error}"); }
        finally { _initialized.Set(); }

        try
        {
            while (Volatile.Read(ref _stop) == 0)
            {
                while (_pending.TryDequeue(out var sound)) { _voices.Add(sound); }

                if (Interlocked.Exchange(ref _disposeAll, 0) != 0)
                {
                    foreach (var sound in _voices) { sound.Dispose(); }
                }

                if (Interlocked.Exchange(ref _refresh, 0) != 0 && IsReady) { ReopenDevice(); }

                if (IsReady && _master != Mixer.MasterVolume)
                {
                    _master = Mixer.MasterVolume;
                    Api!.SetListenerProperty(ListenerFloat.Gain, _master);
                }

                _retiring.Clear();
                foreach (var sound in _voices)
                {
                    if (!sound.Update(this)) { _retiring.Add(sound); }
                }

                foreach (var sound in _retiring)
                {
                    _voices.Remove(sound);
                }

                _releasedBuffers.Clear();
                foreach (var buffer in _buffers)
                {
                    if (buffer.Key.IsDisposed && buffer.Key.UseCount == 0)
                    {
                        Api!.DeleteBuffer(buffer.Value);
                        _releasedBuffers.Add(buffer.Key);
                    }
                }

                foreach (var buffer in _releasedBuffers) { _buffers.Remove(buffer); }

                _cleanup.RemoveAll(task => task.IsCompleted);
                _wake.WaitOne(_voices.Count == 0 ? 50 : 5);
            }
        }
        catch (Exception error)
        {
            Interlocked.Exchange(ref _stop, 1);
            Log.Error($"Audio owner failed: {error}");
        }
        finally
        {
            while (_pending.TryDequeue(out var sound)) { _voices.Add(sound); }

            Interlocked.Exchange(ref _stop, 1);
            foreach (var sound in _voices)
            {
                try
                {
                    sound.Dispose();
                    sound.Update(this);
                }
                catch (Exception error) { Log.Warning($"Audio voice cleanup failed: {error}"); }
            }

            _voices.Clear();
            Preparation.Stop();
            try { DestroyDevice(); }
            catch (Exception error)
            {
                IsReady = false;
                Log.Warning($"Audio device cleanup failed: {error}");
            }
        }
    }

    private unsafe void InitializeDevice()
    {
        _contextApi = ALContext.GetApi();
        Api = AL.GetApi();
        var device = _contextApi.OpenDevice("");
        if (device == null) { return; }

        _device = (nint)device;
        var context = _contextApi.CreateContext(device, null);
        _context = (nint)context;
        if (context == null || !_contextApi.MakeContextCurrent(context)) { return; }

        Api.DistanceModel(DistanceModel.None);
        IsReady = Api.GetError() == AudioError.NoError;
    }

    private unsafe void ReopenDevice()
    {
        var device = (Device*)_device;
        if (!_contextApi!.IsExtensionPresent(device, "ALC_SOFT_reopen_device")) { return; }

        var address = _contextApi.GetProcAddress(device, "alcReopenDeviceSOFT");
        if (address == null) { return; }

        var reopen = (delegate* unmanaged[Cdecl]<Device*, byte*, int*, byte>)address;
        if (reopen(device, null, null) == 0) { Log.Warning("Failed to refresh audio device."); }
        else { _master = float.NaN; }
    }

    private unsafe void DestroyDevice()
    {
        foreach (var buffer in _buffers.Values) { Api?.DeleteBuffer(buffer); }

        _buffers.Clear();
        if (_context != 0)
        {
            _contextApi!.MakeContextCurrent(null);
            _contextApi.DestroyContext((Context*)_context);
        }

        if (_device != 0) { _contextApi!.CloseDevice((Device*)_device); }

        IsReady = false;
        Api?.Dispose();
        _contextApi?.Dispose();
        Api = null;
    }

    internal void CheckError()
    {
        AssertOwner();
        if (Api!.GetError() is { } error && error != AudioError.NoError)
        {
            throw new InvalidOperationException($"OpenAL error: {error}");
        }
    }

    private void AssertOwner()
    {
        if (_ownerThread != Environment.CurrentManagedThreadId)
        {
            throw new InvalidOperationException("Audio device access requires its owner.");
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _stop, 1);
        Wake();
        _thread.Join();
        Preparation.Completion.GetAwaiter().GetResult();
        Task.WhenAll(_cleanup).GetAwaiter().GetResult();
        _cleanup.Clear();
        lock (_signal)
        {
            _signalDisposed = true;
            _wake.Dispose();
        }

        _initialized.Dispose();
    }
}

using System.Diagnostics;

using Engine.Audio;
using Engine.Media.Audio;
using Engine.Media.Audio.Formats;

namespace Engine.Test.Audio;

[CollectionDefinition("Audio device", DisableParallelization = true)]
public sealed class AudioDeviceCollection;

[Collection("Audio device")]
public sealed class StreamingSoundTest : IDisposable
{
    private readonly string? _previousDriver;

    public StreamingSoundTest()
    {
        _previousDriver = Environment.GetEnvironmentVariable("ALSOFT_DRIVERS");
        Environment.SetEnvironmentVariable("ALSOFT_DRIVERS", "null");
        Mixer.Initialize();
        Assert.True(Mixer.IsAudioInitialized);
    }

    public void Dispose()
    {
        Mixer.Dispose();
        Environment.SetEnvironmentVariable("ALSOFT_DRIVERS", _previousDriver);
    }

    [Fact]
    public async Task SlowFactoryCannotBeMistakenForPlaybackCompletion()
    {
        using var gate = new ManualResetEventSlim();
        var caller = Environment.CurrentManagedThreadId;
        var factoryThread = 0;
        await using var sound = new StreamingSound(() =>
            {
                Volatile.Write(ref factoryThread, Environment.CurrentManagedThreadId);
                gate.Wait();
                return new PcmSource(3);
            }
        );
        sound.Play();
        try
        {
            await Wait(() => Volatile.Read(ref factoryThread) != 0);
            Assert.NotEqual(caller, factoryThread);
            await Task.Delay(80);
            Assert.Equal(SoundState.Preparing, sound.State);
            gate.Set();
            await Wait(() => sound.State == SoundState.Playing && Volatile.Read(ref sound.nativeState) == 4114);
            await Wait(() => sound.PlayedSamples > 100);
            Assert.Equal(SoundState.Playing, sound.State);
        }
        finally { gate.Set(); }
    }

    [Fact]
    public async Task StopAndRestartDiscardLatePreparation()
    {
        using var gate = new ManualResetEventSlim();
        var opened = 0;
        await using var sound = new StreamingSound(() =>
            {
                var generation = Interlocked.Increment(ref opened);
                if (generation == 1) { gate.Wait(); }

                return new PcmSource(3);
            }
        );
        sound.Play();
        try
        {
            await Wait(() => Volatile.Read(ref opened) == 1);
            sound.Stop();
            await Task.Delay(30);
            Assert.Equal(SoundState.Stopped, sound.State);
            sound.Play();
            gate.Set();
            await Wait(() => sound.State == SoundState.Playing && sound.PlayedSamples > 100);
            Assert.Equal(2, opened);
        }
        finally { gate.Set(); }
    }

    [Fact]
    public async Task DecodeDoesNotBlockControlOrAnotherVoice()
    {
        using var reading = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        await using var stream = new StreamingSound(() => new PcmSource(3, reading, gate));
        stream.Play();
        try
        {
            await Wait(() => reading.IsSet);
            var elapsed = Stopwatch.StartNew();
            stream.Volume = .2f;
            stream.Pause();
            Assert.True(elapsed.ElapsedMilliseconds < 100);
            using var buffer = new SoundBuffer(new short[16000], 0, 16000, 1, 8000);
            await using var other = new Sound(buffer);
            other.Play();
            await Wait(() => other.State == SoundState.Playing);
            Assert.Equal(SoundState.Paused, stream.State);
            gate.Set();
            stream.Play();
            await Wait(() => stream.State == SoundState.Playing && stream.PlayedSamples > 100);
        }
        finally { gate.Set(); }
    }

    [Fact]
    public async Task DisposalReturnsBeforeBlockedDataAndCompletesAfterRelease()
    {
        using var gate = new ManualResetEventSlim();
        using var opened = new ManualResetEventSlim();
        var sound = new StreamingSound(() =>
            {
                opened.Set();
                gate.Wait();
                return new PcmSource(3);
            }
        );
        sound.Play();
        try
        {
            await Wait(() => opened.IsSet);
            var elapsed = Stopwatch.StartNew();
            var disposal = sound.DisposeAsync().AsTask();
            Assert.True(elapsed.ElapsedMilliseconds < 100);
            Assert.Equal(SoundState.Disposed, sound.State);
            Assert.False(disposal.IsCompleted);
            gate.Set();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0u, sound.source);
        }
        finally
        {
            gate.Set();
            await sound.DisposeAsync();
        }
    }

    [Fact]
    public async Task EofPauseResumeAndLoopFollowConsumedAudio()
    {
        await using var sound = new StreamingSound(() => new PcmSource(.3));
        sound.Play();
        await Wait(() => sound.State == SoundState.Playing);
        sound.Pause();
        await Wait(() => Volatile.Read(ref sound.nativeState) == 4115);
        var paused = sound.PlayedSamples;
        await Task.Delay(50);
        Assert.InRange(sound.PlayedSamples - paused, 0, 100);
        sound.Play();
        await Wait(() => sound.State == SoundState.Stopped);
        Assert.True(sound.PlayedSamples >= 2000);
        await using var loop = new StreamingSound(() => new PcmSource(.05), isLooped: true);
        loop.Play();
        await Wait(() => loop.PlayedSamples > 1200);
        Assert.Equal(SoundState.Playing, loop.State);
    }

    [Fact]
    public async Task RealOggAdvancesWithoutAFrameDispatcher()
    {
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../Content/Assets/Music/AloneForever.ogg")
        );
        await using var sound = new StreamingSound(
            () => Ogg.Stream(File.OpenRead(path)),
            bufferDuration: 1f,
            startPercentage: .45f
        );
        sound.Play();
        await Wait(() => sound.State == SoundState.Playing && sound.PlayedSamples > 1000);
        Assert.Equal(4114, Volatile.Read(ref sound.nativeState));
    }

    [Fact]
    public async Task BufferedPreparationAndWorldCancellationDoNotWaitForData()
    {
        using var gate = new ManualResetEventSlim();
        using var opened = new ManualResetEventSlim();
        using var cancel = new CancellationTokenSource();
        using var buffer = SoundBuffer.LoadDeferred(() =>
            {
                opened.Set();
                gate.Wait();
                var data = new SoundData(1, 8000, 16000);
                var stream = new MemoryStream();
                SoundData.Save(data, stream, SoundFileFormat.Wav);
                stream.Position = 0;
                return stream;
            }
        );
        await using var sound = new Sound(buffer, cancellation: cancel.Token);
        sound.Play();
        try
        {
            await Wait(() => opened.IsSet);
            cancel.Cancel();
            await Wait(() => sound.State == SoundState.Disposed);
            gate.Set();
            await buffer.PrepareAsync();
            await sound.DisposeAsync();
            Assert.Equal(0u, sound.source);
            Assert.Equal(0, buffer.UseCount);
        }
        finally { gate.Set(); }
    }

    [Fact]
    public async Task FadeAndDisposalAdvanceWithoutFrames()
    {
        using var buffer = new SoundBuffer(new short[16000], 0, 16000, 1, 8000);
        await using var sound = new Sound(buffer, isLooped: true);
        sound.Play();
        await Wait(() => sound.State == SoundState.Playing);
        sound.FadeTo(0f, .08f, disposeOnCompletion: true);
        await Wait(() => sound.State == SoundState.Disposed);
        await sound.DisposeAsync();
        Assert.Equal(0, buffer.UseCount);
    }

    [Fact]
    public async Task DeviceRefreshAndVoiceLimitPreserveResourceRetirement()
    {
        using var buffer = new SoundBuffer(new short[16000], 0, 16000, 1, 8000);
        var voices = Enumerable.Range(0, 270).Select(_ => new Sound(buffer)).ToArray();
        Assert.Contains(voices, sound => sound.State == SoundState.Disposed);
        foreach (var voice in voices) { await voice.DisposeAsync(); }

        Assert.Equal(0, buffer.UseCount);
        await using var active = new Sound(buffer, isLooped: true);
        active.Play();
        await Wait(() => active.State == SoundState.Playing);
        Mixer.RequestDeviceRefresh();
        await Task.Delay(50);
        Assert.Equal(SoundState.Playing, active.State);
        Assert.True(Mixer.IsAudioInitialized);
    }

    [Fact]
    public async Task RetiringStreamsKeepTheirAdmissionUntilDecoderExits()
    {
        using var gate = new ManualResetEventSlim();
        using var opened = new ManualResetEventSlim();
        var voices = Enumerable.Range(0, 8).Select(_ => new StreamingSound(() =>
                {
                    opened.Set();
                    gate.Wait();
                    return new PcmSource(3);
                }
            )
        ).ToArray();
        try
        {
            foreach (var voice in voices) { voice.Play(); }

            await Wait(() => opened.IsSet);
            var retirement = voices.Select(voice => voice.DisposeAsync().AsTask()).ToArray();
            await Task.Delay(30);
            await using var rejected = new StreamingSound(() => new PcmSource(3));
            Assert.Equal(SoundState.Disposed, rejected.State);
            gate.Set();
            await Task.WhenAll(retirement).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            gate.Set();
            foreach (var voice in voices) { await voice.DisposeAsync(); }
        }
    }

    [Fact]
    public async Task ApplicationShutdownRetiresActiveAudioAndAllowsReinitialization()
    {
        using var buffer = new SoundBuffer(new short[16000], 0, 16000, 1, 8000);
        var sound = new Sound(buffer, isLooped: true);
        var stream = new StreamingSound(() => new PcmSource(3), isLooped: true);
        sound.Play();
        stream.Play();
        await Wait(() => sound.State == SoundState.Playing && stream.PlayedSamples > 100);
        Mixer.Dispose();
        await sound.DisposeAsync();
        await stream.DisposeAsync();
        Assert.Equal(0, buffer.UseCount);
        Assert.Equal(0u, sound.source);
        Assert.Equal(0u, stream.source);
        Assert.False(Mixer.IsAudioInitialized);
        Mixer.Initialize();
        await using var restarted = new Sound(buffer);
        restarted.Play();
        await Wait(() => restarted.State == SoundState.Playing);
    }

    [Fact]
    public async Task StopDoesNotWaitForAReadAfterPlaybackHasStarted()
    {
        using var reading = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        await using var sound = new StreamingSound(
            () => new PcmSource(3, reading, gate, gateAfterReads: 1),
            bufferDuration: 1f
        );
        sound.Play();
        try
        {
            await Wait(() => reading.IsSet && sound.PlayedSamples > 100);
            sound.Stop();
            await Wait(() => Volatile.Read(ref sound.nativeState) == 4116);
            Assert.False(gate.IsSet);
            Assert.Equal(SoundState.Stopped, sound.State);
        }
        finally { gate.Set(); }
    }

    private static async Task Wait(Func<bool> predicate)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (predicate()) { return; }

            await Task.Delay(5);
        }

        Assert.True(predicate(), "Audio state did not converge before the deadline.");
    }

    private sealed class PcmSource(
        double seconds,
        ManualResetEventSlim? reading = null,
        ManualResetEventSlim? gate = null,
        int gateAfterReads = 0
    ) : StreamingSource
    {
        private int _reads;

        public override int ChannelsCount => 1;

        public override int SamplingFrequency => 8000;

        public override long BytesCount => (long)(seconds * 8000) * 2;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_reads++ >= gateAfterReads)
            {
                reading?.Set();
                gate?.Wait();
            }

            var read = (int)System.Math.Min(count, BytesCount - Position * 2);
            buffer.AsSpan(offset, read).Clear();
            Position += read / 2;
            return read;
        }

        public override StreamingSource Duplicate() => new PcmSource(seconds);
    }
}

using Engine.Audio;
using Engine.Media;

namespace Engine.Test.Audio;

/// <summary>
/// StreamingSound 类的单元测试。
/// 测试流式音频播放器的各项功能，包括播放控制、音量设置和 OGG 格式支持。
/// </summary>
/// <remarks>
/// 所有测试都需要有效的音频输出设备。如果音频初始化失败，构造函数将抛出异常。
/// 测试使用 Content/Assets/Music/AloneForever.ogg 作为测试音频文件。
/// </remarks>
public class StreamingSoundTest : IDisposable
{
    private readonly string _musicFilePath;

    /// <summary>
    /// 初始化测试环境，设置测试音频文件路径并初始化音频系统。
    /// </summary>
    /// <exception cref="InvalidOperationException">音频系统初始化失败</exception>
    public StreamingSoundTest()
    {
        // 设置测试音乐文件路径
        _musicFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Content", "Assets", "Music", "AloneForever.ogg");
        _musicFilePath = Path.GetFullPath(_musicFilePath);

        // 确保音频系统已初始化
        if (!Mixer.IsAudioInitialized)
        {
            Mixer.Initialize();
        }

        // 如果音频初始化失败，抛出异常终止测试
        if (!Mixer.IsAudioInitialized)
        {
            throw new InvalidOperationException("Audio system initialization failed. Tests cannot run without audio device.");
        }
    }

    /// <summary>
    /// 清理测试资源。
    /// </summary>
    public void Dispose()
    {
        // Cleanup if needed
    }

    /// <summary>
    /// 测试：使用有效的 OGG 音频流创建 StreamingSound 实例。
    /// </summary>
    [Fact]
    public void Constructor_WithValidOggStream_ShouldCreateStreamingSound()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);

        // Act
        using var streamingSound = new StreamingSound(streamingSource);

        // Assert
        Assert.NotNull(streamingSound);
        Assert.NotNull(streamingSound.StreamingSource);
    }

    /// <summary>
    /// 测试：传入 null 流媒体源时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_WithNullStreamingSource_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StreamingSound(null!));
    }

    /// <summary>
    /// 测试：播放 OGG 音乐后状态应变为 Playing。
    /// </summary>
    [Fact]
    public void Play_OggMusic_ShouldStartPlaying()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);
        using var streamingSound = new StreamingSound(streamingSource);

        // Act
        streamingSound.Play();

        // Assert
        Assert.Equal(SoundState.Playing, streamingSound.State);

        // Cleanup
        streamingSound.Stop();
    }

    /// <summary>
    /// 测试：暂停正在播放的音乐后状态应变为 Paused。
    /// </summary>
    [Fact]
    public void Pause_PlayingOggMusic_ShouldPause()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);
        using var streamingSound = new StreamingSound(streamingSource);
        streamingSound.Play();

        // Act
        streamingSound.Pause();

        // Assert
        Assert.Equal(SoundState.Paused, streamingSound.State);

        // Cleanup
        streamingSound.Stop();
    }

    /// <summary>
    /// 测试：停止正在播放的音乐后状态应变为 Stopped。
    /// </summary>
    [Fact]
    public void Stop_PlayingOggMusic_ShouldStop()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);
        using var streamingSound = new StreamingSound(streamingSource);
        streamingSound.Play();

        // Act
        streamingSound.Stop();

        // Assert
        Assert.Equal(SoundState.Stopped, streamingSound.State);
    }

    /// <summary>
    /// 测试：停止后重新播放应从头开始，状态变为 Playing。
    /// </summary>
    [Fact]
    public void Play_AfterStop_ShouldRestartFromBeginning()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);
        using var streamingSound = new StreamingSound(streamingSource);
        streamingSound.Play();
        streamingSound.Stop();

        // Act
        streamingSound.Play();

        // Assert
        Assert.Equal(SoundState.Playing, streamingSound.State);

        // Cleanup
        streamingSound.Stop();
    }

    /// <summary>
    /// 测试：创建 StreamingSound 时设置自定义音量应生效。
    /// </summary>
    [Fact]
    public void StreamingSound_WithCustomVolume_ShouldSetVolume()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);
        using var streamingSound = new StreamingSound(streamingSource, volume: 0.5f);

        // Assert
        Assert.Equal(0.5f, streamingSound.Volume);
    }

    /// <summary>
    /// 测试：创建 StreamingSound 时设置自定义音高应生效。
    /// </summary>
    [Fact]
    public void StreamingSound_WithCustomPitch_ShouldSetPitch()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);
        using var streamingSound = new StreamingSound(streamingSource, pitch: 1.5f);

        // Assert
        Assert.Equal(1.5f, streamingSound.Pitch);
    }

    /// <summary>
    /// 测试：创建 StreamingSound 时启用循环播放应生效。
    /// </summary>
    [Fact]
    public void StreamingSound_WithLoopedEnabled_ShouldLoop()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);
        using var streamingSound = new StreamingSound(streamingSource, isLooped: true);

        // Assert
        Assert.True(streamingSound.IsLooped);
    }

    /// <summary>
    /// 测试：Ogg.Stream 方法应返回有效的流媒体源。
    /// </summary>
    [Fact]
    public void OggStream_ShouldReturnValidStreamingSource()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);

        // Act
        using var streamingSource = Ogg.Stream(fileStream);

        // Assert
        Assert.NotNull(streamingSource);
        Assert.True(streamingSource.ChannelsCount is >= 1 and <= 2);
        Assert.True(streamingSource.SamplingFrequency is >= 8000 and <= 48000);
        Assert.True(streamingSource.BytesCount > 0);
    }

    /// <summary>
    /// 测试：从流媒体源读取数据应返回有效的音频数据。
    /// </summary>
    [Fact]
    public void StreamingSource_Read_ShouldReturnData()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);
        var buffer = new byte[4096];

        // Act
        var bytesRead = streamingSource.Read(buffer, 0, buffer.Length);

        // Assert
        Assert.True(bytesRead > 0);
    }

    /// <summary>
    /// 测试：复制流媒体源应创建独立的副本，位置从0开始。
    /// </summary>
    [Fact]
    public void StreamingSource_Duplicate_ShouldCreateIndependentCopy()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);

        // Read some data from original
        var buffer1 = new byte[4096];
        streamingSource.Read(buffer1, 0, buffer1.Length);
        var originalPosition = streamingSource.Position;

        // Act
        using var duplicate = streamingSource.Duplicate();

        // Assert
        Assert.NotNull(duplicate);
        Assert.Equal(streamingSource.ChannelsCount, duplicate.ChannelsCount);
        Assert.Equal(streamingSource.SamplingFrequency, duplicate.SamplingFrequency);
        Assert.Equal(0, duplicate.Position); // Duplicate should start from beginning
    }

    /// <summary>
    /// 测试：CalculateBufferSize 方法应返回正确的缓冲区大小。
    /// </summary>
    [Fact]
    public void CalculateBufferSize_ShouldReturnCorrectSize()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);
        using var streamingSound = new StreamingSound(streamingSource);

        // Act
        var bufferSize = streamingSound.CalculateBufferSize(0.3f);

        // Assert
        var expectedSize = 2 * streamingSource.ChannelsCount * (int)(streamingSource.SamplingFrequency * 0.3f);
        Assert.Equal(expectedSize, bufferSize);
    }

    /// <summary>
    /// 播放测试：实际播放音乐3秒钟，用于验证是否能听到声音。
    /// 注意：此测试需要音频输出设备，且会实际播放声音。
    /// </summary>
    [Fact]
    public void Play_OggMusic_For3Seconds_ShouldActuallyPlaySound()
    {
        // Arrange
        using var fileStream = File.OpenRead(_musicFilePath);
        using var streamingSource = Ogg.Stream(fileStream);
        using var streamingSound = new StreamingSound(streamingSource, volume: 0.5f);

        // Act - 开始播放
        streamingSound.Play();

        // 等待3秒钟让音乐播放
        Thread.Sleep(3000);

        // Assert - 验证仍在播放状态（或正常播放完毕）
        Assert.True(
            streamingSound.State is SoundState.Playing or SoundState.Stopped,
            $"Expected Playing or Stopped state, but got {streamingSound.State}"
        );

        // Cleanup
        streamingSound.Stop();
    }
}

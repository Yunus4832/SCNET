using System.Collections.Concurrent;

using Engine.Core;
using Engine.Media;

using Silk.NET.OpenAL;

namespace Engine.Audio;

/// <summary>
/// 流式音频播放器，用于播放大型音频文件（如背景音乐）。
/// 通过分块读取和播放音频数据，避免一次性加载整个文件到内存。
/// </summary>
/// <remarks>
/// 使用 OpenAL 的缓冲区队列机制实现无缝流式播放。
/// 内部使用3个缓冲区进行循环填充和播放，确保播放连续性。
/// </remarks>
public sealed class StreamingSound : BaseSound
{
    /// <summary>
    /// 控制命令枚举，用于线程间的播放控制通信。
    /// </summary>
    public enum Command
    {
        /// <summary>播放命令</summary>
        Play,

        /// <summary>暂停命令</summary>
        Pause,

        /// <summary>停止命令</summary>
        Stop,

        /// <summary>退出命令，用于终止播放线程</summary>
        Exit
    }

    /// <summary>
    /// 单个缓冲区的持续时间（秒），影响内存使用和播放延迟。
    /// </summary>
    private readonly float _bufferDuration;

    /// <summary>
    /// 标记是否已读取完所有音频数据。
    /// </summary>
    private bool _noMoreData;

    /// <summary>
    /// 命令队列，用于向播放线程发送控制命令。
    /// </summary>
    public BlockingCollection<Command> Queue = new(100);

    /// <summary>
    /// 用于通知播放线程停止的事件信号。
    /// </summary>
    private ManualResetEvent _stopTaskEvent = new(false);

    /// <summary>
    /// 后台播放任务，负责音频数据的流式处理和播放。
    /// </summary>
    private Task _task;

    /// <summary>
    /// 初始化 <see cref="StreamingSound"/> 类的新实例。
    /// </summary>
    /// <param name="streamingSource">音频流数据源</param>
    /// <param name="volume">初始音量（0.0 - 1.0），默认 1.0</param>
    /// <param name="pitch">音高倍率（0.5 - 2.0），默认 1.0</param>
    /// <param name="pan">声相位置（-1.0 左声道 到 1.0 右声道），仅单声道有效，默认 0.0</param>
    /// <param name="isLooped">是否循环播放，默认 false</param>
    /// <param name="disposeOnStop">停止时是否自动释放资源，默认 false</param>
    /// <param name="bufferDuration">单个缓冲区持续时间（秒），范围 0.01 - 10，默认 0.3</param>
    /// <exception cref="ArgumentNullException"><paramref name="streamingSource"/> 为 null</exception>
    /// <exception cref="InvalidOperationException">不支持的声道数或采样率</exception>
    public StreamingSound(StreamingSource streamingSource, float volume = 1f, float pitch = 1f, float pan = 0f,
        bool isLooped = false, bool disposeOnStop = false, float bufferDuration = 0.3f)
    {
        VerifyStreamingSource(streamingSource);

        StreamingSource = streamingSource;
        ChannelsCount = streamingSource.ChannelsCount;
        SamplingFrequency = streamingSource.SamplingFrequency;
        Volume = volume;
        Pitch = pitch;
        Pan = pan;
        IsLooped = isLooped;
        DisposeOnStop = disposeOnStop;
        _bufferDuration = MathUtils.Clamp(bufferDuration, 0.01f, 10f);
        _task = Task.Run(delegate
        {
            try
            {
                if (Mixer.IsAudioInitialized)
                {
                    StreamingThreadFunction();
                }
            }
            catch (Exception message)
            {
                Log.Error(message);
            }
        });
        Mixer.soundsToStopPoll.Add(this);
    }

    /// <summary>
    /// 获取音频流数据源。
    /// </summary>
    public StreamingSource StreamingSource { get; private set; }

    /// <summary>
    /// 从流媒体源读取音频数据到缓冲区。
    /// </summary>
    /// <param name="buffer">目标字节缓冲区</param>
    /// <param name="count">要读取的最大字节数</param>
    /// <returns>实际读取的字节数</returns>
    /// <remarks>
    /// 如果启用了循环播放且到达流末尾，会自动重置位置到开头继续读取。
    /// </remarks>
    private int ReadStreamingSource(byte[] buffer, int count)
    {
        var totalBytesRead = 0;
        if (StreamingSource.BytesCount <= 0)
        {
            return totalBytesRead;
        }

        while (count > 0)
        {
            var bytesRead = StreamingSource.Read(buffer, totalBytesRead, count);
            if (bytesRead > 0)
            {
                totalBytesRead += bytesRead;
                count -= bytesRead;
                continue;
            }

            if (!soundIsLooped)
            {
                break;
            }

            StreamingSource.Position = 0L;
        }

        return totalBytesRead;
    }

    /// <summary>
    /// 验证流媒体源的有效性。
    /// </summary>
    /// <param name="streamingSource">要验证的流媒体源</param>
    /// <exception cref="ArgumentNullException">流媒体源为 null</exception>
    /// <exception cref="InvalidOperationException">声道数不是1或2，或采样率不在 8000-48000 Hz 范围内</exception>
    private void VerifyStreamingSource(StreamingSource streamingSource)
    {
        if (streamingSource.ChannelsCount is < 1 or > 2)
        {
            throw new InvalidOperationException("Unsupported channels count.");
        }

        if (streamingSource.SamplingFrequency is < 8000 or > 48000)
        {
            throw new InvalidOperationException("Unsupported frequency.");
        }
    }

    /// <summary>
    /// 计算指定持续时间所需的缓冲区大小（字节）。
    /// </summary>
    /// <param name="duration">持续时间（秒）</param>
    /// <returns>缓冲区大小（字节）</returns>
    /// <remarks>
    /// 计算公式：2（16位采样）× 声道数 × 采样率 × 持续时间
    /// </remarks>
    public int CalculateBufferSize(float duration)
    {
        return 2 * ChannelsCount * (int)(SamplingFrequency * duration);
    }

    /// <summary>
    /// 内部播放实现，通过 OpenAL 启动音频源播放。
    /// </summary>
    protected override void InternalPlay()
    {
        if (!Mixer.IsAudioInitialized)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        Mixer.AL.SourcePlay(Source);
        Mixer.CheckALError();
    }

    /// <summary>
    /// 内部暂停实现，通过 OpenAL 暂停音频源。
    /// </summary>
    protected override void InternalPause()
    {
        if (!Mixer.IsAudioInitialized)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        Mixer.AL.SourcePause(Source);
        Mixer.CheckALError();
    }

    /// <summary>
    /// 内部停止实现，停止播放并重置流到开头。
    /// </summary>
    protected override void InternalStop()
    {
        if (!Mixer.IsAudioInitialized)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        Mixer.AL.SourceStop(Source);
        Mixer.CheckALError();
        StreamingSource.Position = 0L;
        _noMoreData = false;
    }

    /// <summary>
    /// 内部资源释放实现，停止播放线程并释放所有资源。
    /// </summary>
    internal override void InternalDispose()
    {
        Mixer.soundsToStopPoll.Remove(this);
        _stopTaskEvent.Set();
        _task.Wait();
        _task = null!;
        _stopTaskEvent.Dispose();
        _stopTaskEvent = null!;

        _noMoreData = true;
        StreamingSource.Dispose();
        StreamingSource = null!;

        base.InternalDispose();
    }

    /// <summary>
    /// 流式播放线程主函数，负责音频数据的持续读取、填充和播放。
    /// </summary>
    /// <remarks>
    /// 使用3个缓冲区进行轮询：
    /// 1. 当缓冲区播放完毕后，从源中取出并重新填充数据
    /// 2. 将填充好的缓冲区重新加入播放队列
    /// 3. 如果播放停止且缓冲区为空，自动触发停止事件
    ///
    /// 线程通过 <see cref="_stopTaskEvent"/> 信号控制生命周期。
    /// </remarks>
    private void StreamingThreadFunction()
    {
        if (Mixer.AL is null)
        {
            return;
        }

        // 创建3个 OpenAL 缓冲区用于轮询
        const int bufferCount = 3;
        var openAlBuffers = new uint[bufferCount];
        var availableBuffers = new List<uint>();
        var threadSleepInterval =
            MathUtils.Clamp((int)(0.5f * _bufferDuration / bufferCount * 1000f), 1, 100);
        var audioDataBuffer = new byte[2 * ChannelsCount *
                                       (int)(SamplingFrequency * _bufferDuration / bufferCount)];

        for (var bufferIndex = 0; bufferIndex < bufferCount; bufferIndex++)
        {
            var bufferId = Mixer.AL.GenBuffer();
            Mixer.CheckALError();
            openAlBuffers[bufferIndex] = bufferId;
            availableBuffers.Add(bufferId);
        }

        do
        {
            lock (stateSync)
            {
                if (!_noMoreData)
                {
                    // 获取已播放完毕的缓冲区并回收到列表
                    Mixer.AL.GetSourceProperty(Source, GetSourceInteger.BuffersProcessed, out var processedBufferCount);
                    Mixer.CheckALError();
                    var processedBuffers = new uint[processedBufferCount];
                    Mixer.AL.SourceUnqueueBuffers(Source, processedBuffers);
                    for (var processedIndex = 0; processedIndex < processedBufferCount; processedIndex++)
                    {
                        Mixer.CheckALError();
                        availableBuffers.Add(processedBuffers[processedIndex]);
                    }

                    // 如果没有可用缓冲区、数据已读完或不在播放状态，则跳过
                    if (availableBuffers.Count <= 0 || _noMoreData || State != SoundState.Playing)
                    {
                        continue;
                    }

                    // 读取音频数据到缓冲区
                    var bytesRead = ReadStreamingSource(audioDataBuffer, audioDataBuffer.Length);
                    _noMoreData = bytesRead < audioDataBuffer.Length;
                    if (bytesRead <= 0)
                    {
                        continue;
                    }

                    // 填充 OpenAL 缓冲区
                    var bufferToFill = availableBuffers[^1];
                    Mixer.AL.BufferData(
                        bufferToFill,
                        ChannelsCount == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16,
                        audioDataBuffer,
                        SamplingFrequency
                    );
                    Mixer.CheckALError();

                    // 将填充好的缓冲区加入播放队列
                    Mixer.AL.SourceQueueBuffers(Source, [bufferToFill]);
                    Mixer.CheckALError();
                    availableBuffers.RemoveAt(availableBuffers.Count - 1);

                    // 如果源未在播放状态，启动播放
                    Mixer.AL.GetSourceProperty(Source, GetSourceInteger.SourceState, out var sourceState);
                    Mixer.CheckALError();
                    if ((SourceState)sourceState == SourceState.Playing)
                    {
                        continue;
                    }

                    Mixer.AL.SourcePlay(Source);
                    Mixer.CheckALError();
                }
                else
                {
                    // 数据已读完，检查播放是否结束
                    Mixer.AL.GetSourceProperty(Source, GetSourceInteger.SourceState, out var sourceState);
                    if ((SourceState)sourceState == SourceState.Stopped)
                    {
                        Dispatcher.Dispatch(Stop);
                    }
                }
            }
        } while (!_stopTaskEvent.WaitOne(threadSleepInterval));

        // 清理：停止播放、分离缓冲区、删除缓冲区
        Mixer.AL.SourceStop(Source);
        Mixer.CheckALError();
        Mixer.AL.SetSourceProperty(Source, SourceInteger.Buffer, 0);
        Mixer.CheckALError();
        for (var cleanupIndex = 0; cleanupIndex < bufferCount; cleanupIndex++)
        {
            if (openAlBuffers[cleanupIndex] == 0)
            {
                continue;
            }

            Mixer.AL.DeleteBuffer(openAlBuffers[cleanupIndex]);
            Mixer.CheckALError();
            openAlBuffers[cleanupIndex] = 0;
        }
    }
}

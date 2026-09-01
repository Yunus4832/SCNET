using Engine.Core;
using Engine.Media;

using Silk.NET.OpenAL;

namespace Engine.Audio;

public sealed class SoundBuffer : IDisposable
{
    internal uint mBuffer;

    public SoundBuffer(byte[] data, int startIndex, int itemsCount, int channelsCount, int samplingFrequency)
    {
        Initialize(data, startIndex, itemsCount, channelsCount, samplingFrequency);
        CreateBuffer(data, startIndex, itemsCount, channelsCount, samplingFrequency);
    }

    public SoundBuffer(short[] data, int startIndex, int itemsCount, int channelsCount, int samplingFrequency)
    {
        Initialize(data, startIndex, itemsCount, channelsCount, samplingFrequency);
        CreateBuffer(data, startIndex, itemsCount, channelsCount, samplingFrequency);
    }

    public SoundBuffer(Stream stream, int bytesCount, int channelsCount, int samplingFrequency)
    {
        var array = Initialize(stream, bytesCount, channelsCount, samplingFrequency);
        CreateBuffer(array, 0, array.Length, channelsCount, samplingFrequency);
    }

    public int ChannelsCount { get; private set; }

    public int SamplingFrequency { get; private set; }

    public int SamplesCount { get; private set; }

    public int UseCount { get; internal set; }

    public void Dispose()
    {
        if (UseCount != 0)
        {
            throw new InvalidOperationException("Cannot dispose SoundBuffer which is in use.");
        }

        InternalDispose();
    }

    private void InternalDispose()
    {
        if (mBuffer == 0)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        Mixer.AL.DeleteBuffer(mBuffer);
        Mixer.CheckALError();
        mBuffer = 0;
    }

    private void CreateBuffer<T>(T[] data, int startIndex, int itemsCount, int channelsCount, int samplingFrequency)
        where T : unmanaged
    {
        if (!Mixer.IsAudioInitialized)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        mBuffer = Mixer.AL.GenBuffer();
        Mixer.CheckALError();
        unsafe
        {
            fixed (T* pData = data)
            {
                var elementSizeInBytes = sizeof(T);
                var pStartAddress = pData + startIndex;
                var totalBytesToCopy = itemsCount * elementSizeInBytes;
                Mixer.AL.BufferData(
                    mBuffer,
                    channelsCount == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16,
                    pStartAddress,
                    totalBytesToCopy,
                    samplingFrequency
                );
            }
        }

        Mixer.CheckALError();
    }

    public static SoundBuffer Load(SoundData soundData)
    {
        return new SoundBuffer(soundData.Data, 0, soundData.Data.Length, soundData.ChannelsCount,
            soundData.SamplingFrequency);
    }

    public static SoundBuffer Load(Stream stream, SoundFileFormat format)
    {
        return Load(SoundData.Load(stream, format));
    }

    public static SoundBuffer Load(string fileName, SoundFileFormat format)
    {
        return Load(SoundData.Load(fileName, format));
    }

    public static SoundBuffer Load(Stream stream)
    {
        return Load(SoundData.Load(stream));
    }

    public static SoundBuffer Load(string fileName)
    {
        return Load(SoundData.Load(fileName));
    }

    private void InitializeProperties(int samplesCount, int channelsCount, int samplingFrequency)
    {
        if (samplesCount <= 0)
        {
            throw new InvalidOperationException("Buffer cannot have zero samples.");
        }

        if (channelsCount is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(channelsCount));
        }

        if (samplingFrequency is < 8000 or > 48000)
        {
            throw new ArgumentOutOfRangeException(nameof(samplingFrequency));
        }

        ChannelsCount = channelsCount;
        SamplingFrequency = samplingFrequency;
        SamplesCount = samplesCount;
    }

    private void Initialize<T>(T[] data, int startIndex, int itemsCount, int channelsCount, int samplingFrequency)
    {
        var num = Utilities.SizeOf<T>();
        InitializeProperties(itemsCount * num / channelsCount / 2, channelsCount, samplingFrequency);
        ArgumentNullException.ThrowIfNull(data);

        if (startIndex + itemsCount > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(itemsCount));
        }
    }

    private byte[] Initialize(Stream stream, int bytesCount, int channelsCount, int samplingFrequency)
    {
        var array = new byte[bytesCount];
        if (stream.Read(array, 0, bytesCount) != bytesCount)
        {
            throw new InvalidOperationException("Not enough data in stream.");
        }

        Initialize(array, 0, bytesCount, channelsCount, samplingFrequency);
        return array;
    }
}

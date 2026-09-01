using Engine.FileStorage;

namespace Engine.Media;

public class SoundData
{
    public SoundData(int channelsCount, int samplingFrequency, int bytesCount)
    {
        if (channelsCount is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(channelsCount));
        }

        if (samplingFrequency is < 8000 or > 192000)
        {
            throw new ArgumentOutOfRangeException(nameof(samplingFrequency));
        }

        if (bytesCount < 0 || bytesCount % (2 * channelsCount) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesCount));
        }

        ChannelsCount = channelsCount;
        SamplingFrequency = samplingFrequency;
        Data = new short[bytesCount / 2];
    }

    public int ChannelsCount { get; private set; }

    public int SamplingFrequency { get; private set; }

    public short[] Data { get; private set; }

    public static SoundFileFormat DetermineFileFormat(string extension)
    {
        if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
        {
            return SoundFileFormat.Wav;
        }

        if (extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return SoundFileFormat.Ogg;
        }

        if (extension.Equals(".flac", StringComparison.OrdinalIgnoreCase))
        {
            return SoundFileFormat.Flac;
        }

        if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return SoundFileFormat.Mp3;
        }

        throw new InvalidOperationException("Unsupported sound file format.");
    }

    public static SoundFileFormat DetermineFileFormat(Stream stream)
    {
        if (Wav.IsWavStream(stream))
        {
            return SoundFileFormat.Wav;
        }

        if (Ogg.IsOggStream(stream))
        {
            return SoundFileFormat.Ogg;
        }

        if (Flac.IsFlacStream(stream))
        {
            return SoundFileFormat.Flac;
        }

        if (Mp3.IsMp3Stream(stream))
        {
            return SoundFileFormat.Mp3;
        }

        throw new InvalidOperationException("Unsupported sound file format.");
    }

    public static StreamingSource Stream(Stream stream, SoundFileFormat format)
    {
        switch (format)
        {
            case SoundFileFormat.Wav:
                return Wav.Stream(stream);
            case SoundFileFormat.Ogg:
                return Ogg.Stream(stream);
            case SoundFileFormat.Flac:
                return Flac.Stream(stream);
            case SoundFileFormat.Mp3:
                return Mp3.Stream(stream);
            default:
                throw new InvalidOperationException("Unsupported sound file format.");
        }
    }

    public static StreamingSource Stream(string fileName, SoundFileFormat format)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Stream(stream, format);
    }

    public static StreamingSource Stream(Stream stream)
    {
        var peekStream = new PeekStream(stream, 64);
        var format = DetermineFileFormat(peekStream.GetInitialBytesStream());
        return Stream(peekStream, format);
    }

    public static StreamingSource Stream(string fileName)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Stream(stream);
    }

    public static SoundData Load(Stream stream, SoundFileFormat format)
    {
        switch (format)
        {
            case SoundFileFormat.Wav:
                return Wav.Load(stream);
            case SoundFileFormat.Ogg:
                return Ogg.Load(stream);
            case SoundFileFormat.Flac:
                return Flac.Load(stream);
            case SoundFileFormat.Mp3:
                return Mp3.Load(stream);
            default:
                throw new InvalidOperationException("Unsupported sound file format.");
        }
    }

    public static SoundData Load(string fileName, SoundFileFormat format)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Load(stream, format);
    }

    public static SoundData Load(Stream stream)
    {
        var peekStream = new PeekStream(stream, 64);
        var format = DetermineFileFormat(peekStream.GetInitialBytesStream());
        return Load(peekStream, format);
    }

    public static SoundData Load(string fileName)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Load(stream);
    }

    public static void Save(SoundData soundData, Stream stream, SoundFileFormat format)
    {
        if (format != SoundFileFormat.Wav)
        {
            throw new InvalidOperationException("Unsupported sound file format.");
        }

        Wav.Save(soundData, stream);
    }

    public static void Save(SoundData soundData, string fileName, SoundFileFormat format)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Create);
        Save(soundData, stream, format);
    }

    public static void StereoToMono(SoundData soundData)
    {
        if (soundData.ChannelsCount != 2)
        {
            throw new InvalidOperationException("SoundData is not stereo.");
        }

        var array = new short[soundData.Data.Length / 2];
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = (short)((soundData.Data[2 * i] + soundData.Data[2 * i + 1]) / 2);
        }

        soundData.ChannelsCount = 1;
        soundData.Data = array;
    }
}

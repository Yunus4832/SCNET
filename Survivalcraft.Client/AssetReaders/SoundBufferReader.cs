using Engine.Audio;

namespace Game.AssetReaders;

public class SoundBufferReader : AssetReader
{
    public override string Type => "Engine.Audio.SoundBuffer";

    public override string[] DefaultSuffix => ["wav", "ogg"];

    public override object Get(AssetInfo[] contents)
    {
        return SoundBuffer.LoadDeferred(contents[0].CreateStreamFactory());
    }
}

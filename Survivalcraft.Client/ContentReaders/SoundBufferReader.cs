using Engine.Audio;

namespace Game.ContentReaders;

public class SoundBufferReader : IContentReader
{
    public override string Type => "Engine.Audio.SoundBuffer";

    public override string[] DefaultSuffix => ["wav", "ogg"];

    public override object Get(ContentInfo[] contents)
    {
        return SoundBuffer.Load(contents[0].Duplicate());
    }
}

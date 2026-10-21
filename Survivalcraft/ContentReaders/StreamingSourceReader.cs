using Engine.Media.Audio;

namespace Game.ContentReaders;

public class StreamingSourceReader : IContentReader
{
    public override string Type => "Engine.Media.Audio.StreamingSource";

    public override string[] DefaultSuffix => ["wav", "ogg"];

    public override object Get(ContentInfo[] contents)
    {
        return SoundData.Stream(contents[0].Duplicate());
    }
}

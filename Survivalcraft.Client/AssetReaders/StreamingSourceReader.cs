using Engine.Media.Audio;

namespace Game.AssetReaders;

public class StreamingSourceReader : AssetReader
{
    public override string Type => "Engine.Media.Audio.StreamingSource";

    public override string[] DefaultSuffix => ["wav", "ogg"];

    public override object Get(AssetInfo[] contents)
    {
        return SoundData.Stream(contents[0].Duplicate());
    }
}

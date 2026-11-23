using Engine.Media.Images;

namespace Game.AssetReaders;

public class ImageReader : AssetReader
{
    public override string Type => "Engine.Media.Images.Image";

    public override string[] DefaultSuffix => ["webp", "png"];

    public override object Get(AssetInfo[] contents)
    {
        return Image.Load(contents[0].Duplicate());
    }
}

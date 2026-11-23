using Engine.Graphics;
using Engine.Media.Images;

namespace Game.AssetReaders;

public class Texture2DReader : AssetReader
{
    public override string Type => "Engine.Graphics.Texture2D";

    public override string[] DefaultSuffix => ["webp", "png"];

    public override object Get(AssetInfo[] contents)
    {
        var image = Image.Load(contents[0].Duplicate());
        return Texture2D.Load(image);
    }
}

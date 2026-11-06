using Engine.Graphics;
using Engine.Media.Images;

namespace Game.ContentReaders;

public class Texture2DReader : IContentReader
{
    public override string Type => "Engine.Graphics.Texture2D";

    public override string[] DefaultSuffix => ["webp", "png"];

    public override object Get(ContentInfo[] contents)
    {
        var image = Image.Load(contents[0].Duplicate());
        return Texture2D.Load(image);
    }
}

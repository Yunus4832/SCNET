using Engine.Graphics;

namespace Game.ContentReaders;

public class Texture2DReader : IContentReader
{
    public override string Type => "Engine.Graphics.Texture2D";

    public override string[] DefaultSuffix => ["png", "jpg", "jpeg"];

    public override object Get(ContentInfo[] contents)
    {
        return Texture2D.Load(contents[0].Duplicate());
    }
}

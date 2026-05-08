using Engine.Media;

namespace Game.ContentReaders;

public class ImageReader : IContentReader
{
    public override string Type => "Engine.Media.Image";

    public override string[] DefaultSuffix => ["png", "jpeg", "jpg"];

    public override object Get(ContentInfo[] contents)
    {
        return Image.Load(contents[0].Duplicate());
    }
}

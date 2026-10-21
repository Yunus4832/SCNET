using Engine.Media.Images;

namespace Game.ContentReaders;

public class ImageReader : IContentReader
{
    public override string Type => "Engine.Media.Images.Image";

    public override string[] DefaultSuffix => ["png"];

    public override object Get(ContentInfo[] contents)
    {
        return Image.Load(contents[0].Duplicate());
    }
}

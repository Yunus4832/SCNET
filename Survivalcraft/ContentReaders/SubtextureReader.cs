using Engine.Graphics;

namespace Game.ContentReaders;

public class SubtextureReader : IContentReader
{
    public override string[] DefaultSuffix => ["png", "txt"];

    public override string Type => "Game.Subtexture";

    public override object Get(ContentInfo[] contents)
    {
        return contents[0].ContentPath.Contains("Textures/Atlas/")
            ? TextureAtlasManager.GetSubtexture(contents[0].ContentPath)
            : new Subtexture(ContentManager.Get<Texture2D>(contents[0].ContentPath), Vector2.Zero, Vector2.One);
    }
}

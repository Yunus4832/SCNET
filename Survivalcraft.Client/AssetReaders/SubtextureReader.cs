using Engine.Graphics;

namespace Game.AssetReaders;

public class SubtextureReader : AssetReader
{
    public override string[] DefaultSuffix => ["webp", "png", "txt"];

    public override string Type => "Engine.Graphics.Subtexture";

    public override object Get(AssetInfo[] contents)
    {
        return contents[0].AssetPath.Contains("Textures/Atlas/")
            ? TextureAtlasManager.GetSubtexture(contents[0].AssetPath)
            : new Subtexture(AssetManager.Get<Texture2D>(contents[0].AssetPath), Vector2.Zero, Vector2.One);
    }
}

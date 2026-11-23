namespace Game.AssetReaders;

internal static class BuiltInAssetReaders
{
    public static void Register()
    {
        AssetManager.RegisterReader(new BitmapFontReader());
        AssetManager.RegisterReader(new GltfModelReader());
        AssetManager.RegisterReader(new ImageReader());
        AssetManager.RegisterReader(new JsonArrayReader());
        AssetManager.RegisterReader(new JsonObjectReader());
        AssetManager.RegisterReader(new ShaderReader());
        AssetManager.RegisterReader(new SoundBufferReader());
        AssetManager.RegisterReader(new StreamingSourceReader());
        AssetManager.RegisterReader(new StringReader());
        AssetManager.RegisterReader(new SubtextureReader());
        AssetManager.RegisterReader(new Texture2DReader());
        AssetManager.RegisterReader(new XmlReader());
    }
}

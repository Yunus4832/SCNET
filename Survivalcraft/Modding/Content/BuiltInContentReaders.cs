using Game.ContentReaders;

using StringReader = Game.ContentReaders.StringReader;

namespace Game.Modding.Content;

internal static class BuiltInContentReaders
{
    public static void Register()
    {
        ContentManager.RegisterReader(new BitmapFontReader());
        ContentManager.RegisterReader(new DaeModelReader());
        ContentManager.RegisterReader(new ImageReader());
        ContentManager.RegisterReader(new JsonArrayReader());
        ContentManager.RegisterReader(new JsonObjectReader());
        ContentManager.RegisterReader(new MtllibStructReader());
        ContentManager.RegisterReader(new ObjModelReader());
        ContentManager.RegisterReader(new ShaderReader());
        ContentManager.RegisterReader(new SoundBufferReader());
        ContentManager.RegisterReader(new StreamingSourceReader());
        ContentManager.RegisterReader(new StringReader());
        ContentManager.RegisterReader(new SubtextureReader());
        ContentManager.RegisterReader(new Texture2DReader());
        ContentManager.RegisterReader(new XmlReader());
    }
}

using Engine.Graphics;

namespace Game.ContentReaders;

public class BitmapFontReader : IContentReader
{
    public override string Type => "Engine.Graphics.BitmapFont";

    public override string[] DefaultSuffix => ["lst", "webp"];

    public override object Get(ContentInfo[] contents)
    {
        return contents.Length != 2
            ? throw new Exception("not matches content count")
            : BitmapFont.Initialize(contents[1].Duplicate(), contents[0].Duplicate(), new Vector2(0.0f, -3.0f));
    }
}

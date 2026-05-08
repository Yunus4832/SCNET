namespace Game.ContentReaders;

public class JsonModelReader : IContentReader
{
    public override string Type => "Game.JsonModel";

    public override string[] DefaultSuffix => ["json"];

    public override object Get(ContentInfo[] contents)
    {
        return ModManager.JsonModelReader.Load(contents[0].Duplicate());
    }
}

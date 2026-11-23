namespace Game.AssetReaders;

public class StringReader : AssetReader
{
    public override string Type => "System.String";

    public override string[] DefaultSuffix => ["txt"];

    public override object Get(AssetInfo[] contents)
    {
        return new StreamReader(contents[0].Duplicate()).ReadToEnd();
    }
}

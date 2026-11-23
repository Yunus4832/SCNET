using System.Xml.Linq;

namespace Game.AssetReaders;

public class XmlReader : AssetReader
{
    public override string Type => "System.Xml.Linq.XElement";

    public override string[] DefaultSuffix => ["xml", "xdb"];

    public override object Get(AssetInfo[] contents)
    {
        return XElement.Load(contents[0].Duplicate());
    }
}

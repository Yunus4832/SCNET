using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

namespace Game.Network.Snapshots;

public static class ValuesDictionaryXmlCodec
{
    public static string Serialize(ValuesDictionary values)
    {
        var element = new XElement("Values");
        values.Save(element);
        return XmlUtils.SaveXmlToString(element, true);
    }

    public static ValuesDictionary Deserialize(string xml)
    {
        var values = new ValuesDictionary();
        values.ApplyOverrides(XmlUtils.LoadXmlFromString(xml, true));
        return values;
    }
}

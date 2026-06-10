using System.Xml.Linq;

namespace Game;

public static class AppConfigStore
{
    public static readonly Dictionary<string, string> Values = new();

    public static bool IsLoaded { get; private set; }

    public static void ReadFromXml(XElement root)
    {
        var config = root.Element("Configs");
        if (config == null)
        {
            return;
        }

        foreach (var attribute in config.Attributes())
        {
            if (!Values.ContainsKey(attribute.Name.LocalName))
            {
                Set(attribute.Name.LocalName, attribute.Value);
            }
        }

        IsLoaded = true;
    }

    public static void WriteToXml(XElement root)
    {
        var element = new XElement("Configs");
        foreach (var config in Values)
        {
            element.SetAttributeValue(config.Key, config.Value);
        }

        root.Add(element);
    }

    public static void Set(string key, string value)
    {
        Values[key] = value;
    }
}

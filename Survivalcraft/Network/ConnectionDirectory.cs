using System.Xml.Linq;

using EntitySystem.XmlUtilities;

using static Game.Screens.NetPlayScreen;

namespace Game.Network;

public static class ConnectionDirectory
{
    public static readonly List<Connect> Saved = [];

    public static readonly List<Connect> Collected = [];

    public static readonly List<Connect> Discovered = [];

    public static void ReadFromXml(XElement root)
    {
        Saved.Clear();
        Collected.Clear();

        var savedConnects = root.Element("SaveConnects");
        if (savedConnects != null)
        {
            foreach (var element in savedConnects.Elements())
            {
                var ip = element.Attribute("IP")?.Value ?? throw new InvalidOperationException("IP is null");
                var name = element.Attribute("Name")?.Value ?? string.Empty;
                Saved.Add(new Connect { IP = ip, Name = name });
            }
        }

        var collectedConnects = root.Element("CollectConnects");
        if (collectedConnects == null)
        {
            return;
        }

        foreach (var element in collectedConnects.Elements())
        {
            var ip = element.Attribute("IP")?.Value ?? throw new InvalidOperationException("IP is null");
            var name = element.Attribute("Name")?.Value ?? string.Empty;
            Collected.Add(new Connect { IP = ip, Name = name });
        }
    }

    public static void WriteToXml(XElement root)
    {
        var savedConnects = new XElement("SaveConnects");
        foreach (var connect in Saved)
        {
            var node = XmlUtils.AddElement(savedConnects, "SaveConnects");
            node.SetAttributeValue("IP", connect.IP);
            node.SetAttributeValue("Name", connect.Name);
        }

        root.Add(savedConnects);

        var collectedConnects = new XElement("CollectConnects");
        foreach (var connect in Collected)
        {
            var node = XmlUtils.AddElement(collectedConnects, "CollectConnects");
            node.SetAttributeValue("IP", connect.IP);
            node.SetAttributeValue("Name", connect.Name);
        }

        root.Add(collectedConnects);
    }
}

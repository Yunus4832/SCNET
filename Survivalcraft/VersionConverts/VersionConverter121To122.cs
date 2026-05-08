using System.Xml.Linq;
using EntitySystem.XmlUtilities;

namespace Game.VersionConverts;

public class VersionConverter121To122 : VersionConverter
{
    public override string SourceVersion => "1.21";

    public override string TargetVersion => "1.22";

    public override void ConvertProjectXml(XElement projectNode)
    {
        XmlUtils.SetAttributeValue(projectNode, "Version", TargetVersion);
        foreach (var item in projectNode.Element("Subsystems")?.Elements() ?? [])
        foreach (var item2 in from e in item.Elements("Values")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "CreatureSpawn"
                 select e)
        {
            XmlUtils.SetAttributeValue(item2, "Name", "Spawn");
            foreach (var item3 in from e in item2.Elements("Value")
                     where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "CreaturesData"
                     select e)
            {
                XmlUtils.SetAttributeValue(item3, "Name", "SpawnsData");
            }

            foreach (var item4 in from e in item2.Elements("Value")
                     where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "CreaturesGenerated"
                     select e)
            {
                XmlUtils.SetAttributeValue(item4, "Name", "IsSpawned");
            }
        }
    }

    public override void ConvertWorld(string directoryName)
    {
        var path = Storage.CombinePaths(directoryName, "Project.xml");
        XElement xElement;
        using (var stream = Storage.OpenFile(path, OpenFileMode.Read))
        {
            xElement = XmlUtils.LoadXmlFromStream(stream, null, true);
        }

        ConvertProjectXml(xElement);
        using (var stream2 = Storage.OpenFile(path, OpenFileMode.Create))
        {
            XmlUtils.SaveXmlToStream(xElement, stream2, null, true);
        }
    }
}

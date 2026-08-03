using System.Xml.Linq;

using EntitySystem.XmlUtilities;

namespace WorldUpgradeTool.VersionConverts;

public class VersionConverter23To24 : VersionConverter
{
    public override string SourceVersion => "2.3";

    public override string TargetVersion => "2.4";

    public override void ConvertProjectXml(XElement projectNode)
    {
        XmlUtils.SetAttributeValue(projectNode, "Version", TargetVersion);
        foreach (var item in from e in projectNode.Element("Subsystems")?.Elements()
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "GameInfo"
                 select e)
        {
            var xElement = XmlUtils.AddElement(item, "Value");
            xElement.SetAttributeValue("Name", "AreSeasonsChanging");
            xElement.SetAttributeValue("Type", "bool");
            xElement.SetAttributeValue("Value", "false");
            var xElement2 = XmlUtils.AddElement(item, "Value");
            xElement2.SetAttributeValue("Name", "TimeOfYear");
            xElement2.SetAttributeValue("Type", "float");
            xElement2.SetAttributeValue("Value",
                IntervalUtils.Midpoint(SubsystemSeasons.SummerStart, SubsystemSeasons.AutumnStart));
        }
    }

    public override void ConvertWorld(string directoryName)
    {
        var path = Storage.CombinePaths(directoryName, "Project.xml");
        XElement projectNode;
        using (var stream = Storage.OpenFile(path, OpenFileMode.Read))
        {
            projectNode = XmlUtils.LoadXmlFromStream(stream, null, true);
        }

        ConvertProjectXml(projectNode);
        using (var stream = Storage.OpenFile(path, OpenFileMode.Create))
        {
            XmlUtils.SaveXmlToStream(projectNode, stream, null, true);
        }
    }
}

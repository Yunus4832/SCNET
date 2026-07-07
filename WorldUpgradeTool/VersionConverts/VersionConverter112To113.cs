using System.Xml.Linq;

using EntitySystem.XmlUtilities;

namespace Game.VersionConverts;

public class VersionConverter112To113 : VersionConverter
{
    public override string SourceVersion => "1.12";

    public override string TargetVersion => "1.13";

    public override void ConvertProjectXml(XElement projectNode)
    {
        XmlUtils.SetAttributeValue(projectNode, "Version", TargetVersion);
        ProcessNode(projectNode);
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

    public void ProcessNode(XElement node)
    {
        foreach (var item in node.Attributes())
        {
            ProcessAttribute(item);
        }

        foreach (var item2 in node.Elements())
        {
            ProcessNode(item2);
        }
    }

    public void ProcessAttribute(XAttribute attribute)
    {
        if (attribute.Name == "Value" && attribute.Value == "Dangerous")
        {
            attribute.Value = "Normal";
        }
    }
}

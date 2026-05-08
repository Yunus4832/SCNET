using System.Xml.Linq;
using EntitySystem.XmlUtilities;

namespace Game.VersionConverts;

public class VersionConverter15To16 : VersionConverter
{
    public override string SourceVersion => "1.5";

    public override string TargetVersion => "1.6";

    public override void ConvertProjectXml(XElement projectNode)
    {
        XmlUtils.SetAttributeValue(projectNode, "Version", TargetVersion);
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

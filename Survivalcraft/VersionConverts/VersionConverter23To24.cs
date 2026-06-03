using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

namespace Game.VersionConverts;

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

    public static string ConvertProjectJson(string json)
    {
        // 将字符串解析为 JsonObject
        var jsonObject = JsonSerializer.Deserialize<JsonObject>(json);

        // 修改 Version 到 "2.4"
        if (jsonObject is null || !jsonObject.TryGetPropertyValue("Version", out var versionNode))
        {
            throw new InvalidOperationException("Version info not found");
        }

        if (versionNode is JsonArray versionArray)
        {
            versionArray[0] = "2.4";
        }

        if (jsonObject.TryGetPropertyValue("Subsystem", out var subsystemNode) ||
            subsystemNode is not JsonObject subsystemObject ||
            !subsystemObject.TryGetPropertyValue("GameInfo", out var gameInfoNode))
        {
            throw new InvalidOperationException("GameInfo not found");
        }

        // 在 GameInfo 中加入 TimeOfYear
        if (gameInfoNode is JsonObject gameInfoObject)
        {
            gameInfoObject["TimeOfYear"] = new JsonArray
            {
                "float",
                IntervalUtils.Midpoint(SubsystemSeasons.SummerStart, SubsystemSeasons.AutumnStart)
            };

            // 在 GameInfo 中加入 AreSeasonsChanging
            gameInfoObject["AreSeasonsChanging"] = new JsonArray { "bool", true };
        }

        // 将修改后的 JObject 转换回字符串
        return jsonObject.ToString();
    }

    public override void ConvertWorld(string directoryName)
    {
        var path = Storage.GetSystemPath(Storage.CombinePaths(directoryName, "Project.json"));
        var json = File.ReadAllText(path);
        var convertedJson = ConvertProjectJson(json);
        File.WriteAllText(path, convertedJson);
    }
}

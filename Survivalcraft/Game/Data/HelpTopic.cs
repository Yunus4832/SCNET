using System.Text.Json.Nodes;

namespace Game;

public class HelpTopic
{
    public string Name = string.Empty;

    public string Key = string.Empty;

    public string Title => $"[Help:{Key}:Title]";

    public string Text => $"[Help:{Key}:value]";

    public static HelpTopic Create(string key, JsonObject item)
    {
        return new HelpTopic
        {
            Key = key,
            Name = item.ContainsKey("Name") ? item["Name"]?.ToString() ?? string.Empty : string.Empty
        };
    }
}

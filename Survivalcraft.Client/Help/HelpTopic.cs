using System.Text.Json.Nodes;

namespace Game.Help;

public class HelpTopic
{
    // 固定内置主题的阅读顺序。键同时用于本地化和直达链接。
    private static readonly string[] _readingOrder =
    [
        // 基础操作与物品
        "7", "6", "5", "20", "21", "22", "23", "24", "25", "28", "43", "44", "8", "19", "38",
        // 生存与角色
        "9", "12", "17", "13", "14", "15", "16", "10", "11", "36", "37", "18", "39",
        // 采集、制作与建造
        "29", "26", "27", "30", "31", "32", "34", "33", "35",
        // 联机与内容
        "47", "48", "49", "50", "54", "46", "53", "42"
    ];

    internal static int DisplayOrder(string key)
    {
        var index = Array.IndexOf(_readingOrder, key);
        if (index >= 0) { return index; }

        return key switch { "52" => int.MaxValue - 1, "51" => int.MaxValue, _ => _readingOrder.Length };
    }

    public string Name = string.Empty;

    public string Key = string.Empty;

    public string Title => $"[Help:{Key}:Title]";

    public string Text => Name == "About" ? AboutHelpTopic.GetText() : $"[Help:{Key}:value]";

    public static HelpTopic Create(string key, JsonObject item)
    {
        return new HelpTopic
        {
            Key = key,
            Name = item.ContainsKey("Name") ? item["Name"]?.ToString() ?? string.Empty : string.Empty
        };
    }
}

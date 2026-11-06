using System.Xml.Linq;

namespace Game.Startup;

/// <summary>Serializes host startup settings without accessing a window, instance paths or sessions.</summary>
public static class RunningSettingSerializer
{
    public static RunningSetting Load(Stream stream)
    {
        var root = XElement.Load(stream);
        var settings = new RunningSetting();
        settings.LogLevel = ParseEnum(root, nameof(RunningSetting.LogLevel), settings.LogLevel);
        settings.WindowMode = ParseEnum(root, nameof(RunningSetting.WindowMode), settings.WindowMode);
        settings.WindowWidth = ParseWindowSize(root.Attribute(nameof(RunningSetting.WindowWidth))?.Value);
        settings.WindowHeight = ParseWindowSize(root.Attribute(nameof(RunningSetting.WindowHeight))?.Value);
        settings.DefaultSessionId = NormalizeSessionId(root.Attribute(nameof(RunningSetting.DefaultSessionId))?.Value);
        settings.PendingSessionId = NormalizeSessionId(root.Attribute(nameof(RunningSetting.PendingSessionId))?.Value);
        settings.RemainingArgs = root.Element(nameof(RunningSetting.RemainingArgs))?
                                     .Elements("Arg")
                                     .Select(element => element.Value)
                                     .ToArray() ??
                                 [];
        return settings;
    }

    public static void Save(Stream stream, RunningSetting settings)
    {
        var root = new XElement(
            "RunningSetting",
            new XAttribute(nameof(RunningSetting.LogLevel), settings.LogLevel),
            new XAttribute(nameof(RunningSetting.WindowMode), settings.WindowMode),
            new XAttribute(nameof(RunningSetting.WindowWidth), settings.WindowWidth),
            new XAttribute(nameof(RunningSetting.WindowHeight), settings.WindowHeight),
            new XAttribute(nameof(RunningSetting.DefaultSessionId), NormalizeSessionId(settings.DefaultSessionId)),
            new XAttribute(nameof(RunningSetting.PendingSessionId), NormalizeSessionId(settings.PendingSessionId)),
            new XElement(
                nameof(RunningSetting.RemainingArgs),
                settings.RemainingArgs.Select(arg => new XElement("Arg", arg))
            )
        );
        root.Save(stream);
    }

    private static T ParseEnum<T>(XElement root, string attribute, T fallback) where T : struct, Enum =>
        Enum.TryParse(root.Attribute(attribute)?.Value, true, out T value) ? value : fallback;

    private static int ParseWindowSize(string? value) =>
        int.TryParse(value, out var size) ? Math.Max(size, 0) : 0;

    private static string NormalizeSessionId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

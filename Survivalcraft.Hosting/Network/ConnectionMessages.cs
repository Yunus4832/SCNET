namespace Game.Network;

public static class ConnectionMessages
{
    public static LocalizedText Create(string key, string fallback, params string[] arguments)
    {
        var template = new LocalizedText("Connection", key, fallback);
        return arguments.Length == 0
            ? template
            : LocalizedText.Format(template, arguments.Select(LocalizedText.Literal).ToArray());
    }
}

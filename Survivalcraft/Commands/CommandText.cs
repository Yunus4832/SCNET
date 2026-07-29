using Game.Localization;

namespace Game.Commands;

public static class CommandText
{
    public static string Get(string key, string fallback, params string[] arguments)
    {
        return LocalizationText.Get("Commands", key, fallback, arguments);
    }

    public static string Resolve(CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return string.IsNullOrWhiteSpace(result.MessageKey)
            ? result.Message
            : Get(
                result.MessageKey,
                result.Message,
                result.MessageArguments?.ToArray() ?? []);
    }
}

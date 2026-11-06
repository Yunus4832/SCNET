using Game.Localization;

namespace Game.Commands;

public static class CommandText
{
    public static string Get(string key, string fallback, params string[] arguments)
    {
        return ClientLanguage.Resolver.Get("Commands", key, fallback, arguments);
    }

    public static string Resolve(CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return ClientLanguage.Resolver.Resolve(result.Message);
    }
}

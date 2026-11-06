namespace Game.Teams;

public sealed record TeamMessage(string Key, string Fallback, IReadOnlyList<string> Arguments)
{
    public static TeamMessage Create(string key, string fallback, params string[] arguments)
    {
        return new TeamMessage(key, fallback, arguments);
    }
}

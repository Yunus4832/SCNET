namespace Game.Commands;

public sealed record CommandArgument(
    string Name,
    CommandArgumentKind Kind = CommandArgumentKind.String,
    IReadOnlyList<string>? Choices = null,
    Func<CommandSuggestionContext, IEnumerable<CommandArgumentSuggestion>>? SuggestionProvider = null
) : CommandSegment
{
    public string Name { get; } = Validate(Name);

    private static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Any(char.IsWhiteSpace)
            ? throw new ArgumentException("Command argument names cannot contain whitespace.", nameof(value))
            : value;
    }
}

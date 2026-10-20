namespace Game.Commands;

public sealed record CommandLiteral(string Value, LocalizedText? Description = null) : CommandSegment
{
    public string Value { get; } = Validate(Value);

    private static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Any(char.IsWhiteSpace)
            ? throw new ArgumentException("Command literals cannot contain whitespace.", nameof(value))
            : value;
    }
}

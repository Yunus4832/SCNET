namespace Game.Modding;

public readonly record struct ModId
{
    public string Value { get; }

    public ModId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!IsValid(value))
        {
            throw new ArgumentException($"Invalid mod id \"{value}\".", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;

    private static bool IsValid(string value)
    {
        return value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    }
}

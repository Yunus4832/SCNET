namespace Game.Protocol;

public readonly record struct ResourceId
{
    public ModId Namespace { get; }

    public string Path { get; }

    public ResourceId(ModId @namespace, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' or '/')))
        {
            throw new ArgumentException($"Invalid resource path \"{path}\".", nameof(path));
        }

        Namespace = @namespace;
        Path = path;
    }

    public static ResourceId Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var separator = text.IndexOf(':');
        if (separator <= 0 || separator == text.Length - 1)
        {
            throw new ArgumentException($"Invalid resource id \"{text}\".", nameof(text));
        }

        return new ResourceId(new ModId(text[..separator]), text[(separator + 1)..]);
    }

    public override string ToString() => $"{Namespace}:{Path}";
}

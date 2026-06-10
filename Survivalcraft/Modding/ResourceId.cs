namespace Game.Modding;

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

    public override string ToString() => $"{Namespace}:{Path}";
}

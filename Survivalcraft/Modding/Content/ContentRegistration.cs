namespace Game.Modding.Content;

public sealed class ContentRegistration
{
    private readonly byte[] _bytes;

    public ContentRegistration(string relativePath, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(bytes);
        RelativePath = relativePath;
        _bytes = bytes.ToArray();
    }

    public string RelativePath { get; }

    public MemoryStream OpenRead() => new(_bytes, writable: false);

    public byte[] CopyBytes() => _bytes.ToArray();
}

public static class ContentExtensions
{
    public const string RegistryName = "content.assets";

    public static IDisposable RegisterContent(
        this IModExtensions extensions,
        ResourceId id,
        string relativePath,
        byte[] bytes)
    {
        return extensions.Register(RegistryName, id, new ContentRegistration(relativePath, bytes));
    }
}

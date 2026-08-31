namespace Content.Packaging.Payloads;

public sealed class ContentPayloadCodecRegistry
{
    private readonly IReadOnlyDictionary<ContentPackageType, IContentPayloadCodec> _codecs;

    public ContentPayloadCodecRegistry(IEnumerable<IContentPayloadCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        try
        {
            _codecs = codecs.ToDictionary(codec => codec.Type);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Only one payload codec can be registered for each content type.",
                nameof(codecs), exception);
        }
    }

    public static ContentPayloadCodecRegistry Default { get; } = new([
        new ModPayloadCodec(),
        new WorldPayloadCodec(),
        new BlocksTexturePayloadCodec(),
        new CharacterSkinPayloadCodec(),
        new FurniturePackPayloadCodec()
    ]);

    public IContentPayloadCodec Get(ContentPackageType type) =>
        _codecs.TryGetValue(type, out var codec)
            ? codec
            : throw new ContentPackageException($"No payload codec is registered for content type {type}.");
}

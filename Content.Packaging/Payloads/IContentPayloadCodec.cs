namespace Content.Packaging.Payloads;

public sealed record ContentPayloadValidationContext(
    ContentPackageManifest Manifest,
    IReadOnlySet<string> Paths,
    Func<string, Stream> OpenEntry);

public interface IContentPayloadCodec
{
    ContentPackageType Type { get; }

    void Validate(ContentPayloadValidationContext context);
}

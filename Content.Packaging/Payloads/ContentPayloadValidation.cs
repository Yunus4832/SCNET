namespace Content.Packaging.Payloads;

internal static class ContentPayloadValidation
{
    public static void ValidateEnvelope(
        ContentPayloadValidationContext context,
        string format,
        string entry,
        string mediaType,
        IEnumerable<string> metadataProperties,
        bool allowAdditionalPayload = false)
    {
        var manifest = context.Manifest;
        if (manifest.Payload.Format != format || manifest.Payload.Entry != entry ||
            manifest.Payload.MediaType != mediaType)
        {
            throw new ContentPackageException("manifest.payload does not match its content type.");
        }

        ContentPackageManifest.EnsureExactProperties(manifest.Metadata, metadataProperties, "manifest.metadata");
        if (!context.Paths.Contains(entry))
        {
            throw new ContentPackageException("manifest.payload.entry does not exist in the package.");
        }

        if (!allowAdditionalPayload && !context.Paths.SetEquals(["manifest.json", entry]))
        {
            throw new ContentPackageException(
                "Package contains payload files that are not allowed for its content type.");
        }
    }

    public static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
}

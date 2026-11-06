using System.Text.Json;

namespace Content.Packaging.Payloads;

public sealed class ModPayloadCodec : IContentPayloadCodec
{
    public ContentPackageType Type => ContentPackageType.Mod;

    public void Validate(ContentPayloadValidationContext context)
    {
        var manifest = context.Manifest;
        ContentPayloadValidation.ValidateEnvelope(
            context,
            "scnet.mod-v1",
            "payload/mod.json",
            "application/json",
            ["entrypoint", "clientContentEntrypoint", "dependencies"],
            allowAdditionalPayload: true
        );
        if (!manifest.Metadata.TryGetProperty("entrypoint", out var entrypoint))
        {
            throw new ContentPackageException("Mod metadata.entrypoint is required; data-only packages use null.");
        }

        var dependencies =
            ContentPackageManifest.GetRequiredProperty(manifest.Metadata, "dependencies", "manifest.metadata");
        if (entrypoint.ValueKind is not (JsonValueKind.String or JsonValueKind.Null) ||
            entrypoint.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(entrypoint.GetString()) ||
            dependencies.ValueKind != JsonValueKind.Array)
        {
            throw new ContentPackageException("Mod metadata.entrypoint and metadata.dependencies are invalid.");
        }

        if (manifest.Metadata.TryGetProperty("clientContentEntrypoint", out var clientContentEntrypoint) &&
            (clientContentEntrypoint.ValueKind is not (JsonValueKind.String or JsonValueKind.Null) ||
             clientContentEntrypoint.ValueKind == JsonValueKind.String &&
             string.IsNullOrWhiteSpace(clientContentEntrypoint.GetString())))
        {
            throw new ContentPackageException(
                "Mod metadata.clientContentEntrypoint must be a non-empty string or null."
            );
        }

        var dependencyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dependency in dependencies.EnumerateArray())
        {
            ValidateDependency(dependency, manifest.Identifier, dependencyIds);
        }

        var hasContribution = context.Paths.Any(path =>
            path.StartsWith("payload/data/", StringComparison.Ordinal) ||
            path.StartsWith($"payload/assets/{manifest.Identifier}/", StringComparison.Ordinal)
        );
        if (entrypoint.ValueKind == JsonValueKind.Null &&
            (clientContentEntrypoint.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) &&
            !hasContribution)
        {
            throw new ContentPackageException("Mod must define an applicable entrypoint or contribution.");
        }

        try
        {
            using var stream = context.OpenEntry("payload/mod.json");
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ContentPackageException("Mod payload/mod.json must be an object.");
            }

            ContentPackageManifest.EnsureExactProperties(
                document.RootElement,
                ["formatVersion"],
                "Mod payload/mod.json"
            );
            if (ContentPackageManifest.GetRequiredInt32(
                    document.RootElement,
                    "formatVersion",
                    "Mod payload/mod.json"
                ) !=
                1)
            {
                throw new ContentPackageException("Mod payload/mod.json formatVersion must be 1.");
            }
        }
        catch (JsonException exception)
        {
            throw new ContentPackageException("Mod payload/mod.json is invalid JSON.", exception);
        }

        foreach (var path in context.Paths)
        {
            if (IsAllowedPath(path, manifest.Identifier))
            {
                continue;
            }

            throw new ContentPackageException($"Mod payload path '{path}' is invalid.");
        }
    }

    private static void ValidateDependency(JsonElement dependency, string owner, ISet<string> ids)
    {
        if (dependency.ValueKind != JsonValueKind.Object)
        {
            throw new ContentPackageException("Mod dependencies must contain objects.");
        }

        ContentPackageManifest.EnsureExactProperties(
            dependency,
            ["identifier", "minimumVersion", "optional"],
            "manifest.metadata.dependencies[]"
        );
        var identifier = ContentPackageManifest.GetRequiredString(
            dependency,
            "identifier",
            "manifest.metadata.dependencies[]"
        );
        var minimumVersion = ContentPackageManifest.GetRequiredString(
            dependency,
            "minimumVersion",
            "manifest.metadata.dependencies[]"
        );
        var optional = ContentPackageManifest.GetRequiredProperty(
            dependency,
            "optional",
            "manifest.metadata.dependencies[]"
        );
        if (identifier == owner ||
            !IsValidModIdentifier(identifier) ||
            !ids.Add(identifier) ||
            !SemanticVersion.TryParse(minimumVersion, out _) ||
            optional.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new ContentPackageException("Mod dependency is invalid.");
        }
    }

    private static bool IsAllowedPath(string path, string identifier) =>
        path is "manifest.json" or "payload/mod.json" ||
        path.StartsWith("payload/data/", StringComparison.Ordinal) ||
        path.StartsWith($"payload/assets/{identifier}/", StringComparison.Ordinal) ||
        path.StartsWith("payload/assemblies/", StringComparison.Ordinal) &&
        path.Count(character => character == '/') == 2 &&
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidModIdentifier(string identifier) =>
        identifier.Length is > 0 and <= 120 &&
        identifier.Split('.').All(segment =>
            segment.Length > 0 &&
            char.IsAsciiLetterOrDigit(segment[0]) &&
            segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
        );
}

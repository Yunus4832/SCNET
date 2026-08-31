using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Content.Packaging;

public sealed record ContentPackageManifest(
    int FormatVersion,
    ContentPackageType Type,
    string Identifier,
    string Name,
    string Version,
    ContentPackagePayload Payload,
    JsonElement Metadata)
{
    private static readonly Regex _uuidPattern = new(
        "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex _modIdPattern = new(
        "^[a-z0-9][a-z0-9_-]*(?:\\.[a-z0-9][a-z0-9_-]*)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public const int CurrentFormatVersion = 1;

    public static ContentPackageManifest Parse(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8Json.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ContentPackageException("manifest.json must contain an object.");
            }

            EnsureExactProperties(root,
                ["formatVersion", "type", "identifier", "name", "version", "payload", "metadata"], "manifest");
            var formatVersion = GetRequiredInt32(root, "formatVersion", "manifest");
            if (formatVersion != CurrentFormatVersion)
            {
                throw new ContentPackageException($"Unsupported content package format version {formatVersion}.");
            }

            var type = ParseType(GetRequiredString(root, "type", "manifest"));
            var identifier = GetRequiredString(root, "identifier", "manifest");
            var name = GetRequiredString(root, "name", "manifest");
            var version = GetRequiredString(root, "version", "manifest");
            ValidateIdentity(type, identifier, name, version);
            var payload = ContentPackagePayload.Parse(GetRequiredProperty(root, "payload", "manifest"));
            var metadata = GetRequiredProperty(root, "metadata", "manifest");
            if (metadata.ValueKind != JsonValueKind.Object)
            {
                throw new ContentPackageException("manifest.metadata must be an object.");
            }

            return new ContentPackageManifest(formatVersion, type, identifier, name, version, payload,
                metadata.Clone());
        }
        catch (JsonException exception)
        {
            throw new ContentPackageException("manifest.json is not valid UTF-8 JSON.", exception);
        }
    }

    internal static JsonElement GetRequiredProperty(JsonElement objectElement, string name, string context)
    {
        if (!objectElement.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            throw new ContentPackageException($"{context}.{name} is required.");
        }

        return value;
    }

    internal static string GetRequiredString(JsonElement objectElement, string name, string context)
    {
        var value = GetRequiredProperty(objectElement, name, context);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
        {
            throw new ContentPackageException($"{context}.{name} must be a non-empty string.");
        }

        return value.GetString()!;
    }

    internal static int GetRequiredInt32(JsonElement objectElement, string name, string context)
    {
        var value = GetRequiredProperty(objectElement, name, context);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            throw new ContentPackageException($"{context}.{name} must be a 32-bit integer.");
        }

        return result;
    }

    internal static void EnsureExactProperties(JsonElement objectElement, IEnumerable<string> names, string context)
    {
        var allowedNames = names.ToHashSet(StringComparer.Ordinal);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in objectElement.EnumerateObject())
        {
            if (!seenNames.Add(property.Name))
            {
                throw new ContentPackageException($"{context} contains duplicate property '{property.Name}'.");
            }
            if (!allowedNames.Contains(property.Name))
            {
                throw new ContentPackageException($"{context} contains unknown property '{property.Name}'.");
            }
        }
    }

    private static ContentPackageType ParseType(string value)
    {
        return value switch
        {
            "mod" => ContentPackageType.Mod,
            "world" => ContentPackageType.World,
            "blocksTexture" => ContentPackageType.BlocksTexture,
            "characterSkin" => ContentPackageType.CharacterSkin,
            "furniturePack" => ContentPackageType.FurniturePack,
            _ => throw new ContentPackageException($"Unsupported content package type '{value}'.")
        };
    }

    private static void ValidateIdentity(ContentPackageType type, string identifier, string name, string version)
    {
        if (identifier.Length > 120 ||
            (type == ContentPackageType.Mod ? !_modIdPattern.IsMatch(identifier) : !_uuidPattern.IsMatch(identifier)))
        {
            throw new ContentPackageException($"manifest.identifier is invalid for content type {type}.");
        }

        var scalarCount = name.EnumerateRunes().Count();
        if (name.Trim() != name || !name.IsNormalized(NormalizationForm.FormC) ||
            scalarCount is < 1 or > 120 || name.EnumerateRunes().Any(Rune.IsControl))
        {
            throw new ContentPackageException(
                "manifest.name must be trimmed, 1-120 characters and contain no control characters.");
        }

        if (!SemanticVersion.TryParse(version, out _))
        {
            throw new ContentPackageException("manifest.version must be SemVer 2.0.0 without build metadata.");
        }
    }

}

public sealed record ContentPackagePayload(string Format, string Entry, string MediaType)
{
    internal static ContentPackagePayload Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ContentPackageException("manifest.payload must be an object.");
        }

        ContentPackageManifest.EnsureExactProperties(element, ["format", "entry", "mediaType"], "manifest.payload");
        var result = new ContentPackagePayload(
            ContentPackageManifest.GetRequiredString(element, "format", "manifest.payload"),
            ContentPackageManifest.GetRequiredString(element, "entry", "manifest.payload"),
            ContentPackageManifest.GetRequiredString(element, "mediaType", "manifest.payload"));
        if (!result.Entry.StartsWith("payload/", StringComparison.Ordinal))
        {
            throw new ContentPackageException("manifest.payload.entry must be inside payload/.");
        }

        return result;
    }
}

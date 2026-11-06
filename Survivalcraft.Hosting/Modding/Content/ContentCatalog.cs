using System.Text.Json.Nodes;

using Game.Blocks.Collision;

namespace Game.Modding.Content;

public sealed class ContentCatalog
{
    private static readonly ModId _gameModId = new("game");

    private readonly IReadOnlyList<Entry> _entries;

    private readonly Dictionary<ResourceId, CollisionModelGeometry> _collisionModels = new();

    private readonly object _collisionGeometryLock = new();

    public CollisionGeometryCatalog LoadCollisionGeometry(IEnumerable<ResourceId> requiredAssets)
    {
        lock (_collisionGeometryLock)
        {
            var models = new List<CollisionModelGeometry>();
            foreach (var id in requiredAssets.Distinct())
            {
                if (!_collisionModels.TryGetValue(id, out var geometry))
                {
                    var entry = _entries.SingleOrDefault(entry => entry.Id == id) ??
                                throw new InvalidDataException($"Collision model asset {id} is not registered.");
                    using var stream = entry.Registration.OpenRead();
                    geometry = CollisionModelGeometryReader.Read(id, stream);
                    _collisionModels.Add(id, geometry);
                }

                models.Add(geometry);
            }

            return new CollisionGeometryCatalog(models);
        }
    }

    private ContentCatalog(IReadOnlyList<Entry> entries)
    {
        _entries = entries;
        RegisteredResources = Array.AsReadOnly(
            entries.Select(entry => new RegisteredContentResource(
                    entry.Id,
                    entry.Registration.RelativePath,
                    ToContentPath(entry),
                    () => entry.Registration.OpenRead()
                )
            ).ToArray()
        );
    }

    public IReadOnlyList<ResourceId> Resources => _entries.Select(entry => entry.Id).ToArray();

    public IReadOnlyList<string> LanguageTypes => _entries
        .Where(entry => entry.Id.Namespace == _gameModId)
        .Select(entry => GetLanguageType(entry.Registration.RelativePath))
        .Where(type => type is not null)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
        .Cast<string>()
        .ToArray();

    public static ContentCatalog Compile(ExtensionRegistry extensions)
    {
        var registry = extensions.GetRegistry<ContentRegistration>(ContentExtensions.RegistryName);
        var entries = registry.Entries
            .Select(pair => new Entry(pair.Key, pair.Value))
            .ToArray();
        return new ContentCatalog(entries);
    }

    public IReadOnlyList<RegisteredContentResource> RegisteredResources { get; }

    public Localization.LanguageCatalog CreateLanguageCatalog(string languageType)
    {
        var resolvedLanguage = ResolveLanguage(languageType);
        var language = new Localization.LanguageCatalog();
        language.BeginInitialization();
        language.LanguageTypes.Clear();
        language.LanguageTypes.AddRange(LanguageTypes);
        language.ClearLanguageDisplayNames();
        foreach (var entry in _entries.Where(entry => entry.Id.Namespace == _gameModId))
        {
            var entryLanguageType = GetLanguageType(entry.Registration.RelativePath);
            if (entryLanguageType == null)
            {
                continue;
            }

            using var stream = entry.Registration.OpenRead();
            language.RegisterLanguageDisplayName(entryLanguageType, ReadLanguageDisplayName(stream));
        }

        var languagePath = $"lang/{resolvedLanguage}.json";
        foreach (var entry in _entries.Where(entry =>
                     entry.Registration.RelativePath.Equals(languagePath, StringComparison.OrdinalIgnoreCase)
                 ))
        {
            using var stream = entry.Registration.OpenRead();
            language.LoadJson(stream, resolvedLanguage);
        }

        language.RefreshCommonWords();
        language.CompleteInitialization(resolvedLanguage);
        return language;
    }

    private static string? ReadLanguageDisplayName(Stream stream)
    {
        try
        {
            var root = JsonNode.Parse(stream) as JsonObject;
            return root?["Language"]?["Name"]?.ToString();
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to read language display name: {ex.Message}");
            return null;
        }
    }

    private static string ToContentPath(Entry entry)
    {
        return entry.Id.Namespace == _gameModId
            ? entry.Registration.RelativePath
            : $"{entry.Id.Namespace}/{entry.Registration.RelativePath}";
    }

    private static string? GetLanguageType(string relativePath)
    {
        if (!relativePath.StartsWith("lang/", StringComparison.OrdinalIgnoreCase) ||
            !relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetFileNameWithoutExtension(relativePath);
    }

    private string ResolveLanguage(string preferredLanguage)
    {
        if (!string.IsNullOrWhiteSpace(preferredLanguage) &&
            LanguageTypes.Contains(preferredLanguage, StringComparer.OrdinalIgnoreCase))
        {
            return LanguageTypes.First(type => type.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase));
        }

        if (LanguageTypes.Contains("zh-CN", StringComparer.OrdinalIgnoreCase))
        {
            return LanguageTypes.First(type => type.Equals("zh-CN", StringComparison.OrdinalIgnoreCase));
        }

        if (LanguageTypes.Contains("en-US", StringComparer.OrdinalIgnoreCase))
        {
            return LanguageTypes.First(type => type.Equals("en-US", StringComparison.OrdinalIgnoreCase));
        }

        return LanguageTypes.FirstOrDefault() ??
               throw new InvalidOperationException("No language resources are registered.");
    }

    private sealed record Entry(ResourceId Id, ContentRegistration Registration);
}

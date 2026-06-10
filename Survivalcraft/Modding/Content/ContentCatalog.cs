namespace Game.Modding.Content;

public sealed class ContentCatalog
{
    private readonly IReadOnlyList<Entry> _entries;
    private readonly HashSet<ModId> _installedOwners = [];

    private ContentCatalog(IReadOnlyList<Entry> entries)
    {
        _entries = entries;
    }

    public IReadOnlyList<ResourceId> Resources => _entries.Select(entry => entry.Id).ToArray();

    public IReadOnlyList<string> LanguageTypes => _entries
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

    public void Install()
    {
        BuiltInContentReaders.Register();
        foreach (var entry in _entries)
        {
            if (ContentManager.ContainsOwned(entry.Id.Namespace, ToContentPath(entry)))
            {
                continue;
            }

            using var source = entry.Registration.OpenRead();
            var memory = new MemoryStream();
            source.CopyTo(memory);
            memory.Position = 0;
            var content = new ContentInfo(ToContentPath(entry));
            content.SetContentStream(memory);
            try
            {
                ContentManager.AddOwned(entry.Id.Namespace, content);
                _installedOwners.Add(entry.Id.Namespace);
            }
            catch
            {
                content.Dispose();
                throw;
            }
        }
    }

    public void InitializeLanguage(string languageType)
    {
        var resolvedLanguage = ResolveLanguage(languageType);
        LanguageManager.Initialize(resolvedLanguage);
        LanguageManager.LanguageTypes.Clear();
        LanguageManager.LanguageTypes.AddRange(LanguageTypes);

        var languagePath = $"lang/{resolvedLanguage}.json";
        foreach (var entry in _entries.Where(entry =>
                     entry.Registration.RelativePath.Equals(languagePath, StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = entry.Registration.OpenRead();
            LanguageManager.LoadJson(stream, resolvedLanguage);
        }

        LanguageManager.RefreshCommonWords();
    }

    public void Uninstall()
    {
        foreach (var owner in _installedOwners)
        {
            ContentManager.RemoveOwner(owner);
        }

        _installedOwners.Clear();
    }

    private static string ToContentPath(Entry entry)
    {
        return entry.Id.Namespace == new ModId("game")
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

        return LanguageTypes.FirstOrDefault()
               ?? throw new InvalidOperationException("No language resources are registered.");
    }

    private sealed record Entry(ResourceId Id, ContentRegistration Registration);
}

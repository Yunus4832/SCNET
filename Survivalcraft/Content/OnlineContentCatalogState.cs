using Content.Packaging;

namespace Game.Content;

public enum OnlineContentStatusFilter
{
    All,
    Cached,
    Profile,
    CurrentSession,
    Conflict,
    Missing
}

public sealed record OnlineContentNavigation(
    ContentPackageType? Type = null,
    string? Identifier = null,
    string? Version = null,
    string? PackageHash = null,
    string ReturnScreen = "Content")
{
    public OnlineContentNavigation Normalize()
    {
        var identifier = Identifier?.Trim();
        var version = Version?.Trim();
        var packageHash = PackageHash?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(identifier))
        {
            identifier = null;
        }

        if (string.IsNullOrEmpty(version))
        {
            version = null;
        }

        if (string.IsNullOrEmpty(packageHash))
        {
            packageHash = null;
        }

        if (string.IsNullOrWhiteSpace(ReturnScreen) ||
            (version is null) != (packageHash is null) ||
            version is not null && (Type is null || identifier is null) ||
            packageHash is not null &&
            (packageHash.Length != 64 || packageHash.Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))))
        {
            throw new ArgumentException(
                "Exact online content navigation requires type, identifier, version and canonical SHA-256 hash.");
        }

        return this with
        {
            Identifier = identifier,
            Version = version,
            PackageHash = packageHash,
            ReturnScreen = ReturnScreen.Trim()
        };
    }
}

public sealed record OnlineContentVersionState(
    AggregatedContentVersion Version,
    bool IsCached,
    bool IsProfileReferenced,
    bool IsCurrentSessionReferenced);

public sealed record OnlineContentReference(ContentPackageType Type, string Identifier, string PackageHash);

public sealed class OnlineContentCatalogState
{
    private readonly Dictionary<(ContentPackageType Type, string Identifier), AggregatedContentEntry> _entries = [];
    private HashSet<string> _cachedHashes = new(StringComparer.Ordinal);
    private IReadOnlyList<OnlineContentReference> _currentSessionReferences = [];
    private IReadOnlyList<OnlineContentReference> _profileReferences = [];

    public IReadOnlyList<ContentCatalogRepositoryFailure> Failures { get; private set; } = [];

    public bool HasMore { get; private set; }

    public int NextPageIndex { get; private set; } = 1;

    public void Reset()
    {
        _entries.Clear();
        Failures = [];
        HasMore = false;
        NextPageIndex = 1;
    }

    public void SetReferences(IEnumerable<string> cachedHashes, IEnumerable<OnlineContentReference> profileReferences,
        IEnumerable<OnlineContentReference> currentSessionReferences)
    {
        ArgumentNullException.ThrowIfNull(cachedHashes);
        ArgumentNullException.ThrowIfNull(profileReferences);
        ArgumentNullException.ThrowIfNull(currentSessionReferences);
        _cachedHashes = cachedHashes.ToHashSet(StringComparer.Ordinal);
        _profileReferences = profileReferences.Distinct().ToArray();
        _currentSessionReferences = currentSessionReferences.Distinct().ToArray();
    }

    public void Append(AggregatedContentPage page, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (pageIndex != NextPageIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        foreach (var entry in page.Entries)
        {
            var key = (entry.Type, entry.Identifier);
            _entries[key] = _entries.TryGetValue(key, out var existing)
                ? ContentCatalogMerger.Merge(existing, entry)
                : entry;
        }

        Failures = Failures.Concat(page.Failures)
            .GroupBy(failure => failure.RepositoryId)
            .Select(group => group.First())
            .OrderBy(failure => failure.RepositoryName, StringComparer.Ordinal)
            .ToArray();
        HasMore = page.HasMore;
        NextPageIndex++;
    }

    public IReadOnlyList<AggregatedContentEntry> Filter(string? search, ContentPackageType? type,
        Guid? repositoryId, OnlineContentStatusFilter status)
    {
        var normalizedSearch = search?.Trim();
        return _entries.Values
            .Where(entry => type is null || entry.Type == type)
            .Where(entry => string.IsNullOrEmpty(normalizedSearch) ||
                            entry.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                            entry.Identifier.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                            entry.Summary?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) == true)
            .Where(entry => repositoryId is null || entry.Versions.SelectMany(version => version.Sources)
                .Any(source => source.RepositoryId == repositoryId))
            .Where(entry => MatchesStatus(entry, status))
            .OrderBy(entry => entry.Type)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Identifier, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<OnlineContentVersionState> GetVersions(AggregatedContentEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Versions.Select(version => new OnlineContentVersionState(version,
                _cachedHashes.Contains(version.PackageHash),
                _profileReferences.Any(reference => reference.PackageHash == version.PackageHash),
                _currentSessionReferences.Any(reference => reference.PackageHash == version.PackageHash)))
            .ToArray();
    }

    public (AggregatedContentEntry Entry, AggregatedContentVersion? Version)? Find(OnlineContentNavigation navigation)
    {
        navigation = navigation.Normalize();
        var entry = _entries.Values.FirstOrDefault(candidate =>
            (navigation.Type is null || candidate.Type == navigation.Type) &&
            (navigation.Identifier is null ||
             string.Equals(candidate.Identifier, navigation.Identifier, StringComparison.Ordinal)));
        if (entry is null)
        {
            return null;
        }

        var version = navigation.Version is null
            ? null
            : entry.Versions.FirstOrDefault(candidate =>
                string.Equals(candidate.Version, navigation.Version, StringComparison.Ordinal) &&
                string.Equals(candidate.PackageHash, navigation.PackageHash, StringComparison.Ordinal));
        return (entry, version);
    }

    private bool MatchesStatus(AggregatedContentEntry entry, OnlineContentStatusFilter status)
    {
        return status switch
        {
            OnlineContentStatusFilter.All => true,
            OnlineContentStatusFilter.Cached => entry.Versions.Any(version =>
                _cachedHashes.Contains(version.PackageHash)),
            OnlineContentStatusFilter.Profile => MatchesReference(entry, _profileReferences),
            OnlineContentStatusFilter.CurrentSession => MatchesReference(entry, _currentSessionReferences),
            OnlineContentStatusFilter.Conflict => entry.Versions.Any(version => version.HasHashConflict),
            OnlineContentStatusFilter.Missing => _profileReferences.Concat(_currentSessionReferences)
                .Any(reference => MatchesIdentity(entry, reference) && !_cachedHashes.Contains(reference.PackageHash)),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private static bool MatchesReference(AggregatedContentEntry entry,
        IEnumerable<OnlineContentReference> references)
    {
        return references.Any(reference => MatchesIdentity(entry, reference));
    }

    private static bool MatchesIdentity(AggregatedContentEntry entry, OnlineContentReference reference)
    {
        return entry.Type == reference.Type &&
               string.Equals(entry.Identifier, reference.Identifier, StringComparison.Ordinal);
    }
}

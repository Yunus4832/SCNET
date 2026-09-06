namespace Game.Content;

public sealed record ContentRepository
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public int Priority { get; init; }

    public ContentRepository Normalize()
    {
        if (Id == Guid.Empty)
        {
            throw new ArgumentException("A repository must have a stable nonempty ID.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(BaseUrl);
        if (!Uri.TryCreate(BaseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Repository addresses must be absolute HTTP(S) URLs without credentials, queries or fragments.");
        }

        return this with { Name = Name.Trim(), BaseUrl = uri.AbsoluteUri.TrimEnd('/') };
    }

    public static IReadOnlyList<ContentRepository> NormalizeAll(IEnumerable<ContentRepository> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        var result = new List<ContentRepository>();
        var ids = new HashSet<Guid>();
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var repository in repositories)
        {
            ArgumentNullException.ThrowIfNull(repository);
            var normalized = repository.Normalize();
            if (!ids.Add(normalized.Id) || !addresses.Add(normalized.BaseUrl))
            {
                throw new ArgumentException("Repository IDs and normalized addresses must be unique.");
            }

            result.Add(normalized);
        }

        return result.OrderBy(repository => repository.Priority).ThenBy(repository => repository.Id).ToArray();
    }
}

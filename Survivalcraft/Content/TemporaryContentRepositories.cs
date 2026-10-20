namespace Game.Content;

public static class TemporaryContentRepositories
{
    public const int MaximumCount = ProtocolLimits.MaximumTemporaryRepositories;
    public const int MaximumUrlLength = ProtocolLimits.MaximumRepositoryUrlLength;
    public const int MinimumPriority = ProtocolLimits.MinimumRepositoryPriority;
    public const int MaximumPriority = ProtocolLimits.MaximumRepositoryPriority;

    public static IReadOnlyList<ContentRepository> Validate(IEnumerable<ContentRepository> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        var items = repositories.ToArray();
        if (items.Length > MaximumCount)
        {
            throw new InvalidDataException("Too many temporary content repositories were declared.");
        }

        foreach (var repository in items)
        {
            if (repository.BaseUrl.Length > MaximumUrlLength ||
                repository.Priority is < MinimumPriority or > MaximumPriority)
            {
                throw new InvalidDataException("A temporary content repository is outside protocol limits.");
            }
        }

        try
        {
            return ContentRepository.NormalizeAll(items);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Temporary content repositories are invalid.", exception);
        }
    }
}

namespace Game.Content;

public sealed class ContentSourceContext
{
    private readonly IReadOnlyList<ScopedRepository> _candidates;

    private ContentSourceContext(IReadOnlyList<ContentRepository> persistentRepositories, Guid? sessionScopeId,
        IReadOnlyList<ContentRepository> sessionRepositories)
    {
        PersistentRepositories = persistentRepositories;
        SessionScopeId = sessionScopeId;
        SessionRepositories = sessionRepositories;

        var addresses = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<ScopedRepository>();
        if (sessionScopeId is not null)
        {
            foreach (var repository in sessionRepositories.Where(repository => repository.IsEnabled))
            {
                if (addresses.Add(repository.BaseUrl))
                {
                    candidates.Add(new ScopedRepository(sessionScopeId.Value, repository, true));
                }
            }
        }

        foreach (var repository in persistentRepositories.Where(repository => repository.IsEnabled))
        {
            if (addresses.Add(repository.BaseUrl))
            {
                candidates.Add(new ScopedRepository(Guid.Empty, repository, false));
            }
        }

        _candidates = candidates;
    }

    public IReadOnlyList<ContentRepository> PersistentRepositories { get; }
    public Guid? SessionScopeId { get; }
    public IReadOnlyList<ContentRepository> SessionRepositories { get; }

    internal IReadOnlyList<ScopedRepository> Candidates => _candidates;

    public static ContentSourceContext Persistent(IEnumerable<ContentRepository> repositories)
    {
        return new ContentSourceContext(ContentRepository.NormalizeAll(repositories), null, []);
    }

    public static ContentSourceContext Session(Guid sessionScopeId,
        IEnumerable<ContentRepository> sessionRepositories,
        IEnumerable<ContentRepository> persistentRepositories)
    {
        if (sessionScopeId == Guid.Empty)
        {
            throw new ArgumentException("Session repository scopes require a nonempty ID.", nameof(sessionScopeId));
        }

        return new ContentSourceContext(ContentRepository.NormalizeAll(persistentRepositories), sessionScopeId,
            ContentRepository.NormalizeAll(sessionRepositories));
    }

    internal void Configure(ContentServerClientPool pool)
    {
        pool.Update(Guid.Empty, PersistentRepositories);
        if (SessionScopeId is not null)
        {
            pool.Update(SessionScopeId.Value, SessionRepositories);
        }
    }

    internal sealed record ScopedRepository(Guid ScopeId, ContentRepository Repository, bool IsSession);
}

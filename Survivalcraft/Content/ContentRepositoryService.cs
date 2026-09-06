namespace Game.Content;

public sealed class ContentRepositoryService
{
    private readonly object _gate = new();
    private readonly ContentServerClientPool _pool;
    private readonly Action<IReadOnlyList<ContentRepository>> _save;
    private IReadOnlyList<ContentRepository> _repositories;

    public ContentRepositoryService(IEnumerable<ContentRepository> repositories, ContentServerClientPool pool,
        Action<IReadOnlyList<ContentRepository>> save)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(save);
        _pool = pool;
        _save = save;
        _repositories = ContentRepository.NormalizeAll(repositories);
        _pool.Update(Guid.Empty, _repositories);
    }

    public IReadOnlyList<ContentRepository> Snapshot()
    {
        lock (_gate)
        {
            return _repositories.ToArray();
        }
    }

    public void Add(ContentRepository repository)
    {
        lock (_gate)
        {
            Commit(_repositories.Append(repository));
        }
    }

    public void Edit(ContentRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        lock (_gate)
        {
            EnsureExists(repository.Id);
            Commit(_repositories.Select(item => item.Id == repository.Id ? repository : item));
        }
    }

    public void Delete(Guid repositoryId)
    {
        lock (_gate)
        {
            EnsureExists(repositoryId);
            Commit(_repositories.Where(item => item.Id != repositoryId));
        }
    }

    public void SetOrder(IReadOnlyList<Guid> repositoryIds)
    {
        ArgumentNullException.ThrowIfNull(repositoryIds);
        lock (_gate)
        {
            if (repositoryIds.Count != _repositories.Count || repositoryIds.Distinct().Count() != repositoryIds.Count ||
                repositoryIds.Any(id => _repositories.All(repository => repository.Id != id)))
            {
                throw new ArgumentException("Repository order must contain every configured repository exactly once.",
                    nameof(repositoryIds));
            }

            var repositoriesById = _repositories.ToDictionary(repository => repository.Id);
            Commit(repositoryIds.Select((id, priority) => repositoriesById[id] with { Priority = priority }));
        }
    }

    public async Task<ContentServerHealth> TestConnectionAsync(Guid repositoryId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var repository = _repositories.FirstOrDefault(item => item.Id == repositoryId);
            if (repository is null)
            {
                throw new KeyNotFoundException("The repository does not exist.");
            }

            if (!repository.IsEnabled)
            {
                throw new InvalidOperationException("A disabled repository cannot be tested.");
            }
        }

        using var lease = _pool.Acquire(Guid.Empty, repositoryId);
        return await lease.Client.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
    }

    private void EnsureExists(Guid repositoryId)
    {
        if (!_repositories.Any(item => item.Id == repositoryId))
        {
            throw new KeyNotFoundException("The repository does not exist.");
        }
    }

    private void Commit(IEnumerable<ContentRepository> repositories)
    {
        var normalized = ContentRepository.NormalizeAll(repositories);
        _save(Array.AsReadOnly(normalized.ToArray()));
        _pool.Update(Guid.Empty, normalized);
        _repositories = normalized;
    }
}

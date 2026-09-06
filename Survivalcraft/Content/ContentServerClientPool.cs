namespace Game.Content;

public sealed class ContentServerClientPool(ContentServerClientFactory factory) : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid Scope, Guid Repository), Entry> _entries = [];
    private readonly HashSet<Guid> _closedScopes = [];
    private bool _disposed;

    public void Update(Guid scope, IEnumerable<ContentRepository> repositories)
    {
        var normalized = ContentRepository.NormalizeAll(repositories).Where(item => item.IsEnabled)
            .ToDictionary(item => item.Id);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_closedScopes.Contains(scope))
            {
                throw new InvalidOperationException("The content repository scope has ended.");
            }

            foreach (var key in _entries.Keys.Where(key => key.Scope == scope).ToArray())
            {
                var entry = _entries[key];
                if (!normalized.TryGetValue(key.Repository, out var repository) || repository.BaseUrl != entry.BaseUrl)
                {
                    _entries.Remove(key);
                    Retire(entry);
                }
            }

            foreach (var repository in normalized.Values)
            {
                var key = (scope, repository.Id);
                if (!_entries.ContainsKey(key))
                {
                    _entries.Add(key, new Entry(repository.BaseUrl, repository));
                }
                else
                {
                    _entries[key].Repository = repository;
                }
            }
        }
    }

    public Lease Acquire(Guid scope, Guid repositoryId)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_entries.TryGetValue((scope, repositoryId), out var entry))
            {
                throw new KeyNotFoundException("The repository is not enabled in this scope.");
            }

            entry.Client ??= factory.Create(entry.Repository);
            entry.Users++;
            return new Lease(entry.Client, () => Release(entry));
        }
    }

    public void RemoveScope(Guid scope)
    {
        if (scope == Guid.Empty)
        {
            throw new ArgumentException("The persistent repository scope cannot be ended.", nameof(scope));
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _closedScopes.Add(scope);
            foreach (var key in _entries.Keys.Where(key => key.Scope == scope).ToArray())
            {
                var entry = _entries[key];
                _entries.Remove(key);
                Retire(entry);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var entry in _entries.Values)
            {
                Retire(entry);
            }

            _entries.Clear();
        }
    }

    private static void Retire(Entry entry)
    {
        entry.Retired = true;
        if (entry.Users == 0)
        {
            entry.Client?.Dispose();
        }
    }

    private void Release(Entry entry)
    {
        lock (_gate)
        {
            entry.Users--;
            if (entry.Retired && entry.Users == 0)
            {
                entry.Client?.Dispose();
            }
        }
    }

    private sealed class Entry(string baseUrl, ContentRepository repository)
    {
        public string BaseUrl { get; } = baseUrl;
        public ContentRepository Repository { get; set; } = repository;
        public ContentServerClient? Client { get; set; }
        public int Users { get; set; }
        public bool Retired { get; set; }
    }

    public sealed class Lease : IDisposable
    {
        private Action? _release;

        internal Lease(ContentServerClient client, Action release)
        {
            Client = client;
            _release = release;
        }

        public ContentServerClient Client { get; }

        public void Dispose()
        {
            Interlocked.Exchange(ref _release, null)?.Invoke();
        }
    }
}

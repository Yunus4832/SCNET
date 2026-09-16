namespace Game.Servers;

public sealed class ServerDirectoryService
{
    public const int MaximumRecentServers = 20;

    private readonly int _defaultPort;
    private readonly object _gate = new();
    private readonly Action<ServerDirectoryState> _save;
    private ServerDirectoryState _state;

    public ServerDirectoryService(ServerDirectoryState state, int defaultPort, Action<ServerDirectoryState> save)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(save);
        _defaultPort = defaultPort;
        _save = save;
        _state = Normalize(state, defaultPort);
    }

    public ServerDirectoryState Snapshot()
    {
        lock (_gate)
        {
            return _state with
            {
                MyServers = _state.MyServers.ToArray(),
                Favorites = _state.Favorites.ToArray(),
                RecentServers = _state.RecentServers.ToArray(),
                InstalledSources = _state.InstalledSources.ToArray()
            };
        }
    }

    public StoredServerEntry AddMyServer(string name, string address)
    {
        lock (_gate)
        {
            var entry = CreateServer(name, address, _state.MyServers.Count);
            Commit(_state with { MyServers = _state.MyServers.Append(entry).ToArray() });
            return entry;
        }
    }

    public void EditMyServer(StoredServerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            EnsureExists(_state.MyServers, entry.Id);
            var normalized = NormalizeEntry(entry, _defaultPort);
            Commit(_state with
            {
                MyServers = _state.MyServers.Select(item => item.Id == entry.Id ? normalized : item).ToArray()
            });
        }
    }

    public void DeleteMyServer(Guid id)
    {
        lock (_gate)
        {
            EnsureExists(_state.MyServers, id);
            Commit(_state with { MyServers = Reorder(_state.MyServers.Where(item => item.Id != id)) });
        }
    }

    public void DeleteFavorite(Guid id)
    {
        lock (_gate)
        {
            EnsureExists(_state.Favorites, id);
            Commit(_state with { Favorites = Reorder(_state.Favorites.Where(item => item.Id != id)) });
        }
    }

    public void DeleteRecentServer(Guid id)
    {
        lock (_gate)
        {
            EnsureExists(_state.RecentServers, id);
            Commit(_state with { RecentServers = Reorder(_state.RecentServers.Where(item => item.Id != id)) });
        }
    }

    public StoredServerEntry AddFavorite(string name, string address)
    {
        var normalizedAddress = ServerAddress.Normalize(address, _defaultPort);
        lock (_gate)
        {
            if (_state.Favorites.Any(entry => entry.Address == normalizedAddress))
            {
                throw new ArgumentException("A favorite with this address already exists.", nameof(address));
            }

            var entry = CreateServer(name, normalizedAddress, _state.Favorites.Count);
            Commit(_state with { Favorites = _state.Favorites.Append(entry).ToArray() });
            return entry;
        }
    }

    public void RecordConnectionAttempt(string name, string address, DateTimeOffset now)
    {
        var normalizedAddress = ServerAddress.Normalize(address, _defaultPort);
        lock (_gate)
        {
            var existing = _state.RecentServers.FirstOrDefault(entry => entry.Address == normalizedAddress);
            var recent = new StoredServerEntry
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                Name = NormalizeName(name),
                Address = normalizedAddress,
                UpdatedAt = now
            };
            var entries = _state.RecentServers.Where(entry => entry.Address != normalizedAddress)
                .Prepend(recent)
                .Take(MaximumRecentServers);
            Commit(_state with { RecentServers = Reorder(entries) });
        }
    }

    public InstalledServerSource InstallSource(InstalledServerSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (_gate)
        {
            var candidate = source with { Order = _state.InstalledSources.Count };
            var normalized = NormalizeInstalledSource(candidate);
            if (_state.InstalledSources.Any(item => item.ApiUrl == normalized.ApiUrl))
            {
                throw new ArgumentException("A server source with this URL is already installed.", nameof(source));
            }

            Commit(_state with { InstalledSources = _state.InstalledSources.Append(normalized).ToArray() });
            return normalized;
        }
    }

    public void EditInstalledSource(InstalledServerSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (_gate)
        {
            EnsureInstalledSourceExists(source.Id);
            var normalized = NormalizeInstalledSource(source);
            if (_state.InstalledSources.Any(item => item.Id != normalized.Id && item.ApiUrl == normalized.ApiUrl))
            {
                throw new ArgumentException("A server source with this URL is already installed.", nameof(source));
            }

            Commit(_state with
            {
                InstalledSources = _state.InstalledSources.Select(item => item.Id == normalized.Id ? normalized : item)
                    .ToArray()
            });
        }
    }

    public void DeleteInstalledSource(Guid id)
    {
        lock (_gate)
        {
            EnsureInstalledSourceExists(id);
            Commit(_state with
            {
                InstalledSources = ReorderInstalledSources(_state.InstalledSources.Where(item => item.Id != id))
            });
        }
    }

    public void SetInstalledSourceOrder(IReadOnlyList<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_gate)
        {
            if (ids.Count != _state.InstalledSources.Count || ids.Distinct().Count() != ids.Count ||
                ids.Any(id => _state.InstalledSources.All(item => item.Id != id)))
            {
                throw new ArgumentException("Installed source order must contain every source exactly once.",
                    nameof(ids));
            }

            var byId = _state.InstalledSources.ToDictionary(item => item.Id);
            Commit(_state with
            {
                InstalledSources = ids.Select((id, order) => byId[id] with { Order = order }).ToArray()
            });
        }
    }

    public static ServerDirectoryState Normalize(ServerDirectoryState state, int defaultPort)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state with
        {
            MyServers = NormalizeEntries(state.MyServers, defaultPort, false),
            Favorites = NormalizeEntries(state.Favorites, defaultPort, true),
            RecentServers = NormalizeEntries(state.RecentServers, defaultPort, true)
                .OrderByDescending(entry => entry.UpdatedAt).Take(MaximumRecentServers)
                .Select((entry, order) => entry with { Order = order }).ToArray(),
            InstalledSources = NormalizeInstalledSources(state.InstalledSources)
        };
    }

    private static IReadOnlyList<StoredServerEntry> NormalizeEntries(IEnumerable<StoredServerEntry> entries,
        int defaultPort, bool uniqueAddresses)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var ids = new HashSet<Guid>();
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<StoredServerEntry>();
        foreach (var entry in entries.OrderBy(entry => entry.Order))
        {
            var normalized = NormalizeEntry(entry, defaultPort);
            if (!ids.Add(normalized.Id) || uniqueAddresses && !addresses.Add(normalized.Address))
            {
                throw new ArgumentException("Server entry IDs must be unique and this source requires unique addresses.");
            }

            result.Add(normalized with { Order = result.Count });
        }

        return result;
    }

    private static StoredServerEntry NormalizeEntry(StoredServerEntry entry, int defaultPort)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id == Guid.Empty)
        {
            throw new ArgumentException("Server entry must have a stable nonempty ID.");
        }

        return entry with
        {
            Name = NormalizeName(entry.Name),
            Address = ServerAddress.Normalize(entry.Address, defaultPort)
        };
    }

    private static IReadOnlyList<InstalledServerSource> NormalizeInstalledSources(
        IEnumerable<InstalledServerSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var ids = new HashSet<Guid>();
        var urls = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<InstalledServerSource>();
        foreach (var source in sources.OrderBy(item => item.Order))
        {
            var normalized = NormalizeInstalledSource(source) with { Order = result.Count };
            if (!ids.Add(normalized.Id) || !urls.Add(normalized.ApiUrl))
            {
                throw new ArgumentException("Installed server source IDs and URLs must be unique.");
            }

            result.Add(normalized);
        }

        return result;
    }

    private static InstalledServerSource NormalizeInstalledSource(InstalledServerSource source)
    {
        if (source.Id == Guid.Empty)
        {
            throw new ArgumentException("Installed server source must have a stable nonempty ID.");
        }

        var name = NormalizeName(source.Name);
        if (!Uri.TryCreate(source.ApiUrl.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Server source URL must be an absolute HTTP(S) URL without credentials or fragment.");
        }

        return source with
        {
            RegistrationId = string.IsNullOrWhiteSpace(source.RegistrationId)
                ? null
                : source.RegistrationId.Trim(),
            Name = name,
            ApiUrl = uri.AbsoluteUri
        };
    }

    private StoredServerEntry CreateServer(string name, string address, int order)
    {
        return new StoredServerEntry
        {
            Name = NormalizeName(name),
            Address = ServerAddress.Normalize(address, _defaultPort),
            Order = order,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > 100 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Server or source name is invalid.", nameof(name));
        }

        return normalized;
    }

    private static IReadOnlyList<StoredServerEntry> Reorder(IEnumerable<StoredServerEntry> entries)
    {
        return entries.Select((entry, order) => entry with { Order = order }).ToArray();
    }

    private static IReadOnlyList<InstalledServerSource> ReorderInstalledSources(
        IEnumerable<InstalledServerSource> sources)
    {
        return sources.Select((source, order) => source with { Order = order }).ToArray();
    }

    private static void EnsureExists(IEnumerable<StoredServerEntry> entries, Guid id)
    {
        if (entries.All(entry => entry.Id != id))
        {
            throw new KeyNotFoundException("Server entry does not exist.");
        }
    }

    private void EnsureInstalledSourceExists(Guid id)
    {
        if (_state.InstalledSources.All(source => source.Id != id))
        {
            throw new KeyNotFoundException("Installed server source does not exist.");
        }
    }

    private void Commit(ServerDirectoryState state)
    {
        var normalized = Normalize(state, _defaultPort);
        _save(normalized);
        _state = normalized;
    }
}

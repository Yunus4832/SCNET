namespace Game.Servers;

public sealed class ServerDirectoryService
{
    public const int MaximumRecentServers = 20;

    private readonly int _defaultPort;
    private readonly Lock _gate = new();
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
                LocalServers = _state.LocalServers.ToArray(),
                InstalledSources = _state.InstalledSources.ToArray()
            };
        }
    }

    public StoredServerEntry AddMyServer(string name, string address)
    {
        lock (_gate)
        {
            var normalizedAddress = ServerAddress.Normalize(address, _defaultPort);
            var existing = _state.LocalServers.FirstOrDefault(entry => entry.Address == normalizedAddress);
            if (existing?.Tags.HasFlag(LocalServerTag.MyServer) == true)
            {
                throw new ArgumentException("This server has already been added.", nameof(address));
            }

            var entry = existing is null
                ? CreateServer(name, normalizedAddress, LocalServerTag.MyServer)
                : existing with { Name = NormalizeName(name), Tags = existing.Tags | LocalServerTag.MyServer };
            Commit(_state with
            {
                LocalServers = existing is null
                    ? _state.LocalServers.Append(entry).ToArray()
                    : _state.LocalServers.Select(item => item.Id == existing.Id ? entry : item).ToArray()
            });
            return entry;
        }
    }

    public void DeleteLocalServer(Guid id)
    {
        lock (_gate)
        {
            EnsureExists(_state.LocalServers, id);
            Commit(_state with { LocalServers = Reorder(_state.LocalServers.Where(entry => entry.Id != id)) });
        }
    }

    public void RemoveLocalTag(Guid id, LocalServerTag tag)
    {
        if (tag is not (LocalServerTag.MyServer or LocalServerTag.Favorite or LocalServerTag.Recent))
        {
            throw new ArgumentOutOfRangeException(nameof(tag));
        }

        lock (_gate)
        {
            EnsureExists(_state.LocalServers, id);
            Commit(_state with
            {
                LocalServers = Reorder(_state.LocalServers.Select(entry => entry.Id == id
                        ? entry with { Tags = entry.Tags & ~tag }
                        : entry)
                    .Where(entry => entry.Tags != LocalServerTag.None))
            });
        }
    }

    public StoredServerEntry AddFavorite(string name, string address)
    {
        var normalizedAddress = ServerAddress.Normalize(address, _defaultPort);
        lock (_gate)
        {
            var existing = _state.LocalServers.FirstOrDefault(entry => entry.Address == normalizedAddress);
            if (existing?.Tags.HasFlag(LocalServerTag.Favorite) == true)
            {
                throw new ArgumentException("A favorite with this address already exists.", nameof(address));
            }

            var entry = existing is null
                ? CreateServer(name, normalizedAddress, LocalServerTag.Favorite)
                : existing with { Tags = existing.Tags | LocalServerTag.Favorite };
            Commit(_state with
            {
                LocalServers = existing is null
                    ? _state.LocalServers.Append(entry).ToArray()
                    : _state.LocalServers.Select(item => item.Id == existing.Id ? entry : item).ToArray()
            });
            return entry;
        }
    }

    public void RecordConnectionAttempt(string name, string address, DateTimeOffset now)
    {
        var normalizedAddress = ServerAddress.Normalize(address, _defaultPort);
        lock (_gate)
        {
            var existing = _state.LocalServers.FirstOrDefault(entry => entry.Address == normalizedAddress);
            var recent = existing is null
                ? CreateServer(name, normalizedAddress, LocalServerTag.Recent) with { UpdatedAt = now }
                : existing with
                {
                    Name = existing.Tags == LocalServerTag.Recent ? NormalizeName(name) : existing.Name,
                    Tags = existing.Tags | LocalServerTag.Recent,
                    UpdatedAt = now
                };
            var entries = existing is null
                ? _state.LocalServers.Append(recent)
                : _state.LocalServers.Select(entry => entry.Id == existing.Id ? recent : entry);
            var expired = entries.Where(entry => entry.Tags.HasFlag(LocalServerTag.Recent))
                .OrderByDescending(entry => entry.UpdatedAt)
                .Skip(MaximumRecentServers)
                .Select(entry => entry.Id)
                .ToHashSet();
            Commit(_state with
            {
                LocalServers = Reorder(entries.Select(entry => expired.Contains(entry.Id)
                        ? entry with { Tags = entry.Tags & ~LocalServerTag.Recent }
                        : entry)
                    .Where(entry => entry.Tags != LocalServerTag.None))
            });
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
            LocalServers = NormalizeEntries(state.LocalServers, defaultPort),
            InstalledSources = NormalizeInstalledSources(state.InstalledSources)
        };
    }

    private static IReadOnlyList<StoredServerEntry> NormalizeEntries(IEnumerable<StoredServerEntry> entries,
        int defaultPort)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var ids = new HashSet<Guid>();
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<StoredServerEntry>();
        foreach (var entry in entries.OrderBy(entry => entry.Order))
        {
            var normalized = NormalizeEntry(entry, defaultPort);
            if (!ids.Add(normalized.Id) || !addresses.Add(normalized.Address))
            {
                throw new ArgumentException("Local server IDs and addresses must be unique.");
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

        if (entry.Tags == LocalServerTag.None ||
            (entry.Tags & ~(LocalServerTag.MyServer | LocalServerTag.Favorite | LocalServerTag.Recent)) != 0)
        {
            throw new ArgumentException("Local server tags are invalid.");
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

    private StoredServerEntry CreateServer(string name, string address, LocalServerTag tag)
    {
        return new StoredServerEntry
        {
            Name = NormalizeName(name),
            Address = ServerAddress.Normalize(address, _defaultPort),
            Tags = tag,
            Order = _state.LocalServers.Count,
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

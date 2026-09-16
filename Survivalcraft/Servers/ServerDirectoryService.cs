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
                Subscriptions = _state.Subscriptions.ToArray()
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

    public bool IsFavorite(string address)
    {
        var normalizedAddress = ServerAddress.Normalize(address, _defaultPort);
        lock (_gate)
        {
            return _state.Favorites.Any(entry => entry.Address == normalizedAddress);
        }
    }

    public void SetFavorite(string name, string address, bool favorite)
    {
        var normalizedAddress = ServerAddress.Normalize(address, _defaultPort);
        lock (_gate)
        {
            var existing = _state.Favorites.FirstOrDefault(entry => entry.Address == normalizedAddress);
            if (favorite && existing is null)
            {
                var entry = CreateServer(name, normalizedAddress, _state.Favorites.Count);
                Commit(_state with { Favorites = _state.Favorites.Append(entry).ToArray() });
            }
            else if (!favorite && existing is not null)
            {
                Commit(_state with
                {
                    Favorites = Reorder(_state.Favorites.Where(entry => entry.Id != existing.Id))
                });
            }
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

    public ServerSourceSubscription AddSubscription(ServerSourceSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        lock (_gate)
        {
            var candidate = subscription with { Order = _state.Subscriptions.Count };
            var normalized = NormalizeSubscription(candidate);
            if (_state.Subscriptions.Any(item => item.ApiUrl == normalized.ApiUrl))
            {
                throw new ArgumentException("A subscription with this URL already exists.", nameof(subscription));
            }

            Commit(_state with { Subscriptions = _state.Subscriptions.Append(normalized).ToArray() });
            return normalized;
        }
    }

    public void EditSubscription(ServerSourceSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        lock (_gate)
        {
            EnsureSubscriptionExists(subscription.Id);
            var normalized = NormalizeSubscription(subscription);
            if (_state.Subscriptions.Any(item => item.Id != normalized.Id && item.ApiUrl == normalized.ApiUrl))
            {
                throw new ArgumentException("A subscription with this URL already exists.", nameof(subscription));
            }

            Commit(_state with
            {
                Subscriptions = _state.Subscriptions.Select(item => item.Id == normalized.Id ? normalized : item)
                    .ToArray()
            });
        }
    }

    public void DeleteSubscription(Guid id)
    {
        lock (_gate)
        {
            EnsureSubscriptionExists(id);
            Commit(_state with
            {
                Subscriptions = ReorderSubscriptions(_state.Subscriptions.Where(item => item.Id != id))
            });
        }
    }

    public void SetSubscriptionOrder(IReadOnlyList<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_gate)
        {
            if (ids.Count != _state.Subscriptions.Count || ids.Distinct().Count() != ids.Count ||
                ids.Any(id => _state.Subscriptions.All(item => item.Id != id)))
            {
                throw new ArgumentException("Subscription order must contain every subscription exactly once.",
                    nameof(ids));
            }

            var byId = _state.Subscriptions.ToDictionary(item => item.Id);
            Commit(_state with
            {
                Subscriptions = ids.Select((id, order) => byId[id] with { Order = order }).ToArray()
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
            Subscriptions = NormalizeSubscriptions(state.Subscriptions)
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

    private static IReadOnlyList<ServerSourceSubscription> NormalizeSubscriptions(
        IEnumerable<ServerSourceSubscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        var ids = new HashSet<Guid>();
        var urls = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<ServerSourceSubscription>();
        foreach (var subscription in subscriptions.OrderBy(item => item.Order))
        {
            var normalized = NormalizeSubscription(subscription) with { Order = result.Count };
            if (!ids.Add(normalized.Id) || !urls.Add(normalized.ApiUrl))
            {
                throw new ArgumentException("Subscription IDs and URLs must be unique.");
            }

            result.Add(normalized);
        }

        return result;
    }

    private static ServerSourceSubscription NormalizeSubscription(ServerSourceSubscription subscription)
    {
        if (subscription.Id == Guid.Empty)
        {
            throw new ArgumentException("Subscription must have a stable nonempty ID.");
        }

        var name = NormalizeName(subscription.Name);
        if (!Uri.TryCreate(subscription.ApiUrl.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Subscription URL must be an absolute HTTP(S) URL without credentials or fragment.");
        }

        return subscription with
        {
            RegistrationId = string.IsNullOrWhiteSpace(subscription.RegistrationId)
                ? null
                : subscription.RegistrationId.Trim(),
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

    private static IReadOnlyList<ServerSourceSubscription> ReorderSubscriptions(
        IEnumerable<ServerSourceSubscription> subscriptions)
    {
        return subscriptions.Select((subscription, order) => subscription with { Order = order }).ToArray();
    }

    private static void EnsureExists(IEnumerable<StoredServerEntry> entries, Guid id)
    {
        if (entries.All(entry => entry.Id != id))
        {
            throw new KeyNotFoundException("Server entry does not exist.");
        }
    }

    private void EnsureSubscriptionExists(Guid id)
    {
        if (_state.Subscriptions.All(subscription => subscription.Id != id))
        {
            throw new KeyNotFoundException("Server source subscription does not exist.");
        }
    }

    private void Commit(ServerDirectoryState state)
    {
        var normalized = Normalize(state, _defaultPort);
        _save(normalized);
        _state = normalized;
    }
}

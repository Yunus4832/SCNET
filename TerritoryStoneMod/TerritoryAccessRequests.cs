using Game.Protocol.Identity;

namespace TerritoryStoneMod;

public sealed class TerritoryAccessRequests
{
    public const double Lifetime = 120;

    public const double Cooldown = 30;

    private readonly Dictionary<Guid, TerritoryAccessRequest> _pending = new();

    private readonly Dictionary<PlayerAddress, double> _lastRequest = new();

    public IReadOnlyCollection<TerritoryAccessRequest> Pending => _pending.Values;

    public bool TryAdd(
        PlayerAddress owner,
        PlayerAddress user,
        string name,
        double now,
        out TerritoryAccessRequest? request,
        out string error
    )
    {
        request = null;
        if (owner == default || user == default || owner == user)
        {
            error = "InvalidRequest";
            return false;
        }

        if (_pending.Values.Any(item => item.User == user && item.ExpiresAt > now))
        {
            error = "AlreadyPending";
            return false;
        }

        if (_lastRequest.TryGetValue(user, out var last) && now - last < Cooldown)
        {
            error = "RateLimited";
            return false;
        }

        if (_pending.Count >= 128 || _pending.Values.Count(item => item.Owner == owner) >= 8)
        {
            error = "QueueFull";
            return false;
        }

        request = new TerritoryAccessRequest(Guid.NewGuid(), owner, user, name, now + Lifetime);
        _pending.Add(request.Id, request);
        _lastRequest[user] = now;
        error = string.Empty;
        return true;
    }

    public bool TryResolve(
        Guid id,
        PlayerAddress owner,
        TerritoryPermission permissions,
        double now,
        out TerritoryAccessRequest? request
    )
    {
        request = null;
        if (!TerritoryPermissions.IsValid(permissions) ||
            !_pending.TryGetValue(id, out var found) ||
            found.Owner != owner ||
            found.ExpiresAt <= now)
        {
            return false;
        }

        request = found;
        _pending.Remove(id);
        return true;
    }

    public TerritoryAccessRequest[] Expire(double now, Func<TerritoryAccessRequest, bool> unavailable)
    {
        var removed = _pending.Values.Where(item => item.ExpiresAt <= now || unavailable(item)).ToArray();
        foreach (var request in removed)
        {
            _pending.Remove(request.Id);
        }

        foreach (var user in _lastRequest.Where(pair => now - pair.Value >= Cooldown).Select(pair => pair.Key)
                     .ToArray())
        {
            _lastRequest.Remove(user);
        }

        return removed;
    }

    public void Clear()
    {
        _pending.Clear();
        _lastRequest.Clear();
    }
}

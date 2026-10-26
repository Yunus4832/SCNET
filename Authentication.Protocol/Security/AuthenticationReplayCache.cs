namespace SCNET.Authentication.Protocol.Security;

/// <summary>Bounded provider-side proof replay protection. Never evicts an unexpired accepted proof.</summary>
public sealed class AuthenticationReplayCache(int capacity = 10000)
{
    private readonly Lock _gate = new();

    private readonly Dictionary<(string Subject, string RequestId), long> _accepted = new();

    public bool TryAccept(string subject, string requestId, long expiresAt, DateTimeOffset now)
    {
        AuthenticationNonce.Validate(requestId);
        var timestamp = now.ToUnixTimeSeconds();
        if (expiresAt <= timestamp ||
            expiresAt >
            timestamp +
            AuthenticationProtocol.MaximumRequestLifetimeSeconds +
            AuthenticationProtocol.ClockSkewSeconds)
        {
            return false;
        }

        lock (_gate)
        {
            foreach (var entry in _accepted.Where(entry => entry.Value <= timestamp).ToArray())
            {
                _accepted.Remove(entry.Key);
            }

            if (_accepted.Count >= capacity) { return false; }

            return _accepted.TryAdd((subject, requestId), expiresAt);
        }
    }
}

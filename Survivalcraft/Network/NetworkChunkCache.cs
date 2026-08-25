using Game.Network.Serialization;
using Game.Terrains.Distribution;

namespace Game.Network;

public sealed class NetworkChunkCache(long capacityBytes = 64L * 1024 * 1024)
{
    private sealed class Entry
    {
        public required EncodedTerrainChunk Encoded;
        public long LastAccess;
    }

    private readonly Dictionary<Point2, Entry> _entries = [];
    private readonly Lock _lock = new();
    private long _accessCounter;
    private long _size;

    public EncodedTerrainChunk GetOrEncode(AuthorityChunkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (TryGet(snapshot.Coords, snapshot.ContentVersion, out var cached))
        {
            return cached;
        }

        var encoded = NetworkChunkCodec.Encode(snapshot);
        Store(encoded);
        return encoded;
    }

    public void Store(EncodedTerrainChunk encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        lock (_lock)
        {
            if (_entries.Remove(encoded.Coords, out var previous))
            {
                _size -= previous.Encoded.Payload.Length;
            }

            _entries[encoded.Coords] = new Entry
            {
                Encoded = encoded,
                LastAccess = ++_accessCounter
            };
            _size += encoded.Payload.Length;
            Trim();
        }
    }

    public bool TryGet(Point2 coords, long contentVersion, out EncodedTerrainChunk encoded)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(coords, out var cached) &&
                cached.Encoded.ContentVersion == contentVersion)
            {
                cached.LastAccess = ++_accessCounter;
                encoded = cached.Encoded;
                return true;
            }
        }

        encoded = null!;
        return false;
    }

    public void Remove(Point2 coords)
    {
        lock (_lock)
        {
            if (_entries.Remove(coords, out var entry))
            {
                _size -= entry.Encoded.Payload.Length;
            }
        }
    }

    private void Trim()
    {
        while (_size > capacityBytes && _entries.Count > 0)
        {
            var oldest = _entries.MinBy(pair => pair.Value.LastAccess);
            _entries.Remove(oldest.Key);
            _size -= oldest.Value.Encoded.Payload.Length;
        }
    }
}

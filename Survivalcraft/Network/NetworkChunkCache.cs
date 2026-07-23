using Game.Network.Serialization;

namespace Game.Network;

public sealed class NetworkChunkCache(long capacityBytes = 64L * 1024 * 1024)
{
    private sealed class Entry
    {
        public required TerrainChunk Chunk;
        public required long Revision;
        public required EncodedTerrainChunk Encoded;
        public long LastAccess;
    }

    private readonly Dictionary<Point2, Entry> _entries = [];
    private readonly Lock _lock = new();
    private long _accessCounter;
    private long _size;

    public EncodedTerrainChunk GetOrEncode(TerrainChunk chunk)
    {
        var revision = chunk.NetworkContentRevision;
        lock (_lock)
        {
            if (_entries.TryGetValue(chunk.Coords, out var cached) &&
                ReferenceEquals(cached.Chunk, chunk) && cached.Revision == revision)
            {
                cached.LastAccess = ++_accessCounter;
                return cached.Encoded;
            }
        }

        EncodedTerrainChunk encoded = null!;
        var stable = false;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            revision = chunk.NetworkContentRevision;
            encoded = NetworkChunkCodec.Encode(chunk);
            if (revision != chunk.NetworkContentRevision)
            {
                continue;
            }

            stable = true;
            break;
        }

        // A continuously changing chunk is still safe to send as a snapshot, but must not be cached.
        if (!stable)
        {
            return encoded;
        }

        lock (_lock)
        {
            if (_entries.Remove(chunk.Coords, out var previous))
            {
                _size -= previous.Encoded.Payload.Length;
            }

            _entries[chunk.Coords] = new Entry
            {
                Chunk = chunk,
                Revision = revision,
                Encoded = encoded,
                LastAccess = ++_accessCounter
            };
            _size += encoded.Payload.Length;
            Trim();
        }

        return encoded;
    }

    public void Remove(TerrainChunk chunk)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(chunk.Coords, out var entry) || !ReferenceEquals(entry.Chunk, chunk))
            {
                return;
            }

            _entries.Remove(chunk.Coords);
            _size -= entry.Encoded.Payload.Length;
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

using System.Diagnostics.CodeAnalysis;

namespace Game.Terrains.Rendering;

/// <summary>Per-client-world ownership of chunk presentation resources.</summary>
/// <remarks>Release, renewal and disposal require the terrain worker to be synchronized or stopped.</remarks>
public sealed class TerrainChunkPresentationStore : IDisposable
{
    private readonly Dictionary<TerrainChunk, (ulong Generation, TerrainChunkPresentation Presentation)> _entries =
        new();

    private readonly object _sync = new();

    private bool _disposed;

    public bool TryGet(TerrainChunk chunk, [NotNullWhen(true)] out TerrainChunkPresentation? presentation)
    {
        lock (_sync)
        {
            presentation = null;
            if (_disposed ||
                chunk.IsDisposed ||
                !_entries.TryGetValue(chunk, out var entry) ||
                entry.Generation != chunk.AllocationGeneration)
            {
                return false;
            }

            presentation = entry.Presentation;
            return true;
        }
    }

    public TerrainChunkPresentation Get(TerrainChunk chunk) => Get(chunk, chunk.AllocationGeneration);

    /// <summary>Rejects work that still addresses an older allocation of a retained chunk.</summary>
    public TerrainChunkPresentation Get(TerrainChunk chunk, ulong allocationGeneration)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (allocationGeneration != chunk.AllocationGeneration)
            {
                throw new InvalidOperationException("Cannot access presentation through a stale allocation.");
            }

            if (chunk.IsDisposed)
            {
                throw new InvalidOperationException("Cannot recreate presentation for a released chunk.");
            }

            if (_entries.TryGetValue(chunk, out var entry))
            {
                if (entry.Generation != chunk.AllocationGeneration)
                {
                    throw new InvalidOperationException(
                        "Chunk allocation changed without renewing presentation ownership."
                    );
                }

                return entry.Presentation;
            }

            var presentation = new TerrainChunkPresentation();
            _entries.Add(chunk, (chunk.AllocationGeneration, presentation));
            return presentation;
        }
    }

    public void RenewAllocation(TerrainChunk chunk, ulong previousGeneration)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_entries.TryGetValue(chunk, out var entry)) { return; }

            if (entry.Generation != previousGeneration || chunk.AllocationGeneration <= previousGeneration)
            {
                throw new InvalidOperationException("Cannot renew stale chunk presentation ownership.");
            }

            _entries[chunk] = (chunk.AllocationGeneration, entry.Presentation);
        }
    }

    public void Release(TerrainChunk chunk)
    {
        lock (_sync)
        {
            if (_entries.Remove(chunk, out var entry))
            {
                entry.Presentation.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) { return; }

            _disposed = true;
            foreach (var entry in _entries.Values)
            {
                entry.Presentation.Dispose();
            }

            _entries.Clear();
        }
    }
}

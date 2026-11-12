using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Game.Terrains.Lighting;

/// <summary>Owns immutable light publications for one world's terrain allocations.</summary>
public sealed class TerrainLightingStore(Terrain terrain) : IDisposable
{
    private readonly ConcurrentDictionary<TerrainChunk, Publication> _publications = new();

    private readonly ConcurrentDictionary<TerrainChunk, SeedPublication> _seeds = new();

    private readonly object _sync = new();

    private bool _disposed;

    private sealed record Publication(TerrainLightInput Input, TerrainLightSnapshot Snapshot);

    private sealed record SeedPublication(TerrainLightInput Input, TerrainLightSeed Seed);

    public TerrainLightInput CaptureInput(TerrainChunk chunk)
    {
        RequireOwnedChunk(chunk);
        return new TerrainLightInput(
            chunk.AllocationGeneration,
            chunk.NetworkContentRevision,
            Volatile.Read(ref chunk.NetworkContentVersion),
            chunk.DerivedRevisions.HeightRevision,
            chunk.DerivedRevisions.Capture()
        );
    }

    public bool TryPublishSeed(TerrainChunk chunk, TerrainLightInput input, TerrainLightSeed seed)
    {
        RequireOwnedChunk(chunk);
        ArgumentNullException.ThrowIfNull(seed);
        lock (_sync)
        {
            if (_disposed || !chunk.HasContents || !MatchesSeed(chunk, input)) { return false; }

            _seeds[chunk] = new SeedPublication(input, seed);
            return true;
        }
    }

    public bool TryGetSeed(TerrainChunk chunk, [NotNullWhen(true)] out TerrainLightSeed? seed)
    {
        RequireOwnedChunk(chunk);
        seed = null;
        if (Volatile.Read(ref _disposed) ||
            !_seeds.TryGetValue(chunk, out var publication) ||
            !MatchesSeed(chunk, publication.Input)) { return false; }

        seed = publication.Seed;
        return true;
    }

    public bool TryGetPropagationSnapshot(TerrainChunk chunk, [NotNullWhen(true)] out TerrainLightSnapshot? snapshot)
    {
        RequireOwnedChunk(chunk);
        snapshot = null;
        if (Volatile.Read(ref _disposed)) { return false; }

        if (_publications.TryGetValue(chunk, out var publication) && MatchesSeed(chunk, publication.Input))
        {
            snapshot = publication.Snapshot;
            return true;
        }

        if (!TryGetSeed(chunk, out var seed)) { return false; }

        snapshot = seed.Light;
        return true;
    }

    public bool TryPublish(TerrainChunk chunk, TerrainLightInput input, TerrainLightSnapshot snapshot)
    {
        RequireOwnedChunk(chunk);
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_sync)
        {
            if (_disposed || !chunk.HasContents || !Matches(chunk, input)) { return false; }

            _publications[chunk] = new Publication(input, snapshot);
            return true;
        }
    }

    public bool TryGetCurrentSnapshot(TerrainChunk chunk, [NotNullWhen(true)] out TerrainLightSnapshot? snapshot)
    {
        RequireOwnedChunk(chunk);
        snapshot = null;
        if (Volatile.Read(ref _disposed) ||
            !chunk.HasCurrentLight ||
            !_publications.TryGetValue(chunk, out var publication) ||
            !Matches(chunk, publication.Input))
        {
            return false;
        }

        snapshot = publication.Snapshot;
        return true;
    }

    public bool TryGetPublishedSnapshot(TerrainChunk chunk, [NotNullWhen(true)] out TerrainLightSnapshot? snapshot)
    {
        RequireOwnedChunk(chunk);
        snapshot = null;
        if (Volatile.Read(ref _disposed) ||
            chunk.IsDisposed ||
            !_publications.TryGetValue(chunk, out var publication) ||
            publication.Input.Allocation != chunk.AllocationGeneration)
        {
            return false;
        }

        snapshot = publication.Snapshot;
        return true;
    }

    public bool TryGetEffectiveLight(int x, int y, int z, TerrainLightEnvironment environment, out int light)
    {
        light = 0;
        if (Volatile.Read(ref _disposed)) { return false; }

        if (y is < 0 or >= 256) { return true; }

        var chunk = terrain.GetChunkAtCell(x, z, false);
        if (chunk == null || !TryGetCurrentSnapshot(chunk, out var snapshot)) { return false; }

        light = snapshot.Get(x & 15, y, z & 15).GetEffectiveLight(environment);
        return true;
    }

    public void Release(TerrainChunk chunk)
    {
        RequireOwnedChunk(chunk);
        lock (_sync)
        {
            _publications.TryRemove(chunk, out _);
            _seeds.TryRemove(chunk, out _);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            Volatile.Write(ref _disposed, true);
            _publications.Clear();
            _seeds.Clear();
        }
    }

    private static bool MatchesSeed(TerrainChunk chunk, TerrainLightInput input)
    {
        var current = chunk.DerivedRevisions.Capture();
        return !chunk.IsDisposed &&
               chunk.HasContents &&
               chunk.AllocationGeneration == input.Allocation &&
               chunk.NetworkContentRevision == input.ContentRevision &&
               Volatile.Read(ref chunk.NetworkContentVersion) == input.ContentVersion &&
               chunk.DerivedRevisions.HeightRevision == input.HeightRevision &&
               current.Heights == input.Request.Heights &&
               current.Seed == input.Request.Seed;
    }

    private static bool Matches(TerrainChunk chunk, TerrainLightInput input)
    {
        return !chunk.IsDisposed &&
               chunk.AllocationGeneration == input.Allocation &&
               chunk.NetworkContentRevision == input.ContentRevision &&
               Volatile.Read(ref chunk.NetworkContentVersion) == input.ContentVersion &&
               chunk.DerivedRevisions.HeightRevision == input.HeightRevision &&
               chunk.DerivedRevisions.Capture() == input.Request;
    }

    private void RequireOwnedChunk(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (!ReferenceEquals(chunk.Terrain, terrain))
        {
            throw new ArgumentException("A light publication belongs to its owning terrain.", nameof(chunk));
        }
    }
}

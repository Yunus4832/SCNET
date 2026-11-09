using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Game.TerrainSerializers;

namespace Game.Terrains.Distribution;

/// <summary>Acquires authoritative contents and hands generated results to the world's publication owner.</summary>
/// <remarks>Generation and publication operations are serialized by the owning terrain executor.</remarks>
public sealed class AuthoritativeChunkGenerationPipeline(
    ITerrainContentsGenerator generator,
    Func<TerrainChunk, bool> restorePendingSave,
    Func<TerrainChunk, bool> load
) : IDisposable
{
    private readonly ITerrainContentsGenerator _generator =
        generator ?? throw new ArgumentNullException(nameof(generator));

    private readonly Func<TerrainChunk, bool> _load = load ?? throw new ArgumentNullException(nameof(load));

    private readonly Func<TerrainChunk, bool> _restorePendingSave =
        restorePendingSave ?? throw new ArgumentNullException(nameof(restorePendingSave));

    private readonly ConditionalWeakTable<TerrainChunk, GenerationProgress> _progress = new();

    private readonly ConcurrentQueue<Publication> _publications = new();

    private int _disposed;

    /// <summary>Thread-safe availability hint; the owner still takes and validates tickets under terrain access.</summary>
    public bool HasPendingPublications => Volatile.Read(ref _disposed) == 0 && !_publications.IsEmpty;

    public TerrainGenerationStage GetStage(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return chunk.HasContents ? TerrainGenerationStage.Complete : GetProgress(chunk).Stage;
    }

    public bool TryAdvance(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ObjectDisposedException.ThrowIf(chunk.IsDisposed, chunk);
        if (chunk.HasContents) { return false; }

        var progress = GetProgress(chunk);
        switch (progress.Stage)
        {
            case TerrainGenerationStage.Restore:
                if (_restorePendingSave(chunk) || _load(chunk))
                {
                    progress.Stage = TerrainGenerationStage.Complete;
                    chunk.ContentOrigin = TerrainContentOrigin.Storage;
                    chunk.HasContents = true;
                }
                else
                {
                    chunk.ContentOrigin = TerrainContentOrigin.Generated;
                    progress.Stage = TerrainGenerationStage.FirstPass;
                }

                return true;

            case TerrainGenerationStage.FirstPass:
                if (_generator.TryTakeSeedGeneratedChunkBasis(chunk))
                {
                    progress.Stage = TerrainGenerationStage.FourthPass;
                }
                else
                {
                    _generator.GenerateChunkContentsPass1(chunk);
                    progress.Stage = TerrainGenerationStage.SecondPass;
                }

                return true;

            case TerrainGenerationStage.SecondPass:
                _generator.GenerateChunkContentsPass2(chunk);
                progress.Stage = TerrainGenerationStage.ThirdPass;
                return true;

            case TerrainGenerationStage.ThirdPass:
                _generator.GenerateChunkContentsPass3(chunk);
                progress.Stage = TerrainGenerationStage.FourthPass;
                return true;

            case TerrainGenerationStage.FourthPass:
                _generator.GenerateChunkContentsPass4(chunk);
                progress.Stage = TerrainGenerationStage.Publication;
                progress.Publication = new Publication(this, chunk, chunk.AllocationGeneration);
                _publications.Enqueue(progress.Publication);
                return true;

            default:
                return false;
        }
    }

    public bool TryTakePublication([NotNullWhen(true)] out Publication? publication, int maximumCandidates = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCandidates);
        publication = null;
        if (Volatile.Read(ref _disposed) != 0) { return false; }

        for (var examined = 0; examined < maximumCandidates && _publications.TryDequeue(out var candidate); examined++)
        {
            var chunk = candidate.Chunk;
            if (chunk.IsDisposed ||
                chunk.HasContents ||
                chunk.AllocationGeneration != candidate.Generation ||
                !_progress.TryGetValue(chunk, out var progress) ||
                progress.Generation != candidate.Generation ||
                progress.Stage != TerrainGenerationStage.Publication ||
                !ReferenceEquals(progress.Publication, candidate))
            {
                candidate.TryConsume();
                continue;
            }

            progress.Stage = TerrainGenerationStage.Publishing;
            publication = candidate;
            return true;
        }

        return false;
    }

    /// <summary>Consumes one taken publication; stale tickets never reset a newer allocation's progress.</summary>
    public bool CompletePublication(Publication publication, bool accepted)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if (!publication.BelongsTo(this) || !publication.TryConsume()) { return false; }

        var chunk = publication.Chunk;
        if (Volatile.Read(ref _disposed) != 0 ||
            chunk.IsDisposed ||
            chunk.AllocationGeneration != publication.Generation ||
            !_progress.TryGetValue(chunk, out var progress) ||
            progress.Generation != publication.Generation ||
            progress.Stage != TerrainGenerationStage.Publishing ||
            !ReferenceEquals(progress.Publication, publication))
        {
            return false;
        }

        progress.Publication = null;
        progress.Stage = accepted ? TerrainGenerationStage.Complete : TerrainGenerationStage.Rejected;
        if (!accepted) { return false; }

        chunk.ContentOrigin = TerrainContentOrigin.Generated;
        chunk.HasContents = true;
        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }

        _publications.Clear();
        _progress.Clear();
    }

    private GenerationProgress GetProgress(TerrainChunk chunk)
    {
        var progress = _progress.GetValue(chunk, static _ => new GenerationProgress());
        if (progress.Generation != chunk.AllocationGeneration)
        {
            progress.Generation = chunk.AllocationGeneration;
            progress.Stage = TerrainGenerationStage.Restore;
            progress.Publication = null;
        }

        return progress;
    }

    /// <summary>One generated allocation awaiting a business publication decision.</summary>
    public sealed class Publication
    {
        private readonly AuthoritativeChunkGenerationPipeline _owner;

        private int _consumed;

        public TerrainChunk Chunk { get; }

        public ulong Generation { get; }

        internal Publication(
            AuthoritativeChunkGenerationPipeline owner,
            TerrainChunk chunk,
            ulong generation
        )
        {
            _owner = owner;
            Chunk = chunk;
            Generation = generation;
        }

        internal bool BelongsTo(AuthoritativeChunkGenerationPipeline owner) => ReferenceEquals(_owner, owner);

        internal bool TryConsume() => Interlocked.Exchange(ref _consumed, 1) == 0;
    }

    private sealed class GenerationProgress
    {
        internal ulong Generation { get; set; }

        internal TerrainGenerationStage Stage { get; set; }

        internal Publication? Publication { get; set; }
    }
}

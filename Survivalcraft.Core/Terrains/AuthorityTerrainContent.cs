using Game.Terrains.Distribution;
using Game.Terrains.Execution;

namespace Game.Terrains;

/// <summary>Publishes generated contents through business hooks on the owning world's update thread.</summary>
public sealed class AuthorityTerrainContent(
    SubsystemTerrain terrain,
    AuthoritativeChunkGenerationPipeline generation
) : IDisposable
{
    public AuthoritativeChunkGenerationPipeline Generation { get; } = generation;

    public event Action<TerrainChunk>? ContentPublished;

    private TerrainMainWorkRequest? _publicationRequest;

    private AuthoritativeChunkGenerationPipeline.Publication? _pendingPublication;

    private bool _pendingAccepted;

    private int _disposed;

    private int _publishing;

    public int PublishGeneratedContents(int maximumCount = 8)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        if (!CanPublish() || Interlocked.CompareExchange(ref _publishing, 1, 0) != 0) { return 0; }

        var published = 0;
        var defer = false;
        try
        {
            if (terrain.Access.IsHeldByCurrentThread)
            {
                throw new InvalidOperationException(
                    "Generated content must be published outside a terrain access lease."
                );
            }

            for (var attempt = 0; attempt < maximumCount && CanPublish(); attempt++)
            {
                if (_pendingPublication == null && !Generation.HasPendingPublications) { break; }

                var request = _publicationRequest ??= terrain.WorkExecutor.CreateMainRequest();
                if (_pendingPublication == null)
                {
                    if (!request.TryEnter())
                    {
                        defer = true;
                        break;
                    }

                    if (!Generation.TryTakePublication(out var publication)) { break; }

                    _pendingPublication = publication;
                    _pendingAccepted = false;
                    // Keep the worker paused across callbacks and commit, without holding its access gate.
                    request.YieldAccess();
                    try
                    {
                        if (!ReferenceEquals(publication.Chunk.Terrain, terrain.Terrain))
                        {
                            throw new InvalidOperationException(
                                "Generated content publication belongs to another terrain."
                            );
                        }

                        // Hook exception isolation is unchanged. Never repeat a hook while awaiting commit.
                        WorldRuleHooks.For(terrain.Project)
                            .Invoke(new TerrainChunkGeneratedContext(terrain, publication.Chunk));
                        _pendingAccepted = true;
                    }
                    catch
                    {
                        if (CanPublish()) { defer = !TryCompletePending(request, out _); }

                        throw;
                    }
                }

                if (!CanPublish()) { break; }

                if (!TryCompletePending(request, out var accepted))
                {
                    defer = true;
                    break;
                }

                if (accepted) { published++; }
            }
        }
        finally
        {
            if (!defer || !CanPublish()) { ReleaseRequest(); }

            Volatile.Write(ref _publishing, 0);
        }

        return published;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }

        _pendingPublication = null;
        ReleaseRequest();
        // Disposal may wait; the ordinary update pump never waits for terrain access.
        var entered = WorldRuntimeContext.IsProjectActive(terrain.Project) &&
                      terrain.WorkExecutor.TryEnterMain(Timeout.Infinite);
        try { Generation.Dispose(); }
        finally
        {
            if (entered) { terrain.WorkExecutor.LeaveMain(); }
        }
    }

    private bool TryCompletePending(TerrainMainWorkRequest request, out bool published)
    {
        published = false;
        if (!request.TryEnter()) { return false; }

        try
        {
            var publication = _pendingPublication!;
            _pendingPublication = null;
            published = Generation.CompletePublication(publication, _pendingAccepted && CanPublish());
            if (published) { ContentPublished?.Invoke(publication.Chunk); }

            return true;
        }
        finally
        {
            if (request.HasAccess) { request.YieldAccess(); }
        }
    }

    private void ReleaseRequest()
    {
        var request = _publicationRequest;
        _publicationRequest = null;
        if (request == null) { return; }

        if (request.HasAccess) { request.Leave(); }

        request.Dispose();
    }

    private bool CanPublish()
    {
        return Volatile.Read(ref _disposed) == 0 && WorldRuntimeContext.IsProjectActive(terrain.Project);
    }
}

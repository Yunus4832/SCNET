using Engine.Graphics;

namespace Game.Terrains.Rendering;

/// <summary>Client-owned meshes and GPU resources for one chunk allocation.</summary>
public sealed class TerrainChunkPresentation : IDisposable
{
    private DynamicArray<TerrainChunkGeometry.Buffer>? _buffers;

    public DynamicArray<TerrainChunkGeometry.Buffer> Buffers => _buffers ??= [];

    public TerrainGeometry[] ChunkSliceGeometries => field ??= new TerrainGeometry[TerrainChunk.SlicesCount];

    public float DrawDistanceSquared;

    private Dictionary<Texture2D, TerrainGeometry[]>? _draws;

    public Dictionary<Texture2D, TerrainGeometry[]> Draws => _draws ??= new();

    // Hashes describe the actual CPU Draws, independently of uploaded GPU buffers.
    public readonly int[] GeneratedSliceContentsHashes = new int[TerrainChunk.SlicesCount];

    private readonly bool[] _sliceGeometryValid = new bool[TerrainChunk.SlicesCount];

    public bool HasGeometry => _geometry is not null;

    private TerrainChunkGeometry? _geometry;

    public TerrainChunkGeometry Geometry => _geometry ??= new TerrainChunkGeometry();

    public readonly Dictionary<int, float> HazeEnds = new();

    public double NetworkGeometryReadyTime;

    public double NetworkGeometryUploadTime;

    public bool GeometryUploaded;

    public long ClientGeometryContentVersion => Volatile.Read(ref _completedMesh)?.ContentVersion ?? 0;

    private long _requestedMeshRevision;

    private MeshBuildStamp? _activeMesh;

    private MeshBuildStamp? _completedMesh;

    public TerrainMeshState WorkerMeshState { get; private set; }

    private sealed record MeshBuildStamp(ulong Generation, long ContentVersion, long Revision);

    public void RequestMeshRebuild()
    {
        Interlocked.Increment(ref _requestedMeshRevision);
    }

    public bool HasCurrentGeometry(TerrainChunk chunk)
    {
        var completed = Volatile.Read(ref _completedMesh);
        return completed is not null && Matches(chunk, completed);
    }

    private bool Matches(TerrainChunk chunk, MeshBuildStamp stamp) =>
        stamp.Generation == chunk.AllocationGeneration &&
        stamp.ContentVersion == chunk.NetworkContentVersion &&
        stamp.Revision == Volatile.Read(ref _requestedMeshRevision);

    /// <summary>Called only by the synchronized terrain worker.</summary>
    public TerrainMeshState PrepareMeshUpdate(TerrainChunk chunk)
    {
        if (_activeMesh is null || !Matches(chunk, _activeMesh))
        {
            _activeMesh = new MeshBuildStamp(
                chunk.AllocationGeneration,
                chunk.NetworkContentVersion,
                Volatile.Read(ref _requestedMeshRevision)
            );
            WorkerMeshState = TerrainMeshState.NeedsFirstPass;
        }

        return WorkerMeshState;
    }

    public void CompleteFirstPass() => WorkerMeshState = TerrainMeshState.NeedsSecondPass;

    /// <summary>Publishes only a build for the same allocation, content and rebuild request.</summary>
    public bool CompleteMeshBuild(TerrainChunk chunk)
    {
        if (_activeMesh is null || WorkerMeshState != TerrainMeshState.NeedsSecondPass || !Matches(chunk, _activeMesh))
        {
            return false;
        }

        WorkerMeshState = TerrainMeshState.Ready;
        Volatile.Write(ref _completedMesh, _activeMesh);
        NewGeometryData = true;
        return true;
    }


    public volatile bool NewGeometryData;

    public readonly int[] SliceContentsHashes = new int[TerrainChunk.SlicesCount];

    public void Dispose()
    {
        foreach (var buffer in _buffers ?? [])
        {
            buffer.Dispose();
        }

        _buffers?.Clear();
        var drawArray = _draws?.ToArray() ?? [];
        foreach (var draw in drawArray)
        {
            var drawValues = draw.Value.ToArray();
            foreach (var value in drawValues)
            {
                value.Dispose();
            }
        }

        _draws?.Clear();
        InvalidateSliceContentsHashes();
        GeometryUploaded = false;
        NewGeometryData = false;
        RequestMeshRebuild();
        Volatile.Write(ref _completedMesh, null);
        _activeMesh = null;
        WorkerMeshState = TerrainMeshState.NeedsFirstPass;
    }

    public void InvalidateSliceContentsHashes()
    {
        Array.Clear(_sliceGeometryValid);
    }

    internal bool BeginSliceGeometryUpdate(int slice, int contentsHash)
    {
        if (_sliceGeometryValid[slice] && GeneratedSliceContentsHashes[slice] == contentsHash)
        {
            return false;
        }

        // Invalidate before Draws can be cleared or partially generated, including throwing builds.
        _sliceGeometryValid[slice] = false;
        return true;
    }

    internal void CompleteSliceGeometryUpdate(int slice, int contentsHash)
    {
        GeneratedSliceContentsHashes[slice] = contentsHash;
        _sliceGeometryValid[slice] = true;
    }
}

using Engine.Core.Mathematics;

namespace Game.Protocol.Terrain;

public sealed record SubsystemTerrainPackage : IProtocolMessage
{
    public const int MaximumFragmentRequestsPerPackage = 1;

    public const int MaximumRequestedFragmentCount = ProtocolLimits.MaximumTerrainFragmentCount;

    public enum DataType
    {
        RequestSyncChunks = 0,

        RequestTerrainChunkFragments = 1,

        SyncTerrainChunkFragment = 2,

        SyncTerrainCellDelta = 3,

        CellEditCompleted = 4
    }

    public IReadOnlyList<ChunkContentRequest> ChunkRequests { get; init; } = [];

    public IReadOnlyList<TerrainChunkFragmentRequest> FragmentRequests { get; init; } = [];

    public EncodedTerrainChunkFragment ChunkFragment { get; init; }

    public TerrainCellDelta CellDelta { get; init; }

    public int EditRequestId { get; init; }

    public Point3 EditedCell { get; init; }

    public long EditContentVersion { get; init; }

    public DataType Type { get; init; }

    public byte ID => (byte)PackageType.SubsystemTerrain;

    public SubsystemTerrainPackage()
    {
    }

    public SubsystemTerrainPackage(IReadOnlyList<ChunkContentRequest> requests) => ChunkRequests = requests.ToArray();

    public SubsystemTerrainPackage(EncodedTerrainChunkFragment fragment)
    {
        Type = DataType.SyncTerrainChunkFragment;
        ChunkFragment = fragment;
    }

    public SubsystemTerrainPackage(TerrainCellDelta delta)
    {
        Type = DataType.SyncTerrainCellDelta;
        CellDelta = delta;
    }

    public static SubsystemTerrainPackage CreateFragmentRequest(IEnumerable<TerrainChunkFragmentRequest> requests) =>
        new()
            { Type = DataType.RequestTerrainChunkFragments, FragmentRequests = requests.ToArray() };
}

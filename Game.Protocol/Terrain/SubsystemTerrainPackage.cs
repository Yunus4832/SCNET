using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
[Union(0, typeof(TerrainCellDeltaPackage))]
[Union(1, typeof(TerrainCellEditCompletedPackage))]
[Union(2, typeof(TerrainChunksRequestPackage))]
[Union(3, typeof(TerrainFragmentPackage))]
[Union(4, typeof(TerrainFragmentsRequestPackage))]
public abstract record SubsystemTerrainPackage : IProtocolMessage
{
    public const int MaximumRequestedFragmentCount = ProtocolLimits.MaximumTerrainFragmentCount;

    public const int MaximumFragmentRequestsPerPackage = 1;

    public enum DataType
    {
        RequestSyncChunks = 0,

        RequestTerrainChunkFragments = 1,

        SyncTerrainChunkFragment = 2,

        SyncTerrainCellDelta = 3,

        CellEditCompleted = 4
    }

    [IgnoreMember] public abstract DataType Type { get; }


    public static TerrainFragmentsRequestPackage CreateFragmentRequest(
        IEnumerable<TerrainChunkFragmentRequest> requests
    ) =>
        new TerrainFragmentsRequestPackage { FragmentRequests = requests.ToArray() };
}

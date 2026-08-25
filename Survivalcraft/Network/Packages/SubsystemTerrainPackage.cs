using Game.Network.Enums;
using Game.Network.Serialization;
using Game.Terrains.Distribution;

namespace Game.Network.Packages;

public class SubsystemTerrainPackage : IPackage
{
    public const int MaximumFragmentRequestsPerPackage = 1;

    public const int MaximumRequestedFragmentCount = EncodedTerrainChunkFragmenter.MaximumFragmentCount;

    public enum DataType
    {
        RequestSyncChunks,
        RequestTerrainChunkFragments,
        SyncTerrainChunkFragment,
        ReplyResult,
        SyncTerrainCellDelta
    }

    public List<ChunkContentRequest> ChunkRequests = [];

    public List<ChunkAllocationId> FailedChunkRequests = [];

    public List<TerrainChunkFragmentRequest> FragmentRequests = [];

    public EncodedTerrainChunkFragment ChunkFragment;

    public TerrainCellDelta CellDelta;

    public DataType Type;

    public byte ID => (byte)PackageType.SubsystemTerrain;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public SubsystemTerrainPackage()
    {
    }

    public SubsystemTerrainPackage(List<ChunkContentRequest> requests)
    {
        Type = DataType.RequestSyncChunks;
        ChunkRequests.AddRange(requests);
    }

    public SubsystemTerrainPackage(List<ChunkAllocationId> failedRequests, byte r)
    {
        Type = DataType.ReplyResult;
        FailedChunkRequests.AddRange(failedRequests);
    }

    public SubsystemTerrainPackage(EncodedTerrainChunkFragment fragment)
    {
        Type = DataType.SyncTerrainChunkFragment;
        ChunkFragment = fragment;
    }

    public static SubsystemTerrainPackage CreateFragmentRequest(
        IEnumerable<TerrainChunkFragmentRequest> requests)
    {
        var package = new SubsystemTerrainPackage
        {
            Type = DataType.RequestTerrainChunkFragments
        };
        package.FragmentRequests.AddRange(requests);
        return package;
    }

    public SubsystemTerrainPackage(TerrainCellDelta delta)
    {
        Type = DataType.SyncTerrainCellDelta;
        CellDelta = delta;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write((byte)Type);
        switch (Type)
        {
            case DataType.RequestSyncChunks:
                writer.Write(ChunkRequests.Count);
                foreach (var request in ChunkRequests)
                {
                    writer.Write(request.Allocation.Coords);
                    writer.Write(request.Allocation.Generation);
                    writer.Write(request.KnownContentVersion);
                }

                break;
            case DataType.SyncTerrainChunkFragment:
                writer.Write(ChunkFragment.Allocation.Coords);
                writer.Write(ChunkFragment.Allocation.Generation);
                writer.Write(ChunkFragment.ContentVersion);
                writer.Write(ChunkFragment.TotalLength);
                writer.Write(ChunkFragment.FragmentIndex);
                writer.Write(ChunkFragment.FragmentCount);
                writer.Write((ushort)ChunkFragment.Payload.Length);
                writer.Write(ChunkFragment.Payload);
                break;
            case DataType.RequestTerrainChunkFragments:
                if (FragmentRequests.Count is <= 0 or > MaximumFragmentRequestsPerPackage)
                {
                    throw new InvalidDataException("Invalid terrain fragment request count.");
                }
                writer.Write((ushort)FragmentRequests.Count);
                foreach (var request in FragmentRequests)
                {
                    if (request.FragmentCount is 0 or > MaximumRequestedFragmentCount ||
                        request.MissingFragmentIndices.Length == 0 ||
                        request.MissingFragmentIndices.Any(index => index >= request.FragmentCount))
                    {
                        throw new InvalidDataException("Invalid missing terrain fragment metadata.");
                    }
                    writer.Write(request.Allocation.Coords);
                    writer.Write(request.Allocation.Generation);
                    writer.Write(request.ContentVersion);
                    writer.Write(request.FragmentCount);
                    var bitmap = new byte[(request.FragmentCount + 7) / 8];
                    foreach (var index in request.MissingFragmentIndices)
                    {
                        bitmap[index / 8] |= (byte)(1 << (index % 8));
                    }
                    writer.Write((ushort)bitmap.Length);
                    writer.Write(bitmap);
                }
                break;
            case DataType.SyncTerrainCellDelta:
                writer.Write(CellDelta.Cell);
                writer.Write(CellDelta.Value);
                writer.Write(CellDelta.BaseContentVersion);
                writer.Write(CellDelta.ResultContentVersion);
                break;
            case DataType.ReplyResult:
                writer.Write((ushort)FailedChunkRequests.Count);
                foreach (var allocation in FailedChunkRequests)
                {
                    writer.Write(allocation.Coords);
                    writer.Write(allocation.Generation);
                }

                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = (DataType)reader.ReadByte();
        switch (Type)
        {
            case DataType.RequestSyncChunks:
                ChunkRequests = [];
                var count = reader.ReadInt32();
                while (count-- > 0)
                {
                    ChunkRequests.Add(new ChunkContentRequest(
                        new ChunkAllocationId(reader.ReadPoint2(), reader.ReadUInt64()),
                        reader.ReadInt64()));
                }

                break;
            case DataType.SyncTerrainChunkFragment:
                var fragmentAllocation = new ChunkAllocationId(reader.ReadPoint2(), reader.ReadUInt64());
                var fragmentContentVersion = reader.ReadInt64();
                var totalLength = reader.ReadInt32();
                var fragmentIndex = reader.ReadUInt16();
                var fragmentCount = reader.ReadUInt16();
                var fragmentLength = reader.ReadUInt16();
                ChunkFragment = new EncodedTerrainChunkFragment(
                    fragmentAllocation,
                    fragmentContentVersion,
                    totalLength,
                    fragmentIndex,
                    fragmentCount,
                    reader.ReadBytes(fragmentLength));
                break;
            case DataType.RequestTerrainChunkFragments:
                FragmentRequests = [];
                var requestCount = reader.ReadUInt16();
                if (requestCount is 0 or > MaximumFragmentRequestsPerPackage)
                {
                    throw new InvalidDataException("Invalid terrain fragment request count.");
                }
                while (requestCount-- > 0)
                {
                    var allocation = new ChunkAllocationId(reader.ReadPoint2(), reader.ReadUInt64());
                    var contentVersion = reader.ReadInt64();
                    var requestedFragmentCount = reader.ReadUInt16();
                    var bitmapLength = reader.ReadUInt16();
                    var expectedBitmapLength = (requestedFragmentCount + 7) / 8;
                    if (requestedFragmentCount is 0 or > MaximumRequestedFragmentCount ||
                        bitmapLength != expectedBitmapLength)
                    {
                        throw new InvalidDataException("Invalid missing terrain fragment bitmap.");
                    }
                    var bitmap = reader.ReadBytes(bitmapLength);
                    var missing = new List<ushort>();
                    for (var index = 0; index < requestedFragmentCount; index++)
                    {
                        if ((bitmap[index / 8] & (1 << (index % 8))) != 0)
                        {
                            missing.Add((ushort)index);
                        }
                    }
                    if (missing.Count == 0)
                    {
                        throw new InvalidDataException("Missing terrain fragment bitmap is empty.");
                    }
                    FragmentRequests.Add(new TerrainChunkFragmentRequest(
                        allocation,
                        contentVersion,
                        requestedFragmentCount,
                        [.. missing]));
                }
                break;
            case DataType.SyncTerrainCellDelta:
                CellDelta = new TerrainCellDelta(
                    reader.ReadPoint3(),
                    reader.ReadInt32(),
                    reader.ReadInt64(),
                    reader.ReadInt64());
                break;
            case DataType.ReplyResult:
                FailedChunkRequests = [];
                var mc = reader.ReadUInt16();
                while (mc-- > 0)
                {
                    FailedChunkRequests.Add(new ChunkAllocationId(
                        reader.ReadPoint2(),
                        reader.ReadUInt64()));
                }

                break;
        }
    }

}

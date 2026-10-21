using Game.Protocol.Terrain;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteTerrain(SubsystemTerrainPackage message, ProtocolWriter writer)
    {
        writer.Write((byte)message.Type);
        switch (message.Type)
        {
            case SubsystemTerrainPackage.DataType.CellEditCompleted:
                writer.Write(message.EditRequestId);
                writer.Write(message.EditedCell);
                writer.Write(message.EditContentVersion);
                break;
            case SubsystemTerrainPackage.DataType.RequestSyncChunks:
                writer.Write(message.ChunkRequests.Count);
                foreach (var request in message.ChunkRequests)
                {
                    writer.Write(request.Allocation.Coords);
                    writer.Write(request.Allocation.Generation);
                    writer.Write(request.KnownContentVersion);
                }

                break;
            case SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment:
                writer.Write(message.ChunkFragment.Allocation.Coords);
                writer.Write(message.ChunkFragment.Allocation.Generation);
                writer.Write(message.ChunkFragment.ContentVersion);
                writer.Write(message.ChunkFragment.TotalLength);
                writer.Write(message.ChunkFragment.FragmentIndex);
                writer.Write(message.ChunkFragment.FragmentCount);
                writer.Write((ushort)message.ChunkFragment.Payload.Length);
                writer.Write(message.ChunkFragment.Payload);
                break;
            case SubsystemTerrainPackage.DataType.RequestTerrainChunkFragments:
                if (message.FragmentRequests.Count is <= 0
                    or > SubsystemTerrainPackage.MaximumFragmentRequestsPerPackage)
                {
                    throw new InvalidDataException("Invalid terrain fragment request count.");
                }

                writer.Write((ushort)message.FragmentRequests.Count);
                foreach (var request in message.FragmentRequests)
                {
                    if (request.FragmentCount is 0 or > SubsystemTerrainPackage.MaximumRequestedFragmentCount ||
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
            case SubsystemTerrainPackage.DataType.SyncTerrainCellDelta:
                writer.Write(message.CellDelta.Cell);
                writer.Write(message.CellDelta.Value);
                writer.Write(message.CellDelta.BaseContentVersion);
                writer.Write(message.CellDelta.ResultContentVersion);
                break;
            default: throw new InvalidDataException("Unknown terrain message mode.");
        }
    }

    private static SubsystemTerrainPackage ReadTerrain(ProtocolReader reader)
    {
        var type = (SubsystemTerrainPackage.DataType)reader.ReadByte();
        switch (type)
        {
            case SubsystemTerrainPackage.DataType.CellEditCompleted:
                return new SubsystemTerrainPackage
                {
                    Type = type, EditRequestId = reader.ReadInt32(), EditedCell = reader.ReadPoint3(),
                    EditContentVersion = reader.ReadInt64()
                };
            case SubsystemTerrainPackage.DataType.RequestSyncChunks:
                var count = reader.ReadCount(ProtocolFrameCodec.MaximumPayloadLength / 24);
                EnsureRemaining(reader, count * 24);
                var requests = new ChunkContentRequest[count];
                for (var i = 0; i < count; i++)
                {
                    requests[i] =
                        new ChunkContentRequest(
                            new ChunkAllocationId(reader.ReadPoint2(), reader.ReadUInt64()),
                            reader.ReadInt64()
                        );
                }

                return new SubsystemTerrainPackage(requests);
            case SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment:
                var allocation = new ChunkAllocationId(reader.ReadPoint2(), reader.ReadUInt64());
                var version = reader.ReadInt64();
                var total = reader.ReadInt32();
                var index = reader.ReadUInt16();
                var fragments = reader.ReadUInt16();
                var length = reader.ReadUInt16();
                return new SubsystemTerrainPackage(
                    new EncodedTerrainChunkFragment(
                        allocation,
                        version,
                        total,
                        index,
                        fragments,
                        reader.ReadBytes(length)
                    )
                );
            case SubsystemTerrainPackage.DataType.SyncTerrainCellDelta:
                return new SubsystemTerrainPackage(
                    new TerrainCellDelta(
                        reader.ReadPoint3(),
                        reader.ReadInt32(),
                        reader.ReadInt64(),
                        reader.ReadInt64()
                    )
                );
            case SubsystemTerrainPackage.DataType.RequestTerrainChunkFragments:
                var requestCount = reader.ReadUInt16();
                if (requestCount is 0 or > SubsystemTerrainPackage.MaximumFragmentRequestsPerPackage)
                {
                    throw new InvalidDataException("Invalid terrain fragment request count.");
                }

                var values = new TerrainChunkFragmentRequest[requestCount];
                for (var i = 0; i < requestCount; i++)
                {
                    var requestedAllocation = new ChunkAllocationId(reader.ReadPoint2(), reader.ReadUInt64());
                    var contentVersion = reader.ReadInt64();
                    var fragmentCount = reader.ReadUInt16();
                    var bitmapLength = reader.ReadUInt16();
                    if (fragmentCount is 0 or > SubsystemTerrainPackage.MaximumRequestedFragmentCount ||
                        bitmapLength != (fragmentCount + 7) / 8)
                    {
                        throw new InvalidDataException("Invalid missing terrain fragment bitmap.");
                    }

                    var bitmap = reader.ReadBytes(bitmapLength);
                    var missing = new List<ushort>();
                    for (var j = 0; j < fragmentCount; j++)
                    {
                        if ((bitmap[j / 8] & (1 << (j % 8))) != 0) { missing.Add((ushort)j); }
                    }

                    if (missing.Count == 0)
                    {
                        throw new InvalidDataException("Missing terrain fragment bitmap is empty.");
                    }

                    values[i] = new TerrainChunkFragmentRequest(
                        requestedAllocation,
                        contentVersion,
                        fragmentCount,
                        missing.ToArray()
                    );
                }

                return new SubsystemTerrainPackage { Type = type, FragmentRequests = values };
            default: throw new InvalidDataException("Unknown terrain message mode.");
        }
    }
}

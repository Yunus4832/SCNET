using EntitySystem.Core;

namespace Game.Network.Transport;

/// <summary>Outbound delivery from a logical world into its existing message owner.</summary>
public interface IWorldMessageSink
{
    void Send(IProtocolMessage message, PackageAudience audience);

    void SendNearPoint(Vector3 position, float radius, IProtocolMessage message);

    void SendToChunkObservers(Point2 chunk, IProtocolMessage message);

    void SendToObservers(Entity entity, IProtocolMessage message, WorldConnection? except);
    bool CanQueueTerrainSnapshot(WorldConnection recipient);

    /// <summary>True transfers the owned snapshot; false leaves its ownership with the caller.</summary>
    bool TryQueueOwnedTerrainSnapshot(WorldConnection recipient, TerrainSnapshotMessage message);

    void DiscardOlderChunkAllocations(WorldConnection recipient, ChunkAllocationId allocation);

    void TrimChunkDelivery(WorldConnection recipient, Vector2 center, float distance);

    void ReleaseChunkDelivery(WorldConnection recipient);

    void InvalidateChunkDelivery(Point2 coords);

    void DiscardQueuedChunkRequests(ChunkAllocationId allocation);
}

using EntitySystem.Core;

namespace Game.Network.Transport;

/// <summary>将逻辑世界的出站消息交给已有的消息管理者。</summary>
public interface IWorldMessageSink
{
    void Send(IProtocolMessage message, PackageAudience audience);

    void SendNearPoint(Vector3 position, float radius, IProtocolMessage message);

    void SendToChunkObservers(Point2 chunk, IProtocolMessage message);

    void SendToObservers(Entity entity, IProtocolMessage message, WorldConnection? except);
    bool CanQueueTerrainSnapshot(WorldConnection recipient);

    /// <summary>返回 true 时移交快照所有权；返回 false 时仍由调用方持有。</summary>
    bool TryQueueOwnedTerrainSnapshot(WorldConnection recipient, TerrainSnapshotMessage message);

    /// <summary>接收方队列有余量时移交光照更新或整批更新的所有权。</summary>
    bool TryQueueOwnedTerrainLighting(WorldConnection recipient, IProtocolMessage message);

    void DiscardOlderChunkAllocations(WorldConnection recipient, ChunkAllocationId allocation);

    void TrimChunkDelivery(WorldConnection recipient, Vector2 center, float distance);

    void ReleaseChunkDelivery(WorldConnection recipient);

    void InvalidateChunkDelivery(Point2 coords);

    void DiscardQueuedChunkRequests(ChunkAllocationId allocation);
}

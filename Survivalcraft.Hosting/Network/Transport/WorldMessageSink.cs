using EntitySystem.Core;

using Game.Network.Replication;

namespace Game.Network.Transport;

internal sealed class WorldMessageSink(WorldNetworkBinding binding) : IWorldMessageSink
{
    private Project Project => binding.World.Project ?? throw new InvalidOperationException("World has no project.");

    public void Send(IProtocolMessage package, PackageAudience audience)
    {
        if (!binding.World.IsClosed) { binding.RequireNode().QueuePackage(binding.World, package, audience); }
    }

    public void SendNearPoint(Vector3 position, float radius, IProtocolMessage package)
    {
        if (binding.World.IsClosed) { return; }

        var interest = Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        var observers = interest.GetObserversWithin(position.XZ, radius).ToArray();
        if (observers.Length > 0)
        {
            Send(package, PackageAudience.To(observers));
        }
    }

    public void SendToChunkObservers(Point2 chunk, IProtocolMessage package)
    {
        if (binding.World.IsClosed) { return; }

        var observers = Project.FindSubsystem<SubsystemNetworkInterest>(true)!.GetChunkObservers(chunk).ToArray();
        if (observers.Length > 0)
        {
            Send(package, PackageAudience.To(observers));
        }
    }

    public void SendToObservers(Entity entity, IProtocolMessage package, WorldConnection? except = null)
    {
        if (binding.World.IsClosed) { return; }

        if (!ReferenceEquals(entity.Project, binding.World.Project))
        {
            throw new InvalidOperationException("An entity message must use its own world endpoint.");
        }

        var body = entity.FindComponent<ComponentBody>();
        if (body is null)
        {
            throw new InvalidOperationException(
                $"Entity {entity.EntityId} has no body and cannot be routed by entity position."
            );
        }

        var interest = entity.Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        var candidates = interest.Entities.GetObservers(EntityInterestGroup.Creatures, entity.EntityId);
        if (body.Character?.CharacterData.ControllerConnection is { IsConnected: true } owner)
        {
            candidates = candidates.Append(owner);
        }

        var observers = candidates
            .Where(client => client.IsConnected)
            .Where(client => !ReferenceEquals(client, except))
            .Distinct<WorldConnection>(ReferenceEqualityComparer.Instance)
            .ToArray();
        if (observers.Length > 0)
        {
            if (package is ComponentMountPackage { Type: ComponentMountPackage.EventType.Mount })
            {
                // A newly spawned mount may not have entered the periodic baseline yet.
                // Queue missing dependencies on Bulk before the ordered mount event.
                foreach (var observer in observers)
                {
                    interest.EnsureBodyGroupObserved(observer, body);
                }
            }

            Send(package, PackageAudience.To(observers));
        }
    }

    // A captured old-world publisher must never operate on a replacement runtime of the same node.
    private NetNode? ChunkNode => ReferenceEquals(binding.Node?.Runtime, binding.World) ? binding.Node : null;

    public bool CanQueueTerrainSnapshot(WorldConnection recipient) =>
        !binding.World.IsClosed && ChunkNode?.CanQueueTerrainSnapshot(recipient) == true;

    public bool TryQueueOwnedTerrainSnapshot(WorldConnection recipient, TerrainSnapshotMessage message) =>
        !binding.World.IsClosed && ChunkNode?.TryQueueOwnedTerrainSnapshot(recipient, message) == true;

    public void DiscardOlderChunkAllocations(WorldConnection recipient, ChunkAllocationId allocation)
    {
        ChunkNode?.DiscardOlderChunkAllocations(recipient, allocation);
    }

    public void TrimChunkDelivery(WorldConnection recipient, Vector2 center, float distance)
    {
        ChunkNode?.TrimChunkDelivery(recipient, center, distance);
    }

    public void ReleaseChunkDelivery(WorldConnection recipient)
    {
        ChunkNode?.ReleaseChunkDelivery(recipient);
    }

    public void InvalidateChunkDelivery(Point2 coords)
    {
        ChunkNode?.InvalidateChunkDelivery(coords);
    }

    public void DiscardQueuedChunkRequests(ChunkAllocationId allocation)
    {
        ChunkNode?.DiscardQueuedChunkRequests(allocation);
    }
}

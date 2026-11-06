using Game.Terrains.Distribution;

namespace Game.Network.Handling;

public sealed class TerrainSnapshotMessageHandler : PackageHandlerBase<TerrainSnapshotMessage>
{
    public override void Handle(TerrainSnapshotMessage message, PackageReceiveContext context)
    {
        if (context.IsServer || context.Project is null) { return; }

        var terrain = context.Project.FindSubsystem<SubsystemTerrain>(true)!;
        var transport = terrain.ChunkContentTransport as ClientChunkContentTransport ??
                        throw new InvalidOperationException("Terrain snapshots require a client content transport.");
        transport.Receive(message.Snapshot, message.Reservation);
    }
}

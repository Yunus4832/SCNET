using Game.Terrains.Distribution;

namespace Game.Network.Packages.Handlers;

public sealed class SubsystemTerrainPackageHandler : PackageHandlerBase<SubsystemTerrainPackage>
{
    internal static bool AcceptsDirection(SubsystemTerrainPackage.DataType type, bool isServer)
    {
        return type switch
        {
            SubsystemTerrainPackage.DataType.RequestSyncChunks or
                SubsystemTerrainPackage.DataType.RequestTerrainChunkFragments => isServer,
            SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment or
                SubsystemTerrainPackage.DataType.SyncTerrainCellDelta => !isServer,
            _ => false
        };
    }

    public override void Handle(SubsystemTerrainPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (GameManager.Project is null || !AcceptsDirection(package.Type, isServer))
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemTerrain = project.FindSubsystem<SubsystemTerrain>(true)!;
        switch (package.Type)
        {
            case SubsystemTerrainPackage.DataType.RequestSyncChunks:
                if (context.Sender is null)
                {
                    break;
                }

                var scheduler = subsystemTerrain.TerrainUpdater.ServerChunkDistribution ??
                                throw new InvalidOperationException(
                                    "Terrain chunk requests require an authoritative server scheduler.");
                scheduler.Enqueue(context.Sender, package.ChunkRequests);
                break;
            case SubsystemTerrainPackage.DataType.RequestTerrainChunkFragments:
                if (context.Sender is null)
                {
                    break;
                }

                var fragmentScheduler = subsystemTerrain.TerrainUpdater.ServerChunkDistribution ??
                                        throw new InvalidOperationException(
                                            "Terrain fragment requests require an authoritative server scheduler.");
                fragmentScheduler.EnqueueMissing(context.Sender, package.FragmentRequests);
                break;
            case SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment:
                var transport = subsystemTerrain.ChunkContentTransport as NetworkChunkContentTransport ??
                                throw new InvalidOperationException(
                                    "Remote terrain snapshots require a network chunk transport.");
                transport.Receive(package.ChunkFragment);

                break;
            case SubsystemTerrainPackage.DataType.SyncTerrainCellDelta:
                var deltaTransport = subsystemTerrain.ChunkContentTransport as NetworkChunkContentTransport ??
                                     throw new InvalidOperationException(
                                         "Remote terrain cell deltas require a network chunk transport.");
                deltaTransport.Receive(package.CellDelta);
                break;
        }
    }
}

using Game.Terrains.Distribution;

namespace Game.Network.Packages.Handlers;

public sealed class SubsystemTerrainPackageHandler : PackageHandlerBase<SubsystemTerrainPackage>
{
    public override void Handle(SubsystemTerrainPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemTerrain = project.FindSubsystem<SubsystemTerrain>(true)!;
        switch (package.Type)
        {
            case SubsystemTerrainPackage.DataType.RequestSyncChunks:
                if (package.From is null)
                {
                    break;
                }

                var scheduler = subsystemTerrain.TerrainUpdater.ServerChunkDistribution ??
                                throw new InvalidOperationException(
                                    "Terrain chunk requests require an authoritative server scheduler.");
                scheduler.Enqueue(package.From, package.ChunkRequests);
                break;
            case SubsystemTerrainPackage.DataType.RequestTerrainChunkFragments:
                if (package.From is null)
                {
                    break;
                }

                var fragmentScheduler = subsystemTerrain.TerrainUpdater.ServerChunkDistribution ??
                                        throw new InvalidOperationException(
                                            "Terrain fragment requests require an authoritative server scheduler.");
                fragmentScheduler.EnqueueMissing(package.From, package.FragmentRequests);
                break;
            case SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment:
                var transport = subsystemTerrain.ChunkContentTransport as NetworkChunkContentTransport ??
                                throw new InvalidOperationException(
                                    "Remote terrain snapshots require a network chunk transport.");
                transport.Receive(package.ChunkFragment);

                break;
            case SubsystemTerrainPackage.DataType.SyncTerrainCellDelta:
                if (isServer)
                {
                    break;
                }

                var deltaTransport = subsystemTerrain.ChunkContentTransport as NetworkChunkContentTransport ??
                                     throw new InvalidOperationException(
                                         "Remote terrain cell deltas require a network chunk transport.");
                deltaTransport.Receive(package.CellDelta);
                break;
            case SubsystemTerrainPackage.DataType.ReplyResult:
                var failureTransport = subsystemTerrain.ChunkContentTransport as NetworkChunkContentTransport ??
                                       throw new InvalidOperationException(
                                           "Remote terrain failures require a network chunk transport.");
                failureTransport.ReceiveFailures(package.FailedChunkRequests);

                break;
        }
    }
}

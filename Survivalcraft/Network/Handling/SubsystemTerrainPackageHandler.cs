using Game.Network.Transport;
using Game.Terrains.Distribution;

namespace Game.Network.Handling;

public sealed class SubsystemTerrainPackageHandler : PackageHandlerBase<SubsystemTerrainPackage>
{
    internal static bool AcceptsDirection(SubsystemTerrainPackage.DataType type, bool isServer)
    {
        return type switch
        {
            SubsystemTerrainPackage.DataType.RequestSyncChunks or
                SubsystemTerrainPackage.DataType.RequestTerrainChunkFragments => isServer,
            SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment or
                SubsystemTerrainPackage.DataType.SyncTerrainCellDelta or
                SubsystemTerrainPackage.DataType.CellEditCompleted => !isServer,
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
        switch (package)
        {
            case TerrainCellEditCompletedPackage message1:
            {
                subsystemTerrain.CellPrediction?.Complete(
                    message1.EditRequestId,
                    message1.EditedCell,
                    message1.EditContentVersion
                );
                break;
            }
            case TerrainChunksRequestPackage message2:
            {
                if (context.Sender is null)
                {
                    break;
                }

                var scheduler = subsystemTerrain.TerrainUpdater.ServerChunkDistribution ??
                                throw new InvalidOperationException(
                                    "Terrain chunk requests require an authoritative server scheduler."
                                );
                scheduler.Enqueue(context.Sender, message2.ChunkRequests);
                break;
            }
            case TerrainFragmentsRequestPackage message3:
            {
                if (context.Sender is null)
                {
                    break;
                }

                var fragmentScheduler = subsystemTerrain.TerrainUpdater.ServerChunkDistribution ??
                                        throw new InvalidOperationException(
                                            "Terrain fragment requests require an authoritative server scheduler."
                                        );
                fragmentScheduler.EnqueueMissing(context.Sender, message3.FragmentRequests);
                break;
            }
            case TerrainFragmentPackage message4:
            {
                var transport = subsystemTerrain.ChunkContentTransport as NetworkChunkContentTransport ??
                                throw new InvalidOperationException(
                                    "Remote terrain snapshots require a network chunk transport."
                                );
                transport.Receive(message4.ChunkFragment);

                break;
            }
            case TerrainCellDeltaPackage message5:
            {
                var deltaTransport = subsystemTerrain.ChunkContentTransport as NetworkChunkContentTransport ??
                                     throw new InvalidOperationException(
                                         "Remote terrain cell deltas require a network chunk transport."
                                     );
                deltaTransport.Receive(message5.CellDelta);
                break;
            }
        }
    }
}

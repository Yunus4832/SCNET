using Game.Terrains.Distribution;

namespace Game.Network.Handling;

public sealed class SubsystemTerrainPackageHandler : PackageHandlerBase<SubsystemTerrainPackage>
{
    internal static bool AcceptsDirection(SubsystemTerrainPackage.DataType type, bool isServer)
    {
        return type switch
        {
            SubsystemTerrainPackage.DataType.RequestSyncChunks => isServer,
            SubsystemTerrainPackage.DataType.SyncTerrainCellDelta or
                SubsystemTerrainPackage.DataType.CellEditCompleted => !isServer,
            _ => false
        };
    }

    public override void Handle(SubsystemTerrainPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (context.Project is null || !AcceptsDirection(package.Type, isServer))
        {
            return;
        }

        var project = context.Project;
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
            case TerrainCellDeltaPackage message5:
            {
                var deltaTransport = subsystemTerrain.ChunkContentTransport as ClientChunkContentTransport ??
                                     throw new InvalidOperationException(
                                         "Remote terrain cell deltas require a network chunk transport."
                                     );
                foreach (var delta in message5.CellDeltas) { deltaTransport.Receive(delta); }

                break;
            }
        }
    }
}

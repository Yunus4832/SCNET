using Game.Network;
using Game.Terrains.Distribution;

namespace Game.Commands.BuiltIn;

internal static class NetworkDiagnosticCommandHandlers
{
    public static NetworkCommandDiagnostics Diagnostics { get; } = new(
        () => CommonLib.Net,
        () => null,
        CaptureClientTerrain
    );

    private static object? CaptureClientTerrain(CommandContext context)
    {
        var terrain = context.Project?.FindSubsystem<SubsystemTerrain>();
        if (terrain?.ChunkContentTransport is not ClientChunkContentTransport transport)
        {
            return null;
        }

        var presentation = context.Project!.FindSubsystem<SubsystemTerrainPresentation>(true)!;
        var locations = terrain.Demands.Locations.Values.ToArray();
        return new
        {
            transport.PendingSnapshotBytes,
            transport.BufferedSnapshotBytes,
            transport.TransfersCompleted,
            transport.RequestsSubmitted,
            transport.PendingDeltaCount,
            transport.PendingResyncCount,
            NativeTransport = WorldNetworkBinding.FindNode(context.Project!)
                ?.CaptureTerrainReceptionStatistics(),
            Chunks = terrain.Terrain.AllocatedChunks.Select(chunk => new
                {
                    chunk.Coords.X,
                    Z = chunk.Coords.Y,
                    Visible = locations.Any(location => Vector2.DistanceSquared(chunk.Center, location.Center) <=
                                                        location.VisibilityDistance * location.VisibilityDistance
                    ),
                    chunk.HasContents,
                    Drawable = ClientChunkDerivationPipeline.CanDraw(
                        TerrainContentRole.Replica,
                        chunk,
                        presentation.ChunkPresentations.Get(chunk)
                    ),
                    chunk.HasCurrentHeightData,
                    chunk.HasCurrentLight,
                    chunk.IsContentInitialized,
                    chunk.NetworkContentVersion,
                    chunk.NeedsContentSnapshot,
                    presentation.ChunkPresentations.Get(chunk).ClientGeometryContentVersion,
                    CurrentGeometry = presentation.ChunkPresentations.Get(chunk).HasCurrentGeometry(chunk),
                    presentation.ChunkPresentations.Get(chunk).UploadedContentVersion,
                    CurrentUploadedGeometry =
                        presentation.ChunkPresentations.Get(chunk).HasCurrentUploadedGeometry(chunk)
                }
            ).ToArray()
        };
    }
}

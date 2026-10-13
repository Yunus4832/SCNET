using System.Text.Json;

using Game.Network;
using Game.Terrains.Distribution;

namespace Game.Commands;

internal static class NetworkDiagnosticCommandHandlers
{
    public static CommandResult GetSendStatistics(CommandContext context, GetNetworkSendStatisticsCommand _)
    {
        var statistics = CommonLib.Net.SendStatistics;
        var snapshot = new
        {
            Terrain = CaptureTerrainBacklog(context),
            ClientTerrain = CaptureClientTerrain(context),
            HeadlessTicks = RunMode.Value == RunModeType.HeadlessServer ? HeadlessEntry.TickStatistics : null,
            Interest = context.Project?.FindSubsystem<SubsystemNetworkInterest>()?.Statistics,
            Routing = statistics.GetRouting(),
            Encoding = statistics.GetEncoding(),
            Fanout = statistics.GetFanout().OrderBy(item => item.Channel)
                .ThenBy(item => item.PackageType.FullName).Select(item => new
                {
                    Package = item.PackageType.FullName,
                    Channel = item.Channel.ToString(),
                    item.Messages,
                    item.RecipientDeliveries,
                    item.AverageFanout,
                    item.MaximumFanout
                }).ToArray(),
            PendingPackages = CommonLib.Net.PendingPackageCount,
            Invitation = new
            {
                Starting = CommonLib.Net.IsInvitationStarting,
                Code = CommonLib.Net.InvitationCode,
                Error = CommonLib.Net.InvitationError
            },
            ReliableQueues = CommonLib.Net.Clients.Values.Where(client => client.IsConnected && client.Peer != null)
                .Select(client => new
                {
                    ClientId = client.ID,
                    Path = client.Peer!.PathKind.ToString(),
                    AddressFamily = client.Peer.TransportEndpoint.AddressFamily.ToString(),
                    PendingBytes = client.Peer!.PendingReliableBytes
                }).ToArray(),
            Channels = statistics.GetChannels().OrderBy(item => item.Key).Select(item => new
            {
                Channel = item.Key.ToString(),
                item.Value.Batches,
                item.Value.EncodedBytes
            }).ToArray(),
            Packages = statistics.GetPackages().OrderBy(item => item.Channel)
                .ThenBy(item => item.PackageType.FullName).Select(item => new
                {
                    Package = item.PackageType.FullName,
                    Channel = item.Channel.ToString(),
                    item.Deliveries,
                    item.PayloadBytes
                }).ToArray()
        };
        return new CommandResult(true, "diagnostics.network.send_statistics",
            "Connection send totals captured; excludes transport overhead and retransmissions.",
            Data: JsonSerializer.SerializeToNode(snapshot));
    }

    private static object? CaptureTerrainBacklog(CommandContext context)
    {
        var terrain = context.Project?.FindSubsystem<SubsystemTerrain>();
        var scheduler = terrain?.TerrainUpdater.ServerChunkDistribution;
        if (scheduler == null)
        {
            return null;
        }

        var backlog = scheduler.GetBacklog();
        return new
        {
            backlog.Pending,
            backlog.AwaitingContent,
            backlog.OutstandingEncodes,
            terrain!.TerrainUpdater.PendingLocationCount,
            terrain.TerrainUpdater.LocationHandoffTimeouts,
            terrain.TerrainUpdater.DeferredAllocationPasses,
            AllocatedStates = terrain.Terrain.AllocatedChunks.GroupBy(chunk => chunk.WorkerState)
                .ToDictionary(group => group.Key.ToString(), group => group.Count()),
            SaveOutstanding = terrain.TerrainSaveCoordinator?.OutstandingCount
        };
    }

    private static object? CaptureClientTerrain(CommandContext context)
    {
        var terrain = context.Project?.FindSubsystem<SubsystemTerrain>();
        if (terrain?.ChunkContentTransport is not NetworkChunkContentTransport transport)
        {
            return null;
        }
        var locations = terrain.TerrainUpdater.UpdateLocations.Values.ToArray();
        return new
        {
            transport.PendingTransferCount,
            transport.FragmentsReceived,
            transport.TransfersCompleted,
            transport.FullChunkRequestsSent,
            transport.MissingFragmentRequestsSent,
            Chunks = terrain.Terrain.AllocatedChunks.Select(chunk => new
            {
                X = chunk.Coords.X,
                Z = chunk.Coords.Y,
                Visible = locations.Any(location => Vector2.DistanceSquared(chunk.Center, location.Center) <=
                                                    location.VisibilityDistance * location.VisibilityDistance),
                chunk.IsLoaded,
                Drawable = ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk),
                MainState = chunk.MainThreadState.ToString(),
                WorkerState = chunk.WorkerState.ToString(),
                chunk.NetworkContentVersion,
                chunk.ClientGeometryContentVersion
            }).ToArray()
        };
    }
}

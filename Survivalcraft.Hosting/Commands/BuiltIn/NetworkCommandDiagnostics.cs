using System.Text.Json;

namespace Game.Commands.BuiltIn;

public sealed class NetworkCommandDiagnostics(
    Func<NetNode> getNode,
    Func<object?> getServerTicks,
    Func<CommandContext, object?>? getClientTerrain = null
)
{
    public CommandResult GetSendStatistics(CommandContext context, GetNetworkSendStatisticsCommand _)
    {
        var node = context.Project is { } project
            ? WorldNetworkBinding.For(project).RequireNode()
            : getNode();
        var statistics = node.SendStatistics;
        var snapshot = new
        {
            RuntimeId = node.Runtime.Id,
            RuntimeSide = node.Runtime.Side.ToString(),
            Terrain = CaptureTerrainBacklog(context),
            ClientTerrain = getClientTerrain?.Invoke(context),
            ServerTicks = getServerTicks(),
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
                    }
                ).ToArray(),
            PendingPackages = node.PendingPackageCount,
            node.PendingNetworkPackageBytes,
            node.PendingLogicalPackageBytes,
            P2P = node.GetP2PStatus(),
            ReliableQueues = node.Clients.Values.Where(client => client.IsConnected && client.Peer != null)
                .Select(client => new
                    {
                        ClientId = client.ID,
                        Path = client.Peer!.PathKind.ToString(),
                        AddressFamily = client.Peer.TransportEndpoint.AddressFamily.ToString(),
                        PendingBytes = client.Peer!.PendingReliableBytes
                    }
                ).ToArray(),
            Channels = statistics.GetChannels().OrderBy(item => item.Key).Select(item => new
                {
                    Channel = item.Key.ToString(),
                    item.Value.Batches,
                    item.Value.EncodedBytes
                }
            ).ToArray(),
            Packages = statistics.GetPackages().OrderBy(item => item.Channel)
                .ThenBy(item => item.PackageType.FullName).Select(item => new
                    {
                        Package = item.PackageType.FullName,
                        Channel = item.Channel.ToString(),
                        item.Deliveries,
                        item.PayloadBytes
                    }
                ).ToArray()
        };
        return new CommandResult(
            true,
            "diagnostics.network.send_statistics",
            LocalizedText.Literal("Connection send totals captured; excludes transport overhead and retransmissions."),
            Data: JsonSerializer.SerializeToNode(snapshot)
        );
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
            OutstandingEncodes = WorldNetworkBinding.FindNode(context.Project!)?.OutstandingChunkEncodes ??
                                 0,
            terrain!.TerrainUpdater.PendingLocationCount,
            terrain.TerrainUpdater.LocationHandoffTimeouts,
            terrain.TerrainUpdater.DeferredAllocationPasses,
            AllocatedStates = terrain.Terrain.AllocatedChunks.GroupBy(chunk => chunk.WorkerState)
                .ToDictionary(group => group.Key.ToString(), group => group.Count()),
            SaveOutstanding = terrain.TerrainSaveCoordinator?.OutstandingCount
        };
    }
}

using System.Text.Json;

using Game.Network;

namespace Game.Commands;

internal static class NetworkDiagnosticCommandHandlers
{
    public static CommandResult GetSendStatistics(CommandContext context, GetNetworkSendStatisticsCommand _)
    {
        var statistics = CommonLib.Net.SendStatistics;
        var snapshot = new
        {
            Terrain = CaptureTerrainBacklog(context),
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
            ReliableQueues = CommonLib.Net.Clients.Values.Where(client => client.IsConnected && client.Peer != null)
                .Select(client => new
                {
                    ClientId = client.ID,
                    Channels = Enum.GetValues<NetworkChannel>().Select(channel => new
                    {
                        Channel = channel.ToString(),
                        Ordered = client.Peer!.GetPacketsCountInReliableQueue((byte)channel, true),
                        Unordered = client.Peer!.GetPacketsCountInReliableQueue((byte)channel, false)
                    }).ToArray()
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
}

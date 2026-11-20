using System.Text.Json.Nodes;

using EntitySystem.Core;

using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Network;

namespace Survivalcraft.Test.Commands;

public sealed class NetworkDiagnosticRuntimeTest
{
    [Theory]
    [InlineData(NodeRole.Authority)]
    [InlineData(NodeRole.Client)]
    public void StatisticsBelongToTheRequestedWorldInsteadOfTheGlobalNode(NodeRole role)
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            role
        );
        node.Runtime.BindProject(project);
        node.SendStatistics.RecordFanout(typeof(SubsystemTimePackage), NetworkChannel.Control, 3);
        var context = new CommandContext(CommandInvocationChannel.HttpApi, CommandPrincipal.LocalOperator, project);
        var diagnostics = new NetworkCommandDiagnostics(
            () => throw new InvalidOperationException("The requested world must supply its own endpoint."),
            () => null
        );
        var result = diagnostics.GetSendStatistics(context, new GetNetworkSendStatisticsCommand());
        var data = Assert.IsType<JsonObject>(result.Data);
        Assert.Equal(node.Runtime.Id, data["RuntimeId"]!.GetValue<Guid>());
        Assert.Equal(node.Runtime.Side.ToString(), data["RuntimeSide"]!.GetValue<string>());
        var fanout = Assert.Single(data["Fanout"]!.AsArray());
        Assert.Equal(3, fanout!["RecipientDeliveries"]!.GetValue<int>());
    }
}

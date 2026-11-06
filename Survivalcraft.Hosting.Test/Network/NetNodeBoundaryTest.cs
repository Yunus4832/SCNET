using Game.Network;
using Game.Network.Handling;

namespace Survivalcraft.Hosting.Test.Network;

public sealed class NetNodeBoundaryTest
{
    [Fact]
    public void NodeRetainsItsExplicitDeliveryPolicyAcrossRuntimeReset()
    {
        var options = new NetNodeOptions(3, 7, 12345);
        var node = new NetNode(
            options,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Inactive
        );
        node.PrepareAuthorityWorld();
        Assert.Same(options, node.Options);
        Assert.Equal(3, node.Options.MaximumChunkEncodes);
        Assert.Equal(7, node.Options.ChunksPerSecond);
        Assert.Equal(12345, node.Options.BytesPerSecond);
        node.StopImmediate();
    }

    [Theory]
    [InlineData("first", "second", "first\nsecond")]
    [InlineData("first", "", "first")]
    [InlineData("", "second", "second")]
    [InlineData("", "", "")]
    public void RepeatedRemovalPreservesNonEmptyReasons(string first, string second, string expected)
    {
        var node = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, NodeRole.Inactive);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
        node.RemoveClient(client, first);
        node.RemoveClient(client, second);
        var field = typeof(NetNode).GetField(
            "_clientsToRemove",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        var removals = (Dictionary<Client, string>)field.GetValue(node)!;
        Assert.Equal(expected, removals[client]);
        Assert.Single(removals);
        node.StopImmediate();
    }

    [Fact]
    public void AdmissionRequestCarriesOnlyTheExplicitModHash()
    {
        var token = Guid.NewGuid();
        var options = new ClientConnectionOptions(new string('a', 64));
        var request = NetNode.CreateConnectionRequest(token, options);
        Assert.Equal(token, request.TmpToken);
        Assert.Equal(options.ModDataHash, request.ModDataHash);
        Assert.Throws<ArgumentException>(() => new ClientConnectionOptions(" "));
    }
}

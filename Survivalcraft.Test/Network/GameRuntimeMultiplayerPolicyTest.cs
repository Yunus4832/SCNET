using System.Reflection;
using System.Runtime.CompilerServices;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class GameRuntimeMultiplayerPolicyTest
{
    [Fact]
    public void InProcessAuthorityRoleWithoutListenerDoesNotEnableMultiplayerUi()
    {
        using var gameRuntime = new GameRuntime(true);
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        gameRuntime.Attach(authority.Runtime);
        gameRuntime.Attach(client.Runtime);
        using var first = InProcessMessageConnection.CreatePair(
            authority,
            new Client(null, 1, Guid.NewGuid(), Guid.Empty),
            client,
            new Client(null, 0, Guid.NewGuid(), Guid.Empty)
        ).ToAuthority;

        Assert.False(gameRuntime.IsMultiplayer);
        var host = typeof(NetNode).GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic)!;
        host.SetValue(authority, RuntimeHelpers.GetUninitializedObject(host.FieldType));
        try { Assert.True(gameRuntime.IsMultiplayer); }
        finally { host.SetValue(authority, null); }
    }

    [Fact]
    public void RemoteConnectionEnablesMultiplayerUiAndClosureRemovesIt()
    {
        using var gameRuntime = new GameRuntime(false);
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var connection = new NetworkStub();
        node.Server = new Client(null, 0, Guid.NewGuid(), Guid.Empty) { Connection = connection };
        gameRuntime.Attach(node.Runtime);
        Assert.True(gameRuntime.IsMultiplayer);
        node.Runtime.Close();
        Assert.False(gameRuntime.IsMultiplayer);
    }

    private sealed class NetworkStub : IMessageConnection
    {
        public bool IsConnected => true;

        public bool IsInProcess => false;

        public ConnectionSendResult TrySendOwned(OutboundPackage message) => ConnectionSendResult.Accepted;

        public void Disconnect(string reason)
        {
        }

        public void Dispose()
        {
        }
    }
}

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class GameRuntimeMultiplayerPolicyTest
{
    [Fact]
    public async Task InProcessAuthorityWithoutListenerDoesNotEnableMultiplayerUi()
    {
        await using var game = new GameRuntime(true, Inline);
        await game.StartAuthorityAsync(_ => WorldRuntimeContext.For(new RuntimeTestProject(RuntimeSide.Authority)));
        var client = new NetNode(NetNodeOptions.Default, true, PackageHandlerCatalog.Empty, NodeRole.Client);
        game.AttachClient(client.Runtime);
        var connection = await game.AuthorityExecution!.InvokeAsync(world =>
            InProcessMessageConnection.CreatePair(
                WorldNetworkBinding.For(world).RequireNode(),
                new Client(null, 1, Guid.NewGuid(), Guid.Empty),
                client,
                new Client(null, 0, Guid.NewGuid(), Guid.Empty)
            ).ToAuthority
        );
        using (connection) { Assert.False(game.IsMultiplayer); }
    }

    [Fact]
    public async Task RemoteConnectionEnablesMultiplayerUiAndClosureRemovesIt()
    {
        await using var game = new GameRuntime(false, Inline);
        var node = new NetNode(NetNodeOptions.Default, true, PackageHandlerCatalog.Empty, NodeRole.Client);
        node.Server = new Client(null, 0, Guid.NewGuid(), Guid.Empty) { Connection = new NetworkStub() };
        game.AttachClient(node.Runtime);
        Assert.True(game.IsMultiplayer);
        node.Runtime.Close();
        Assert.False(game.IsMultiplayer);
    }

    private static Task Inline(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private sealed class NetworkStub : IMessageConnection
    {
        public bool IsConnected => true;

        public bool IsInProcess => false;

        public ConnectionSendResult TrySendOwned(OutboundPackage message) => ConnectionSendResult.Accepted;

        public void Disconnect(LocalizedText reason)
        {
        }

        public void Dispose()
        {
        }
    }
}

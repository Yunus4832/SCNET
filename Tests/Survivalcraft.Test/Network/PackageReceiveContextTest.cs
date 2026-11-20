using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class PackageReceiveContextTest
{
    [Fact]
    public void DeferredEnvelopesKeepIndependentSendersForTheSamePayload()
    {
        var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        var second = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid());
        var payload = new TestPackage();
        var handler = new TestHandler();
        var builder = new PackageHandlerCatalogBuilder();
        builder.RegisterLogical(() => handler);
        var catalog = builder.Build();
        var queued = new[]
        {
            new ReceivedPackage(
                payload,
                new PackageReceiveContext(
                    null,
                    Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Authority, catalog),
                    first
                )
            ),
            new ReceivedPackage(
                payload,
                new PackageReceiveContext(
                    null,
                    Fixtures.RoutingWorldTestFactory.Create(RuntimeSide.Client, catalog),
                    second
                )
            )
        };
        try
        {
            foreach (var received in queued) { WorldNetworkBinding.For(received.Context.Runtime).Dispatch(received); }

            Assert.Equal(2, handler.Contexts.Count);
            Assert.Same(first, handler.Contexts[0].Sender);
            Assert.True(handler.Contexts[0].IsServer);
            Assert.Same(second, handler.Contexts[1].Sender);
            Assert.False(handler.Contexts[1].IsServer);
        }
        finally
        {
            foreach (var received in queued) { received.Context.Runtime.Close(); }
        }
    }

    private sealed record TestPackage : IProtocolMessage;

    private sealed class TestHandler : PackageHandlerBase<TestPackage>
    {
        public List<PackageReceiveContext> Contexts { get; } = [];

        public override void Handle(TestPackage package, PackageReceiveContext context)
        {
            Contexts.Add(context);
        }
    }
}

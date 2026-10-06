using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public sealed class PackageReceiveContextTest
{
    [Fact]
    public void DeferredEnvelopesKeepIndependentSendersForTheSamePayload()
    {
        var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var second = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid(), null);
        var payload = new TestPackage();
        var queued = new[]
        {
            new ReceivedPackage(payload, new PackageReceiveContext(null, true, first)),
            new ReceivedPackage(payload, new PackageReceiveContext(null, false, second))
        };
        var handler = new TestHandler();
        PackageDispatcher.Register<TestPackage>(handler);
        try
        {
            foreach (var received in queued)
            {
                PackageDispatcher.Handle(received);
            }

            Assert.Equal(2, handler.Contexts.Count);
            Assert.Same(first, handler.Contexts[0].Sender);
            Assert.True(handler.Contexts[0].IsServer);
            Assert.Same(second, handler.Contexts[1].Sender);
            Assert.False(handler.Contexts[1].IsServer);
        }
        finally
        {
            PackageDispatcher.Unregister(typeof(TestPackage));
        }
    }

    private sealed class TestPackage : IPackage
    {
        public byte ID => 250;

        public ClientState MinNeedState => ClientState.ProjectLoaded;

        public void WriteData(PackageStreamWriter writer)
        {
        }

        public void ReadData(PackageStreamReader reader)
        {
        }
    }

    private sealed class TestHandler : PackageHandlerBase<TestPackage>
    {
        public List<PackageReceiveContext> Contexts { get; } = [];

        public override void Handle(TestPackage package, PackageReceiveContext context)
        {
            Contexts.Add(context);
        }
    }
}

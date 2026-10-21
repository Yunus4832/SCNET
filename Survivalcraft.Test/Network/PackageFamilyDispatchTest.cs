using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class PackageFamilyDispatchTest
{
    [Fact]
    public void ExplicitVariantsUseOneTypedHandlerAndUnregisterAsOneFamily()
    {
        var handler = new Handler();
        var first = new First();
        var second = new Second();
        PackageDispatcher.Register(typeof(First), handler);
        PackageDispatcher.Register(typeof(Second), handler);
        try
        {
            var context = new PackageReceiveContext(null, false, null);
            Assert.True(PackageDispatcher.TryHandle(first, context));
            Assert.True(PackageDispatcher.TryHandle(second, context));
            Assert.Equal(new Message[] { first, second }, handler.Received);
            PackageDispatcher.Unregister(typeof(First));
            Assert.False(PackageDispatcher.TryHandle(first, context));
            Assert.False(PackageDispatcher.TryHandle(second, context));
        }
        finally { PackageDispatcher.Unregister(typeof(Message)); }
    }

    private abstract record Message : IProtocolMessage
    {
        public byte ID => 250;
    }

    private sealed record First : Message;

    private sealed record Second : Message;

    private sealed class Handler : PackageHandlerBase<Message>
    {
        public List<Message> Received { get; } = [];

        public override void Handle(Message package, PackageReceiveContext context) => Received.Add(package);
    }
}

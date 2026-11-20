using Game.Modding;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Protocol;
using Game.Protocol.Connection;
using Game.Protocol.Mods;
using Game.Protocol.Serialization;
using Game.Protocol.World;
using Game.World;

namespace Survivalcraft.Hosting.Test.Network;

public sealed class PackageHandlerCatalogTest
{
    [Fact]
    public void ModFactoriesCaptureTheirActualOwnerAndUnloadIndependentlyAcrossWorlds()
    {
        var owner = new ModId("test");
        var firstHooks = new ModNetworkHooks();
        var secondHooks = new ModNetworkHooks();
        var firstCalls = 0;
        var secondCalls = 0;
        using var firstRegistration = firstHooks.ForOwner(owner).OnMessage("ping", _ => firstCalls++);
        using var secondRegistration = secondHooks.ForOwner(owner).OnMessage("ping", _ => secondCalls++);
        firstHooks.Freeze();
        secondHooks.Freeze();
        var firstBuilder = new PackageHandlerCatalogBuilder();
        firstBuilder.RegisterFamily(() => new ModEnvelopePackageHandler(firstHooks));
        var secondBuilder = new PackageHandlerCatalogBuilder();
        secondBuilder.RegisterFamily(() => new ModEnvelopePackageHandler(secondHooks));
        var first = new WorldRuntimeContext(RuntimeSide.Client, false);
        var second = new WorldRuntimeContext(RuntimeSide.Client, false);
        using var firstBinding = new WorldNetworkBinding(first, null, PackageHandlerCatalog.Empty);
        using var secondBinding = new WorldNetworkBinding(second, null, PackageHandlerCatalog.Empty);
        firstBinding.InstallHandlers(firstBuilder.Build());
        secondBinding.InstallHandlers(secondBuilder.Build());
        var message = new ModEnvelopePackage("test", "ping", []);
        Assert.True(firstBinding.Dispatch(message, new PackageReceiveContext(null, first, null)));
        Assert.True(secondBinding.Dispatch(message, new PackageReceiveContext(null, second, null)));
        firstHooks.RemoveOwner(owner);
        firstBinding.Dispatch(message, new PackageReceiveContext(null, first, null));
        secondBinding.Dispatch(message, new PackageReceiveContext(null, second, null));
        Assert.Equal(1, firstCalls);
        Assert.Equal(2, secondCalls);
        secondRegistration.Dispose();
        secondBinding.Dispatch(message, new PackageReceiveContext(null, second, null));
        Assert.Equal(2, secondCalls);
        first.Close();
        second.Close();
    }

    [Fact]
    public void DuplicateFamilyRegistrationIsAtomicAndBuiltCatalogIsImmutable()
    {
        var builder = new PackageHandlerCatalogBuilder();
        builder.RegisterLogical(() => new StateHandler());
        var frozen = builder.Build();
        Assert.Throws<InvalidOperationException>(() => builder.RegisterFamily(() => new ClientHandler()));
        Assert.False(builder.Build().Contains(typeof(ClientRemovedPackage)));
        builder.RegisterFamily(() => new TimeHandler());
        Assert.False(frozen.Contains(typeof(SubsystemTimePackage)));
        Assert.True(builder.Build().Contains(typeof(SubsystemTimePackage)));
    }

    [Fact]
    public void BusinessHandlersInstallOnceAndInstancesBelongToEachWorld()
    {
        var instances = new List<TimeHandler>();
        var builder = new PackageHandlerCatalogBuilder();
        builder.RegisterFamily(() =>
            {
                var handler = new TimeHandler();
                instances.Add(handler);
                return handler;
            }
        );
        var catalog = builder.Build();
        var first = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var second = new WorldRuntimeContext(RuntimeSide.Authority, false);
        using var firstBinding = new WorldNetworkBinding(first, null, catalog);
        using var secondBinding = new WorldNetworkBinding(second, null, catalog);
        var message = new SubsystemTimePackage(1, 0);
        Assert.False(firstBinding.Dispatch(message, new PackageReceiveContext(null, first, null)));
        firstBinding.InstallHandlers(catalog);
        secondBinding.InstallHandlers(catalog);
        Assert.True(firstBinding.Dispatch(message, new PackageReceiveContext(null, first, null)));
        Assert.True(firstBinding.Dispatch(message, new PackageReceiveContext(null, first, null)));
        Assert.True(secondBinding.Dispatch(message, new PackageReceiveContext(null, second, null)));
        Assert.Equal([2, 1], instances.Select(handler => handler.Calls));
        Assert.Throws<InvalidOperationException>(() => firstBinding.InstallHandlers(catalog));
        Assert.False(firstBinding.Dispatch(message, new PackageReceiveContext(null, second, null)));
        first.Close();
        Assert.Same(PackageHandlerCatalog.Empty, firstBinding.Handlers);
        Assert.False(firstBinding.Dispatch(message, new PackageReceiveContext(null, first, null)));
        second.Close();
    }

    [Fact]
    public void KnownUnsupportedMessagesStillDecodeAndMalformedBatchDoesNotExposeAPrefix()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        using var binding = new WorldNetworkBinding(world, null, PackageHandlerCatalog.Empty);
        var context = new PackageReceiveContext(null, world, null);
        var payload = ProtocolPacketCodec.Encode(
            [new SubsystemTimePackage(1, 0), new SubsystemTimePackage(2, 0)],
            CompressionPolicy.None
        );
        var decoded = ProtocolReceivePolicy.DecodePackages(context, payload);
        Assert.Equal(2, decoded.Count);
        Assert.All(decoded, received => Assert.False(binding.Dispatch(received)));
        Assert.Empty(ProtocolReceivePolicy.DecodePackages(context, payload.AsMemory(0, payload.Length - 1)));
        world.Close();
    }

    [Theory]
    [InlineData(NodeRole.Authority, RuntimeSide.Authority)]
    [InlineData(NodeRole.Client, RuntimeSide.Client)]
    public void ExplicitEndpointRoleDeterminesItsFirstWorld(NodeRole role, RuntimeSide side)
    {
        var node = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, role);
        Assert.Equal(side, node.Runtime.Side);
        Assert.Same(PackageHandlerCatalog.Empty, node.Handlers);
        node.StopImmediate();
    }

    private sealed class TimeHandler : PackageHandlerBase<SubsystemTimePackage>
    {
        public int Calls { get; private set; }

        public override void Handle(SubsystemTimePackage package, PackageReceiveContext context) => Calls++;
    }

    private sealed class StateHandler : PackageHandlerBase<ClientStateChangedPackage>
    {
        public override void Handle(ClientStateChangedPackage package, PackageReceiveContext context)
        {
        }
    }

    private sealed class ClientHandler : PackageHandlerBase<ClientPackage>
    {
        public override void Handle(ClientPackage package, PackageReceiveContext context)
        {
        }
    }
}

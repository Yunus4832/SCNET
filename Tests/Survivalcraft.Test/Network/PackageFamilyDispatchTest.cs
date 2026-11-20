using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Network;

public sealed class PackageFamilyDispatchTest
{
    [Fact]
    public void ExplicitVariantsUseOneTypedWorldHandlerAndCloseRemovesTheirRoutes()
    {
        var handler = new Handler();
        var builder = new PackageHandlerCatalogBuilder();
        builder.RegisterFamily(() => handler);
        var world = RoutingWorldTestFactory.Create(RuntimeSide.Client, builder.Build());
        var binding = WorldNetworkBinding.For(world);
        var context = new PackageReceiveContext(null, world, null);
        Assert.True(binding.Dispatch(new ClientListPackage(), context));
        Assert.True(binding.Dispatch(new ClientRemovedPackage(), context));
        Assert.Equal([typeof(ClientListPackage), typeof(ClientRemovedPackage)], handler.Types);
        world.Close();
        Assert.False(binding.Dispatch(new ClientListPackage(), context));
        Assert.False(binding.Dispatch(new ClientRemovedPackage(), context));
        Assert.Equal(2, handler.Types.Count);
    }

    private sealed class Handler : PackageHandlerBase<ClientPackage>
    {
        public List<Type> Types { get; } = [];

        public override void Handle(ClientPackage package, PackageReceiveContext context) =>
            Types.Add(package.GetType());
    }
}

using Game.Network;
using Game.Network.Handling;
using Game.World;

namespace Survivalcraft.Test.Fixtures;

/// <summary>Creates a logical routing world with an explicitly empty or supplied test catalog.</summary>
internal static class RoutingWorldTestFactory
{
    public static WorldRuntimeContext Create(RuntimeSide side) => Create(side, PackageHandlerCatalog.Empty);

    public static WorldRuntimeContext Create(RuntimeSide side, PackageHandlerCatalog catalog)
    {
        var world = new WorldRuntimeContext(side, false);
        _ = new WorldConnections(world);
        var binding = new WorldNetworkBinding(world, null, PackageHandlerCatalog.Empty);
        binding.InstallHandlers(catalog);
        if (!world.IsAuthority) { _ = new ClientBootstrapContent(world); }

        return world;
    }
}

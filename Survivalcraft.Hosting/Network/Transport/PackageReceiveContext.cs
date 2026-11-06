using EntitySystem.Core;

namespace Game.Network.Transport;

public readonly record struct PackageReceiveContext(NetNode? Node, WorldRuntimeContext Runtime, Client? Sender)
{
    public bool IsServer => Runtime.Side == RuntimeSide.Authority;

    public Project? Project => Runtime.Project;

    public static PackageReceiveContext ForDiscovery(
        Handling.PackageHandlerCatalog handlers,
        Client? sender = null
    )
    {
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        _ = new WorldNetworkBinding(world, null, handlers);
        return new PackageReceiveContext(null, world, sender);
    }
}

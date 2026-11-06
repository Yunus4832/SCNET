using EntitySystem.Core;

namespace Game.Network.Transport;

public static class NetworkSender
{
    private static WorldRuntimeContext? GetActiveRuntime(Project project)
    {
        if (project.IsDisposed) { return null; }

        var world = WorldRuntimeContext.For(project);
        return world.IsClosed ? null : world;
    }

    public static void SendToServer(WorldRuntimeContext world, IProtocolMessage message)
    {
        if (world.IsClosed) { return; }

        if (world.IsAuthority || WorldConnections.For(world).Authority is not { } authority)
        {
            throw new InvalidOperationException("Only a client world can submit a message to its authority.");
        }

        WorldMessageEndpoint.For(world).Send(message, PackageAudience.To(authority));
    }

    public static void Send(Project project, IProtocolMessage message, PackageAudience audience)
    {
        if (GetActiveRuntime(project) is { } world) { WorldMessageEndpoint.For(world).Send(message, audience); }
    }

    public static void SendGlobal(Project project, IProtocolMessage message)
    {
        Send(project, message, PackageAudience.Global);
    }

    public static void SendTo(WorldConnection connection, IProtocolMessage message)
    {
        if (!connection.IsConnected) { return; }

        var world = connection.World ?? throw new InvalidOperationException("The recipient has no world owner.");
        if (world.IsClosed) { return; }

        if (!WorldConnections.For(world).Contains(connection))
        {
            throw new InvalidOperationException("The recipient is no longer registered with its world endpoint.");
        }

        WorldMessageEndpoint.For(world).Send(message, PackageAudience.To(connection));
    }

    public static void SendNearPoint(Project project, Vector3 position, float radius, IProtocolMessage message)
    {
        if (GetActiveRuntime(project) is { } world)
        {
            WorldMessageEndpoint.For(world).SendNearPoint(position, radius, message);
        }
    }

    public static void SendToChunkObservers(Project project, Point2 chunk, IProtocolMessage message)
    {
        if (GetActiveRuntime(project) is { } world)
        {
            WorldMessageEndpoint.For(world).SendToChunkObservers(chunk, message);
        }
    }

    public static void SendToObservers(Entity entity, IProtocolMessage message, WorldConnection? except = null)
    {
        if (GetActiveRuntime(entity.Project) is { } world)
        {
            WorldMessageEndpoint.For(world).SendToObservers(entity, message, except);
        }
    }
}

using System.Runtime.CompilerServices;

namespace Game.Network.Transport;

/// <summary>Explicit binding to the sole outbound message path of one world.</summary>
public sealed class WorldMessageEndpoint : IDisposable
{
    private static readonly ConditionalWeakTable<WorldRuntimeContext, WorldMessageEndpoint> _bindings = new();

    private readonly IWorldMessageSink _sink;

    private readonly CancellationTokenRegistration _closed;

    private bool _disposed;

    public WorldRuntimeContext World { get; }

    public WorldMessageEndpoint(WorldRuntimeContext world, IWorldMessageSink sink)
    {
        ObjectDisposedException.ThrowIf(world.IsClosed, world);
        World = world;
        _sink = sink;
        _bindings.Add(world, this);
        try
        {
            world.OwnWorldResource(this);
            _closed = world.Lifetime.Register(() => _bindings.Remove(world));
        }
        catch
        {
            _bindings.Remove(world);
            throw;
        }
    }

    public static IWorldMessageSink For(WorldRuntimeContext world)
    {
        return _bindings.TryGetValue(world, out var endpoint) && !world.IsClosed
            ? endpoint._sink
            : throw new InvalidOperationException("World has no outbound message endpoint.");
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _closed.Dispose();
        _bindings.Remove(World);
    }
}

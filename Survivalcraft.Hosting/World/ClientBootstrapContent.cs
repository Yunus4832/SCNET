using System.Runtime.CompilerServices;

using EntitySystem.Core;

namespace Game.World;

/// <summary>Client installation queues and pending content bytes for one actual client world.</summary>
public sealed class ClientBootstrapContent : IDisposable
{
    private static readonly ConditionalWeakTable<WorldRuntimeContext, ClientBootstrapContent> _bindings = new();

    private readonly CancellationTokenRegistration _closed;

    private bool _disposed;

    public WorldRuntimeContext World { get; }

    public byte[]? BlocksTextureBytes { get; set; }

    internal ClientBootstrapContent(WorldRuntimeContext world)
    {
        ObjectDisposedException.ThrowIf(world.IsClosed, world);
        if (world.IsAuthority) { throw new InvalidOperationException("Client content requires a client world."); }

        World = world;
        _bindings.Add(world, this);
        try
        {
            world.OwnWorldResource(this);
            _closed = world.Lifetime.Register(Clear);
        }
        catch
        {
            _bindings.Remove(world);
            throw;
        }
    }

    public static ClientBootstrapContent For(WorldRuntimeContext world)
    {
        return _bindings.TryGetValue(world, out var binding)
            ? binding
            : throw new InvalidOperationException("World has no client content owner.");
    }

    public static ClientBootstrapContent For(Project project) => For(WorldRuntimeContext.For(project));

    private void Clear()
    {
        BlocksTextureBytes = null;
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _closed.Dispose();
        Clear();
        _bindings.Remove(World);
    }
}

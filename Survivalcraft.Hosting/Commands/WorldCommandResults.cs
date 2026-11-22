using System.Runtime.CompilerServices;

using EntitySystem.Core;

namespace Game.Commands;

public readonly record struct CommandResultDelivery(CommandResult Result, string CorrelationId);

/// <summary>在本次世界生命周期内发布强类型的本地命令补全结果。</summary>
public sealed class WorldCommandResults : IDisposable
{
    private static readonly ConditionalWeakTable<WorldRuntimeContext, WorldCommandResults> _bindings = new();

    private readonly CancellationTokenRegistration _closed;

    private bool _disposed;

    public WorldRuntimeContext World { get; }

    public event Action<CommandResultDelivery>? Delivered;

    public WorldCommandResults(WorldRuntimeContext world)
    {
        ObjectDisposedException.ThrowIf(world.IsClosed, world);
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

    public static WorldCommandResults For(WorldRuntimeContext world)
    {
        return _bindings.TryGetValue(world, out var results)
            ? results
            : throw new InvalidOperationException("World has no command result owner.");
    }

    public static WorldCommandResults For(Project project) => For(WorldRuntimeContext.For(project));

    public void Publish(CommandResult result, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        if (_disposed || World.IsClosed) { return; }

        Delivered?.Invoke(new CommandResultDelivery(result, correlationId));
    }

    private void Clear()
    {
        Delivered = null;
        _bindings.Remove(World);
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _closed.Dispose();
        Clear();
    }
}

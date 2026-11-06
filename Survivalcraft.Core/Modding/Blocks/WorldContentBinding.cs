using System.Runtime.CompilerServices;

using EntitySystem.Core;

namespace Game.Modding.Blocks;

/// <summary>Owns one actual catalog until the world's final resource-release phase.</summary>
public sealed class WorldContentBinding : IDisposable
{
    private static readonly ConditionalWeakTable<WorldRuntimeContext, WorldContentBinding> _bindings = new();

    private Action? _release;

    public BlockContentCatalog Catalog { get; }

    public WorldRuntimeContext World { get; }

    public WorldContentBinding(WorldRuntimeContext world, BlockContentCatalog catalog, Action release)
    {
        ObjectDisposedException.ThrowIf(world.IsClosed, world);
        World = world;
        Catalog = catalog;
        _release = release;
        _bindings.Add(world, this);
        try { world.OwnFinalWorldResource(this); }
        catch
        {
            _bindings.Remove(world);
            throw;
        }
    }

    public static WorldContentBinding For(WorldRuntimeContext world)
    {
        return _bindings.TryGetValue(world, out var binding)
            ? binding
            : throw new InvalidOperationException("World has no content binding.");
    }

    public static WorldContentBinding For(Project project) => For(WorldRuntimeContext.For(project));

    public void Dispose()
    {
        var release = Interlocked.Exchange(ref _release, null);
        if (release is null) { return; }

        _bindings.Remove(World);
        release();
    }
}

using System.Reflection;

using Game.Modding;
using Game.Modding.Blocks;

namespace Survivalcraft.Test.Modding;

internal sealed class WorldCatalogScope : IDisposable
{
    private readonly GameModRuntime? _previous = CurrentModRuntime.Value;

    private readonly GameModRuntime _runtime;

    public WorldCatalogScope(params ModDescriptor[] mods)
    {
        _runtime = GameModRuntime.Start(mods);
        var registry = BlockRuntimeCatalog.Compile(
            _runtime.Host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName)
        );
        Catalog = new BlockContentCatalog(registry);
        typeof(GameModRuntime).GetProperty(
                nameof(GameModRuntime.WorldCatalog),
                BindingFlags.Public | BindingFlags.Instance
            )!
            .SetValue(_runtime, Catalog);
        CurrentModRuntime.Set(_runtime);
    }

    public BlockContentCatalog Catalog { get; }

    public void Dispose()
    {
        _runtime.Dispose();
        CurrentModRuntime.Set(_previous);
    }
}

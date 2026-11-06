using Game.Modding;
using Game.Modding.Blocks;
using Game.Network;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Modding;

internal sealed class WorldCatalogScope : IDisposable
{
    private readonly GameModRuntime _runtime;

    public WorldCatalogScope(params ModDescriptor[] mods) : this(RuntimeSide.Authority, mods)
    {
    }

    public WorldCatalogScope(RuntimeSide side, params ModDescriptor[] mods) : this(
        new WorldRuntimeContext(
            side,
            (side == RuntimeSide.Client && true)
        ),
        mods
    )
    {
    }

    public WorldCatalogScope(NetNode node, params ModDescriptor[] mods) : this(node.Runtime, mods)
    {
    }

    private WorldCatalogScope(WorldRuntimeContext runtime, params ModDescriptor[] mods)
    {
        _runtime = GameModRuntime.Start(mods);
        var registry = BlockRuntimeCatalog.Compile(
            _runtime.Host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName).Entries
        );
        Catalog = new BlockContentCatalog(registry);
        _runtime.AdoptWorldCatalog(runtime, Catalog);
        Project = new RuntimeTestProject(runtime.Side, runtime);
    }

    public GameModRuntime Runtime => _runtime;

    public BlockContentCatalog Catalog { get; }

    public RuntimeTestProject Project { get; }

    public void Dispose()
    {
        try { Project.Dispose(); }
        finally { _runtime.Dispose(); }
    }
}

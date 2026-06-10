using Game.Blocks;
using Game.Modding;
using Game.Modding.Blocks;

namespace Survivalcraft.Test.Modding;

public class BlockRuntimeCatalogTest
{
    [Fact]
    public void BuiltInContentModRegistersStableIdsAndLegacyIndexes()
    {
        var host = new ModHost();

        host.LoadAndStart([BuiltInContentMod.CreateDescriptor()]);

        var registry = host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName);
        Assert.True(registry.TryGet(
            new ResourceId(new ModId("game"), "air"),
            out var air));
        Assert.Equal(AirBlock.Index, air!.LegacyIndex);
        Assert.True(registry.TryGet(
            new ResourceId(new ModId("game"), "crafting_table"),
            out var craftingTable));
        Assert.Equal(CraftingTableBlock.Index, craftingTable!.LegacyIndex);
        Assert.True(registry.Entries.Count > 250);

        host.StopAll();
    }

    [Fact]
    public void CatalogCreatesLegacyArrayAndStableLookup()
    {
        var host = new ModHost();
        host.LoadAndStart([Descriptor(
            ("air", 0, static () => new AirBlock()),
            ("machine", 42, static () => new DirtBlock()))]);
        var registry = host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName);

        var catalog = BlockRuntimeCatalog.Compile(registry);
        var blocks = catalog.CreateLegacyBlockArray();

        Assert.IsType<AirBlock>(blocks[0]);
        Assert.IsType<DirtBlock>(blocks[42]);
        Assert.Equal(42, blocks[42].BlockIndex);
        Assert.Same(blocks[0], blocks[41]);
        Assert.True(catalog.TryGet(new ResourceId(new ModId("example"), "machine"), out var machine));
        Assert.Equal(42, machine!.RuntimeIndex);

        host.StopAll();
    }

    [Fact]
    public void CatalogRejectsRuntimeIndexConflicts()
    {
        var host = new ModHost();
        host.LoadAndStart([Descriptor(
            ("air", 0, static () => new AirBlock()),
            ("first", 42, static () => new DirtBlock()),
            ("second", 42, static () => new DirtBlock()))]);
        var registry = host.Extensions.GetRegistry<BlockRegistration>(BlockExtensions.RegistryName);

        var exception = Assert.Throws<InvalidOperationException>(() => BlockRuntimeCatalog.Compile(registry));

        Assert.Contains("both use runtime index 42", exception.Message);
        host.StopAll();
    }

    private static ModDescriptor Descriptor(params (string Id, int Index, Func<Block> Factory)[] blocks)
    {
        var manifest = new ModManifest("example", "Example", "1.0.0");
        return new ModDescriptor(manifest, () => new TestBlockMod(blocks));
    }

    private sealed class TestBlockMod((string Id, int Index, Func<Block> Factory)[] blocks) : IMod
    {
        public void Configure(IModContext context)
        {
            foreach (var block in blocks)
            {
                context.Extensions.Register(
                    BlockExtensions.RegistryName,
                    new ResourceId(context.Manifest.ModId, block.Id),
                    new BlockRegistration(block.Index, block.Factory));
            }
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }
}

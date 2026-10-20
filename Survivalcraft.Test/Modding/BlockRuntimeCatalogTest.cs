using System.Runtime.CompilerServices;
using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Subsystems;
using Game.Terrains;

namespace Survivalcraft.Test.Modding;

public class BlockRuntimeCatalogTest
{
    [Fact]
    public void ExplicitBuiltInTableMatchesCompleteBaseline()
    {
        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "BuiltInBlocks.txt"));
        var actual = BuiltInBlockRegistry.Entries.Select(entry =>
            $"{entry.Index};game:{entry.Path};{entry.BlockType.FullName}").ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal(258, actual.Length);
        Assert.Equal(258, BuiltInBlockRegistry.Entries.Select(entry => entry.Index).Distinct().Count());
        Assert.Equal(258, BuiltInBlockRegistry.Entries.Select(entry => entry.Path).Distinct().Count());
        foreach (var entry in BuiltInBlockRegistry.Entries)
        {
            Assert.Equal(entry.BlockType, entry.Factory().GetType());
        }
    }

    [Fact]
    public void DynamicAllocationIsIndependentOfRegistrationOrderAndPreservesSavedIndices()
    {
        var first = ResourceId.Parse("test:machine");
        var second = ResourceId.Parse("other:tool");
        var saved = new BlockIndexMapping([new(first, 300)]);
        var a = BlockIndexMapping.Allocate([first, second], saved);
        var b = BlockIndexMapping.Allocate([second, first], saved);
        Assert.Equal(300, a.Entries[first]);
        Assert.Equal(a.Entries[second], b.Entries[second]);
        Assert.Equal(a.Entries.OrderBy(pair => pair.Key.ToString()),
            BlockIndexMapping.FromXml(a.ToXml()).Entries.OrderBy(pair => pair.Key.ToString()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(1024)]
    public void MappingRejectsInvalidOrReservedIndices(int index)
    {
        Assert.Throws<InvalidDataException>(() => new BlockIndexMapping([new(ResourceId.Parse("test:machine"), index)]));
    }

    [Fact]
    public void MappingRejectsDuplicateIdOrIndex()
    {
        var first = ResourceId.Parse("test:first");
        var second = ResourceId.Parse("test:second");
        Assert.Throws<InvalidDataException>(() => new BlockIndexMapping([new(first, 300), new(first, 301)]));
        Assert.Throws<InvalidDataException>(() => new BlockIndexMapping([new(first, 300), new(second, 300)]));
    }

    [Fact]
    public void CapacityExhaustionDoesNotWrapIndices()
    {
        var resources = Enumerable.Range(0, 1024).Select(i => ResourceId.Parse($"test:block_{i}"));
        Assert.Throws<InvalidOperationException>(() => BlockIndexMapping.Allocate(resources, BlockIndexMapping.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => Terrain.MakeBlockValue(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Terrain.MakeBlockValue(1024));
    }

    [Fact]
    public void PreviewAndWorldHaveIndependentInstancesAttributesAndRecipes()
    {
        using var preview = Build(264);
        using var world = Build(300);
        var id = ResourceId.Parse("test:machine");
        Assert.NotSame(preview.GetBlock(id), world.GetBlock(id));
        Assert.Equal(264, preview.GetBlock(id).BlockIndex);
        Assert.Equal(300, world.GetBlock(id).BlockIndex);
        Assert.Same(preview, preview.GetBlock(id).Catalog);
        Assert.Same(world, world.GetBlock(id).Catalog);
        Assert.Equal(1, ((TestBlock)world.GetBlock(id)).InitializeCount);
        Assert.Equal(264, Terrain.ExtractContents(preview.Recipes.ReadonlyRecipes.Single().ResultValue));
        Assert.Equal(300, Terrain.ExtractContents(world.Recipes.ReadonlyRecipes.Single().ResultValue));
        Assert.Equal(7, Terrain.ExtractData(world.Recipes.ReadonlyRecipes.Single().ResultValue));
        Assert.Equal(264, Terrain.ExtractContents(preview.Recipes.ReadonlyRecipes.Single().RemainsValue));
        world.GetBlock(id).DisplayName = "World";
        Assert.Equal("Machine", preview.GetBlock(id).DisplayName);
        world.Dispose();
        Assert.Equal(264, preview.GetBlock(id).BlockIndex);
    }

    [Fact]
    public void MissingWorldMappingAllocatesAndRoundTripsWithoutChangingAssignedIndices()
    {
        var project = XElement.Parse("<Project><Subsystems /><Entities /></Project>");
        var initial = BlockIndexMapping.FromXml(project.Element("BlockRegistry"));
        using var first = new BlockContentCatalog(BlockRuntimeCatalog.Compile(Definitions(), savedMapping: initial));
        project.Add(first.Registry.Mapping.ToXml());
        var saved = BlockIndexMapping.FromXml(XElement.Parse(project.ToString()).Element("BlockRegistry"));
        using var restored = new BlockContentCatalog(BlockRuntimeCatalog.Compile(Definitions(), savedMapping: saved));

        var id = ResourceId.Parse("test:machine");
        Assert.Equal(first.GetBlock(id).BlockIndex, restored.GetBlock(id).BlockIndex);
        Assert.NotEmpty(saved.Entries);
    }

    [Fact]
    public void ServerMappingMustIncludeEveryRegisteredModBlock()
    {
        var registry = Definitions();
        Assert.Throws<InvalidDataException>(() => BlockRuntimeCatalog.Compile(registry,
            savedMapping: BlockIndexMapping.Empty, requireExactMapping: true));
    }

    [Fact]
    public void AddingAResourcePreservesSerializedItemValuesAndWorldMapping()
    {
        using var original = Build(300);
        var persisted = new ValuesDictionary();
        var item = Terrain.MakeBlockValue(300, 0, 7);
        persisted.SetValue("Value", item);
        var encoded = persisted.ToMessagePack();
        var mapping = BlockIndexMapping.FromXml(XElement.Parse(original.Registry.Mapping.ToXml().ToString()));
        var definitions = Definitions();
        definitions.Register(new ModId("another"), ResourceId.Parse("another:new_block"),
            new BlockRegistration(static () => new AirBlock()));
        using var updated = new BlockContentCatalog(BlockRuntimeCatalog.Compile(definitions, savedMapping: mapping));
        var restored = new ValuesDictionary();
        restored.ApplyOverridesUseMessagePack(encoded);
        var restoredValue = restored.GetValue<int>("Value");
        Assert.Equal(item, restoredValue);
        Assert.Equal(7, Terrain.ExtractData(restoredValue));
        Assert.Same(updated.GetBlock(ResourceId.Parse("test:machine")),
            updated.Blocks[Terrain.ExtractContents(restoredValue)]);
        Assert.NotEqual(300, updated.GetBlock(ResourceId.Parse("another:new_block")).BlockIndex);
    }

    [Fact]
    public void WorldBehaviorBindingResolvesResourcesAndDeduplicatesBothDeclarations()
    {
        using var world = Build(300);
        var id = ResourceId.Parse("test:machine");
        world.GetBlock(id).Behaviors = "MachineBehavior";
        var behavior = new TestBehavior(id);
        var table = SubsystemBlockBehaviors.BuildTable(world, [behavior], _ => behavior);
        Assert.Same(behavior, Assert.Single(table[300]));
        Assert.Empty(table[264]);
        Assert.Throws<KeyNotFoundException>(() => SubsystemBlockBehaviors.BuildTable(world,
            [new TestBehavior(ResourceId.Parse("test:unknown"))], _ => behavior));
    }

    [Fact]
    public void DisposedWorldCatalogCanBeCollectedWhilePreviewRemainsAlive()
    {
        using var preview = Build(264);
        var world = CreateDisposedWorld();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(world.IsAlive);
        Assert.Equal(264, preview.GetBlock(ResourceId.Parse("test:machine")).BlockIndex);
        GC.KeepAlive(preview);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedWorld()
    {
        var world = Build(300);
        var reference = new WeakReference(world);
        var block = (TestBlock)world.GetBlock(ResourceId.Parse("test:machine"));
        world.Dispose();
        world.Dispose();
        Assert.Equal(1, block.DisposeCount);
        return reference;
    }

    private sealed class TestBehavior(ResourceId id) : SubsystemBlockBehavior
    {
        public override ResourceId[] HandledBlocks => [id];
    }

    [Fact]
    public void VanillaDerivedBlocksGenerateTheirAllocatedCreativeIndices()
    {
        var registry = new NamespacedRegistry<BlockRegistration>();
        registry.Register(new ModId("game"), ResourceId.Parse("game:air"),
            new BlockRegistration(static () => new AirBlock()));
        registry.Register(new ModId("test"), ResourceId.Parse("test:paint"),
            new BlockRegistration(static () => new PaintBucketBlock()));
        registry.Register(new ModId("test"), ResourceId.Parse("test:sapling"),
            new BlockRegistration(static () => new SaplingBlock()));
        registry.Register(new ModId("test"), ResourceId.Parse("test:plate"),
            new BlockRegistration(static () => new PressurePlateBlock()));
        registry.Register(new ModId("test"), ResourceId.Parse("test:wire"),
            new BlockRegistration(static () => new WireBlock()));
        var mapping = new BlockIndexMapping([
            new(ResourceId.Parse("test:paint"), 300),
            new(ResourceId.Parse("test:sapling"), 301),
            new(ResourceId.Parse("test:plate"), 302),
            new(ResourceId.Parse("test:wire"), 303)
        ]);
        using var catalog = new BlockContentCatalog(BlockRuntimeCatalog.Compile(registry, savedMapping: mapping));
        foreach (var (id, index) in mapping.Entries)
        {
            var values = catalog.GetBlock(id).GetCreativeValues().ToArray();
            Assert.NotEmpty(values);
            Assert.All(values, value => Assert.Equal(index, Terrain.ExtractContents(value)));
        }

        var wire = (WireBlock)catalog.GetBlock(ResourceId.Parse("test:wire"));
        var wiredValue = wire.SetWireFacesBitmask(Terrain.MakeBlockValue(303), 5);
        Assert.Equal(303, Terrain.ExtractContents(wiredValue));
        Assert.Equal(5, wire.GetWireFacesBitmask(wiredValue));
        Assert.True(wire.WireExistsOnFace(wiredValue, 2));
        Assert.Equal(0, wire.GetWireFacesBitmask(Terrain.MakeBlockValue(133, 0, 5)));
    }

    [Fact]
    public void ReusedFactoryInstanceIsRejectedWithoutDisposingPreviewBlock()
    {
        var shared = new TestBlock();
        var registry = new NamespacedRegistry<BlockRegistration>();
        registry.Register(new ModId("game"), ResourceId.Parse("game:air"),
            new BlockRegistration(static () => new AirBlock()));
        registry.Register(new ModId("test"), ResourceId.Parse("test:machine"), new BlockRegistration(() => shared));
        using var preview = new BlockContentCatalog(BlockRuntimeCatalog.Compile(registry));
        Assert.Throws<InvalidOperationException>(() => BlockRuntimeCatalog.Compile(registry));
        Assert.Same(shared, preview.GetBlock(ResourceId.Parse("test:machine")));
        Assert.Equal(0, shared.DisposeCount);
    }

    private static NamespacedRegistry<BlockRegistration> Definitions()
    {
        var registry = new NamespacedRegistry<BlockRegistration>();
        registry.Register(new ModId("game"), ResourceId.Parse("game:air"), new BlockRegistration(static () => new AirBlock()));
        registry.Register(new ModId("test"), ResourceId.Parse("test:machine"), new BlockRegistration(static () => new TestBlock()));
        return registry;
    }

    private static BlockContentCatalog Build(int index)
    {
        var mapping = new BlockIndexMapping([new(ResourceId.Parse("test:machine"), index)]);
        var registry = BlockRuntimeCatalog.Compile(Definitions(), savedMapping: mapping);
        var catalog = new BlockContentCatalog(registry);
        catalog.LoadBlocksData("Id;DisplayName;DropContent\ntest:machine;Machine;#");
        catalog.Initialize(null, XElement.Parse("""
            <Recipes>
              <Recipe Result="test:machine@7" ResultCount="1" Remains="test:machine" RemainsCount="1"
                      Description="Machine" RequiredHeatLevel="0" a="machine">"a"</Recipe>
            </Recipes>
            """));
        Assert.Equal(index, catalog.GetBlock(ResourceId.Parse("test:machine")).DropContent);
        return catalog;
    }

    private sealed class TestBlock : AirBlock
    {
        public int InitializeCount { get; private set; }

        public int DisposeCount { get; private set; }

        public override void Dispose() => DisposeCount++;

        public TestBlock()
        {
            CraftingId = "machine";
            MaxStacking = 64;
        }

        public override void Initialize()
        {
            Assert.InRange(BlockIndex, 0, 1023);
            Assert.Same(this, Catalog.GetBlock(ResourceId.Parse("test:machine")));
            InitializeCount++;
        }
    }
}

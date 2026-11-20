using System.Xml.Linq;

using Game.Blocks;
using Game.Blocks.Collision;
using Game.Modding.Blocks;

namespace Survivalcraft.Test.Modding;

public sealed class BlockCatalogInitializationTest
{
    [Fact]
    public void MaterializedDataInitializesOnceWithoutAnyHostRegistryOrReader()
    {
        var block = new TrackingBlock();
        var registry = BlockRuntimeCatalog.Compile(
            [
                new(ResourceId.Parse("game:air"), new BlockRegistration(static () => new AirBlock())),
                new(ResourceId.Parse("test:tracked"), new BlockRegistration(() => block))
            ],
            [new BlockDataEntry(ResourceId.Parse("test:rules"), "Id;Durability\ntest:tracked;37")]
        );
        using var catalog = new BlockContentCatalog(registry);
        var geometry = new CollisionGeometryCatalog([]);

        catalog.Initialize(geometry, null, new XElement("Recipes"));

        Assert.Equal(37, block.Durability);
        Assert.Equal(1, block.Initialized);
        Assert.Same(geometry, catalog.CollisionGeometry);
        Assert.Throws<InvalidOperationException>(() => catalog.Initialize(geometry, null, new XElement("Recipes")));
        Assert.Equal(1, block.Initialized);
    }

    [Fact]
    public void FailedInitializationDisposesTheActualBlocksAndCannotBeRetried()
    {
        var first = new TrackingBlock();
        var failure = new TrackingBlock { Fails = true };
        var registry = BlockRuntimeCatalog.Compile(
            [
                new(ResourceId.Parse("game:air"), new BlockRegistration(() => first)),
                new(ResourceId.Parse("test:failure"), new BlockRegistration(() => failure))
            ]
        );
        using var catalog = new BlockContentCatalog(registry);
        Assert.Throws<InvalidOperationException>(() => catalog.Initialize(
                new CollisionGeometryCatalog([]),
                null,
                new XElement("Recipes")
            )
        );
        Assert.Equal(1, first.Disposed);
        Assert.Equal(1, failure.Disposed);
        Assert.Throws<ObjectDisposedException>(() => catalog.Initialize(
                new CollisionGeometryCatalog([]),
                null,
                new XElement("Recipes")
            )
        );
        catalog.Dispose();
        Assert.Equal(1, first.Disposed);
        Assert.Equal(1, failure.Disposed);
    }

    private sealed class TrackingBlock : AirBlock
    {
        public int Initialized { get; private set; }

        public int Disposed { get; private set; }

        public bool Fails { get; init; }

        public override void Initialize()
        {
            Initialized++;
            if (Fails) { throw new InvalidOperationException("Initialization fixture."); }
        }

        public override void Dispose()
        {
            Disposed++;
        }
    }
}

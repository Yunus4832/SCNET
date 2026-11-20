using System.Collections;
using System.Reflection;
using System.Xml.Linq;

using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Blocks.Collision;
using Game.Blocks.Presentation;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Rendering;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Subsystems;

public sealed class MovingBlocksOwnershipTest
{
    [Fact]
    public void AuthoritySetLifecycleAndCollisionFactsDoNotRequirePresentation()
    {
        using var fixture = new Fixture(RuntimeSide.Authority, false);
        var added = 0;
        var removed = 0;
        fixture.Moving.Added += set =>
        {
            added++;
            Assert.Single(set.Blocks);
        };
        fixture.Moving.Removed += _ => removed++;
        var set = fixture.CreateSet();
        Assert.Equal(1, added);
        Assert.Single(fixture.Moving.MovingBlockSets);
        Assert.NotNull(fixture.Moving.Raycast(new Vector3(0, 64.5f, 1.5f), new Vector3(3, 64.5f, 1.5f), true));
        var revision = set.ContentRevision;
        ((IMovingBlockSet)set).SetBlock(new Point3(1, 0, 0), fixture.BlockValue);
        Assert.Equal(revision + 1, set.ContentRevision);
        Assert.Equal(2, set.Box.Width);
        fixture.Moving.RemoveMovingBlockSetLogic(set);
        fixture.Moving.RemoveMovingBlockSetLogic(set);
        Assert.Empty(fixture.Moving.MovingBlockSets);
        Assert.Equal(1, removed);
    }

    [Fact]
    public void ActualReplicaHandlerPublishesOneLifecycleEventForDuplicateBaseline()
    {
        using var fixture = new Fixture(RuntimeSide.Client, false);
        var set = fixture.CreateSet();
        var package = MovingBlockPackageBuilder.Capture(set);
        var node = Game.Network.WorldNetworkBinding.For(fixture.Project).RequireNode();
        var added = 0;
        fixture.Moving.Added += _ => added++;
        var handler = new MovingBlockPackageHandler();
        var context = new PackageReceiveContext(node, node.Runtime, null);
        handler.Handle(package, context);
        handler.Handle(package, context);
        Assert.Equal(1, added);
        Assert.Single(fixture.Moving.MovingBlockSets);
    }

    [Fact]
    public void ClientMeshCacheRebuildsOnContentRevisionWithoutWritingRuleState()
    {
        using var fixture = new Fixture(RuntimeSide.Client, true);
        var set = fixture.CreateSet();
        fixture.Moving.AddReplicaMovingBlockSet(set);
        var state = fixture.States[set]!;
        fixture.Generate(set, state);
        Assert.True(Count(Get(state, "Indices")!) > 0);
        var sample = (Point3)Get(state, "GeometryGenerationPosition")!;
        var priorRevision = set.ContentRevision;
        ((IMovingBlockSet)set).SetBlock(Point3.Zero, 0);
        fixture.Generate(set, state);
        Assert.Equal(priorRevision + 1, set.ContentRevision);
        Assert.Equal(set.ContentRevision, Get(state, "ContentRevision"));
        Assert.Equal(sample, Get(state, "GeometryGenerationPosition"));
        Assert.Equal(0, Count(Get(state, "Indices")!));
        Assert.Equal(new Vector3(1, 64, 1), set.Position);
        Assert.Equal(new Vector3(2, 64, 1), set.TargetPosition);
        Assert.False(set.Stop);
    }

    [Fact]
    public void RemovedVisualLifetimeAdvancesWithoutDrawAndNeverMutatesTheSet()
    {
        using var fixture = new Fixture(RuntimeSide.Client, true);
        var set = fixture.CreateSet();
        fixture.Moving.AddReplicaMovingBlockSet(set);
        fixture.Moving.RemoveMovingBlockSetLogic(set);
        Assert.Single(fixture.States);
        ((IMovingBlockSet)set).SetBlock(Point3.Zero, 0);
        for (var i = 0; i < 5; i++) { fixture.Presentation!.Draw(null!, 150); }

        Assert.Single(fixture.States);
        for (var i = 0; i < 5; i++) { fixture.Presentation!.Update(.016f); }

        Assert.Empty(fixture.States);
        Assert.Equal(new Vector3(1, 64, 1), set.Position);
        Assert.False(set.Stop);
        Assert.Empty(set.Blocks);
    }

    [Fact]
    public void SharedRulesAndMovingFactsHaveNoRenderingFields()
    {
        Assert.False(typeof(IDrawable).IsAssignableFrom(typeof(SubsystemMovingBlocks)));
        foreach (var type in new[] { typeof(SubsystemMovingBlocks), typeof(SubsystemMovingBlocks.MovingBlockSet) })
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var name = field.FieldType.FullName ?? string.Empty;
                Assert.DoesNotContain("Engine.Graphics", name);
                Assert.DoesNotContain("TerrainGeometry", name);
                Assert.DoesNotContain("TerrainVertex", name);
                Assert.DoesNotContain("BlockGeometryGenerator", name);
            }
        }
    }

    private static int Count(object array) => (int)array.GetType().GetProperty("Count")!.GetValue(array)!;

    private static object? Get(object instance, string name) => instance.GetType().GetField(
        name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    )!.GetValue(instance);

    private sealed class Fixture : IDisposable
    {
        public Fixture(RuntimeSide side, bool withPresentation)
        {
            Project = new RuntimeTestProject(side);
            var registry = new NamespacedRegistry<BlockRegistration>();
            registry.Register(
                new ModId("game"),
                ResourceId.Parse("game:air"),
                new BlockRegistration(static () => new AirBlock { Transparent = true })
            );
            registry.Register(
                new ModId("test"),
                ResourceId.Parse("test:cube"),
                new BlockRegistration(static () => new TestCube())
            );
            Rules = new BlockContentCatalog(BlockRuntimeCatalog.Compile(registry.Entries));
            Rules.Initialize(new CollisionGeometryCatalog([]), null, new XElement("Recipes"));
            _ = new WorldContentBinding(WorldRuntimeContext.For(Project), Rules, () => { });
            BlockValue = Terrain.MakeBlockValue(Rules.GetBlock(ResourceId.Parse("test:cube")).BlockIndex);
            Project.AddSubsystem(Moving);
            var terrain = new Terrain();
            terrain.AllocateChunk(0, 0);
            Project.AddSubsystem(new SubsystemTerrain { Terrain = terrain });
            if (!withPresentation) { return; }

            Project.AddSubsystem(new SubsystemAnimatedTextures());
            Project.AddSubsystem(new SubsystemSkyPresentation());
            Project.AddSubsystem(new SubsystemElectricity());
            Project.AddSubsystem(new SubsystemFurnitureBlockBehavior());
            Project.AddSubsystem(new SubsystemMetersBlockBehavior());
            Project.AddSubsystem(new SubsystemPalette());
            var owner = new SubsystemBlockPresentation();
            Project.AddSubsystem(owner);
            owner.Bind(
                new BlockPresentationCatalog(
                    Rules,
                    [
                        new(
                            ResourceId.Parse("game:air"),
                            static (block, catalog) => new AirBlockPresentation((AirBlock)block, catalog)
                        ),
                        new(
                            ResourceId.Parse("test:cube"),
                            static (block, catalog) => new TestCubePresentation((TestCube)block, catalog)
                        )
                    ]
                )
            );
            Presentation = new SubsystemMovingBlocksPresentation();
            Project.AddSubsystem(Presentation);
            Presentation.Load(new EntitySystem.TemplatesDatabase.ValuesDictionary());
        }

        public RuntimeTestProject Project { get; }

        public BlockContentCatalog Rules { get; }

        public int BlockValue { get; }

        public SubsystemMovingBlocks Moving { get; } = new();

        public SubsystemMovingBlocksPresentation? Presentation { get; }

        public IDictionary States => (IDictionary)Get(Presentation!, "_states")!;

        public SubsystemMovingBlocks.MovingBlockSet CreateSet() =>
            (SubsystemMovingBlocks.MovingBlockSet)Moving.AddMovingBlockSet(
                new Vector3(1, 64, 1),
                new Vector3(2, 64, 1),
                1f,
                0f,
                0f,
                Vector2.Zero,
                [new MovingBlock { Offset = Point3.Zero, Value = BlockValue }],
                "test",
                "tag",
                false
            )!;

        public void Generate(SubsystemMovingBlocks.MovingBlockSet set, object state) =>
            typeof(SubsystemMovingBlocksPresentation).GetMethod(
                "GenerateGeometry",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.Invoke(Presentation, [set, state]);

        public void Dispose()
        {
            Project.Dispose();
            Rules.Dispose();
        }
    }

    private sealed class TestCube : CubeBlock
    {
    }

    private sealed class TestCubePresentation(TestCube block, BlockPresentationCatalog catalog)
        : CubeBlockPresentation(block, catalog)
    {
    }
}

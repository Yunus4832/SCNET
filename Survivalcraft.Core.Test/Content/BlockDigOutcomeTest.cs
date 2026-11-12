using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Components;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Transport;
using Game.Protocol;
using Game.Protocol.Terrain;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.Terrains.Lighting;
using Game.World;

using Survivalcraft.Core.Test.Fixtures;

namespace Survivalcraft.Core.Test.Content;

public sealed class BlockDigOutcomeTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObserverShakeUsesTheExplicitOldHitBeforeOrAfterTheCellDelta(bool deltaArrived)
    {
        using var project = new CoreTestProject(RuntimeSide.Client);
        var terrain = new SubsystemTerrain();
        project.InitializeSubsystem(terrain);
        using var leaves = new OakLeavesBlock();
        var oldValue = AutumnLeaves(12);
        var expected = leaves.GetDigValue(terrain, oldValue, 0, Hit(oldValue));
        var current = deltaArrived ? expected.Value : oldValue;
        var observed = ComponentMiner.SelectDigSourceValue(false, true, current, oldValue);
        var dig = leaves.GetDigValue(terrain, observed, 0, Hit(observed));
        Assert.Equal(expected, dig);
        Assert.Equal(BlockDigOutcomeKind.LeavesShaken, dig.Kind);
        Assert.False(leaves.AllowsDestructionDebris(observed, dig.Value));
        Assert.False(ComponentMiner.ApplyDigRuleEffects(terrain, observed, dig));
    }

    [Fact]
    public void AuthorityUsesCurrentContentsInsteadOfAForgedOldHit()
    {
        using var leaves = new OakLeavesBlock();
        var claimed = AutumnLeaves(12);
        var winter = leaves.GetDigValue(null!, claimed, 0, Hit(claimed)).Value;
        var source = ComponentMiner.SelectDigSourceValue(true, true, winter, claimed);
        Assert.Equal(winter, source);
        Assert.Equal(BlockDigOutcomeKind.BlockChanged, leaves.GetDigValue(null!, source, 0, Hit(claimed)).Kind);
        Assert.Equal(winter, ComponentMiner.SelectDigSourceValue(false, false, winter, claimed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityFallenLeavesRequireTheShakeToHaveActuallyBeenInstalled(bool installed)
    {
        using var fixture = new ShakeWorld();
        var leaves = (DeciduousLeavesBlock)fixture.Scope.Catalog.GetBlock(ResourceId.Parse("game:oak_leaves"));
        var oldValue = AutumnLeaves(leaves.BlockIndex);
        var dig = leaves.GetDigValue(fixture.TerrainSubsystem, oldValue, 0, Hit(oldValue));
        fixture.Terrain.SetCellValueFast(1, 5, 1, installed ? dig.Value : oldValue);
        Assert.Equal(installed, ComponentMiner.ApplyDigRuleEffects(fixture.TerrainSubsystem, oldValue, dig));
        var groundCover = Terrain.ExtractContents(fixture.Terrain.GetCellValue(1, 2, 1));
        var fallen = fixture.Scope.Catalog.GetBlock(ResourceId.Parse("game:fallen_leaves")).BlockIndex;
        Assert.Equal(installed ? fallen : 0, groundCover);
        Assert.Equal(installed ? 1 : 0, fixture.Chunk.ModificationCounter);
        Assert.False(WorldRuntimeContext.For(fixture.Scope.Project).HasPresentation);
        Assert.Equal(installed ? 1 : 0, fixture.Scope.Messages.ChunkMessages.Count);
        if (installed)
        {
            var current = fixture.Terrain.GetCellValue(1, 5, 1);
            var source = ComponentMiner.SelectDigSourceValue(true, true, current, oldValue);
            var repeated = leaves.GetDigValue(fixture.TerrainSubsystem, source, 0, Hit(oldValue));
            Assert.False(ComponentMiner.ApplyDigRuleEffects(fixture.TerrainSubsystem, source, repeated));
            Assert.Equal(1, fixture.Chunk.ModificationCounter);
        }
    }

    [Fact]
    public void UnpublishedGenerationEditsEnterTheBaselineWithoutPublishingLiveDeltas()
    {
        using var fixture = new ShakeWorld();
        fixture.Chunk.HasContents = false;
        fixture.Chunk.IsContentInitialized = false;
        fixture.TerrainSubsystem.ChangeCellNet(1, 3, 1, 7);
        Assert.Equal(7, Terrain.ExtractContents(fixture.Terrain.GetCellValue(1, 3, 1)));
        Assert.Empty(fixture.Scope.Messages.ChunkMessages);
        fixture.Chunk.HasContents = true;
        fixture.Chunk.IsContentInitialized = true;
        fixture.TerrainSubsystem.ChangeCellNet(1, 3, 1, 0);
        Assert.Single(fixture.Scope.Messages.ChunkMessages);
    }

    [Fact]
    public void ACompleteInitialNormalizationBurstIsContainedInTheFirstBaseline()
    {
        using var fixture = new ShakeWorld();
        fixture.Chunk.IsContentInitialized = false;
        fixture.Chunk.AreBehaviorsNotified = true;
        var block = fixture.Scope.Catalog.GetBlock(ResourceId.Parse("game:granite")).BlockIndex;
        for (var i = 0; i < 300; i++)
        {
            fixture.TerrainSubsystem.ChangeCellNet((i / 16) % 16, 3 + i / 256, i % 16, Terrain.MakeBlockValue(block));
        }

        Assert.Empty(fixture.Scope.Messages.ChunkMessages);
        Assert.False(fixture.TerrainSubsystem.ChunkContentAuthority!.TryGetSnapshot(fixture.Chunk.Coords, out _));
        fixture.Chunk.IsContentInitialized = true;
        Assert.True(
            fixture.TerrainSubsystem.ChunkContentAuthority.TryGetSnapshot(fixture.Chunk.Coords, out var baseline)
        );
        Assert.Equal(fixture.Chunk.NetworkContentRevision + 1, baseline.ContentVersion);
        for (var i = 0; i < 300; i++)
        {
            Assert.Equal(
                block,
                Terrain.ExtractContents(
                    baseline.Cells.Span[
                        TerrainChunk.CalculateCellIndex((i / 16) % 16, 3 + i / 256, i % 16)]
                )
            );
        }

        fixture.TerrainSubsystem.ChangeCellNet(0, 3, 0, 0);
        Assert.Single(fixture.Scope.Messages.ChunkMessages);
    }

    [Fact]
    public void OrdinaryDebrisPermissionsMatchFreshDropGenerationWithoutRepeatingRandomDrops()
    {
        using var scope = new CoreBlockWorld();
        var cairn = scope.Catalog.GetBlock(ResourceId.Parse("game:cairn"));
        Check(cairn, Terrain.MakeBlockValue(cairn.BlockIndex), 0);
        var slab = (SlabBlock)scope.Catalog.GetBlock(ResourceId.Parse("game:basalt_slab"));
        Check(slab, Terrain.MakeBlockValue(slab.BlockIndex), Terrain.MakeBlockValue(slab.FullBlockIndex));
        Check(slab, Terrain.MakeBlockValue(slab.BlockIndex), 0);
        var wire = (WireBlock)scope.Catalog.GetBlock(ResourceId.Parse("game:wire"));
        var oldWire = wire.SetWireFacesBitmask(Terrain.MakeBlockValue(wire.BlockIndex), 3);
        Check(wire, oldWire, oldWire);
        Check(wire, oldWire, wire.SetWireFacesBitmask(oldWire, 1));

        static void Check(Block block, int oldValue, int newValue)
        {
            var drops = new List<BlockDropValue>();
            block.GetDropValues(null!, oldValue, newValue, 0, drops, out var allowed);
            Assert.Equal(allowed, block.AllowsDestructionDebris(oldValue, newValue));
        }
    }

    private static int AutumnLeaves(int index)
    {
        var data = DeciduousLeavesBlock.SetTimeOfSeason(0, 0.8f);
        return Terrain.MakeBlockValue(index, DeciduousLeavesBlock.SetSeason(data, Season.Autumn));
    }

    private static TerrainRaycastResult Hit(int value) => new() { Value = value, CellFace = new CellFace(1, 5, 1, 4) };

    private static BlockDigOutcome Outcome(int oldValue, BlockDigData dig) =>
        new(oldValue, dig.Value, dig.CellFace, new Vector3(1.5f, 5.5f, 1.5f), dig.Kind, false);

    private sealed class ShakeWorld : IDisposable
    {
        public CoreBlockWorld Scope { get; } = new();

        public Terrain Terrain { get; } = new();

        public TerrainChunk Chunk { get; }

        public SubsystemTerrain TerrainSubsystem { get; }

        private readonly TerrainComputationScheduler _computations;

        public ShakeWorld()
        {
            Scope.Catalog.GetBlock(ResourceId.Parse("game:air")).Transparent = true;
            Chunk = Terrain.AllocateChunk(0, 0);
            Chunk.HasContents = true;
            Chunk.IsContentInitialized = true;
            Chunk.ContentOrigin = TerrainContentOrigin.Storage;
            Terrain.SetCellValueFast(1, 1, 1, Scope.Catalog.GetBlock(ResourceId.Parse("game:granite")).BlockIndex);
            TerrainSubsystem = new SubsystemTerrain { Terrain = Terrain };
            Scope.Project.InitializeSubsystem(TerrainSubsystem);
            var info = new SubsystemGameInfo { WorldSettings = new WorldSettings() };
            Scope.Project.AddSubsystem(info);
            Scope.Project.AddSubsystem(new SubsystemTime());
            Scope.Project.AddSubsystem(new SubsystemSky());
            Scope.Project.AddSubsystem(new SubsystemNetworkInterest());
            var queue = new SubsystemCellChangeQueue();
            Scope.Project.AddSubsystem(queue);
            Set(queue, "_subsystemTerrain", TerrainSubsystem);
            var leaves = new SubsystemDeciduousLeavesBlockBehavior();
            Scope.Project.AddSubsystem(leaves);
            Set(leaves, "_subsystemTerrain", TerrainSubsystem);
            typeof(SubsystemBlockBehavior).GetProperty(
                    "SubsystemTerrain",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )!
                .SetValue(leaves, TerrainSubsystem);
            Set(leaves, "_subsystemCellChangeQueue", queue);
            var behaviors = new SubsystemBlockBehaviors();
            Scope.Project.AddSubsystem(behaviors);
            behaviors.Load(new ValuesDictionary());
            Set(TerrainSubsystem, "_subsystemBlockBehaviors", behaviors);
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellAuthority))!
                .SetValue(TerrainSubsystem, new TerrainCellAuthority(Terrain));
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.ChunkContentAuthority))!
                .SetValue(TerrainSubsystem, new TerrainChunkContentAuthority(Terrain));
            _computations = new TerrainComputationScheduler(
                TerrainSubsystem,
                new TerrainLightingCalculator(
                    Terrain,
                    Scope.Catalog,
                    false,
                    chunk => chunk.HasContents && !chunk.NeedsContentValidation
                )
            );
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Computations))!
                .SetValue(TerrainSubsystem, _computations);
        }

        public void Dispose()
        {
            TerrainSubsystem.WorkExecutor.Dispose();
            _computations.Dispose();
            Scope.Dispose();
            Terrain.Dispose();
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
        }
    }

    private sealed class CoreBlockWorld : IDisposable
    {
        public CoreTestProject Project { get; } = new(RuntimeSide.Authority);

        public BlockContentCatalog Catalog { get; }

        public RecordingSink Messages { get; } = new();

        public CoreBlockWorld()
        {
            var registrations = BuiltInBlockRegistry.Entries.Select(entry =>
                new KeyValuePair<ResourceId, BlockRegistration>(
                    new ResourceId(BuiltInBlockIdentities.Namespace, entry.Path),
                    new BlockRegistration(entry.Factory)
                )
            ).ToArray();
            Catalog = new BlockContentCatalog(BlockRuntimeCatalog.Compile(registrations));
            var world = WorldRuntimeContext.For(Project);
            _ = new WorldContentBinding(world, Catalog, Catalog.Dispose);
            _ = new WorldConnections(world);
            _ = new WorldMessageEndpoint(world, Messages);
            var gameplay = new GameplayHooks();
            var blocks = new BlockBehaviorHooks();
            gameplay.Freeze();
            blocks.Freeze();
            _ = new WorldRuleHooks(world, gameplay, blocks);
        }

        public void Dispose() => Project.Dispose();
    }

    public sealed class RecordingSink : IWorldMessageSink
    {
        public List<(IProtocolMessage Message, PackageAudience Audience)> Messages { get; } = [];

        public void Send(IProtocolMessage message, PackageAudience audience)
        {
            Messages.Add((message, audience));
        }

        public void SendNearPoint(Vector3 position, float radius, IProtocolMessage message) =>
            throw new NotSupportedException();

        public List<(Point2 Chunk, IProtocolMessage Message)> ChunkMessages { get; } = [];

        public void SendToChunkObservers(Point2 chunk, IProtocolMessage message) => ChunkMessages.Add((chunk, message));

        public void SendToObservers(Entity entity, IProtocolMessage message, WorldConnection? except) =>
            throw new NotSupportedException();

        public bool CanQueueTerrainSnapshot(WorldConnection recipient) => throw new NotSupportedException();

        public bool TryQueueOwnedTerrainSnapshot(WorldConnection recipient, TerrainSnapshotMessage message) =>
            throw new NotSupportedException();

        public void DiscardOlderChunkAllocations(WorldConnection recipient, ChunkAllocationId allocation) =>
            throw new NotSupportedException();

        public void TrimChunkDelivery(WorldConnection recipient, Vector2 center, float distance) =>
            throw new NotSupportedException();

        public void ReleaseChunkDelivery(WorldConnection recipient) => throw new NotSupportedException();

        public void InvalidateChunkDelivery(Point2 coords) => throw new NotSupportedException();

        public void DiscardQueuedChunkRequests(ChunkAllocationId allocation) => throw new NotSupportedException();
    }
}

using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Blocks;
using Game.Network;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

using Random = Engine.Core.Mathematics.Random;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class DeciduousLeavesOwnershipTest
{
    [Fact]
    public void AuthoritySeasonAndFallenLeafChangesQueueWithoutViewsOrParticles()
    {
        using var fixture = new LeafWorld(RuntimeSide.Authority, false);
        var block = fixture.Block;
        var oldValue = Autumn(block.BlockIndex);
        fixture.Info.WorldSettings.TimeOfYear = block.WinterStart + .01f;
        fixture.Terrain.SetCellValueFast(1, 3, 1, oldValue);
        fixture.Terrain.SetCellValueFast(
            1,
            1,
            1,
            fixture.Scope.Catalog.GetBlock(ResourceId.Parse("game:granite")).BlockIndex
        );
        var random = block.SharedRandom1.State;

        fixture.Rules.OnPoll(oldValue, 1, 3, 1, 0);

        Assert.Equal(random, block.SharedRandom1.State);
        Assert.Equal(2, fixture.Queue.PendingChangesCount);
        Assert.Equal(oldValue, fixture.Terrain.GetCellValue(1, 3, 1));
        Assert.Null(fixture.Scope.Project.FindSubsystem<SubsystemGameWidgets>());
        Assert.Null(fixture.Scope.Project.FindSubsystem<SubsystemParticles>());
    }

    [Fact]
    public void ClientGenerationAndPollingReportCellsWithoutQueuingSeasonOrFallenTerrainChanges()
    {
        using var fixture = new LeafWorld(RuntimeSide.Client, false);
        var value = Autumn(fixture.Block.BlockIndex);
        fixture.Info.WorldSettings.TimeOfYear = fixture.Block.WinterStart + .01f;
        fixture.Terrain.SetCellValueFast(1, 3, 1, value);
        var observed = new List<(int Value, Point3 Point)>();
        fixture.Rules.LeafCellVisited += (cell, point) => observed.Add((cell, point));
        var random = fixture.Block.SharedRandom1.State;

        fixture.Rules.OnBlockGenerated(value, 1, 3, 1, true);
        fixture.Rules.OnPoll(value, 1, 3, 1, 0);
        fixture.Rules.CreateFallenLeaves(new Point3(1, 3, 1), false);

        Assert.Equal(2, observed.Count);
        Assert.All(observed, visit => Assert.Equal((value, new Point3(1, 3, 1)), visit));
        Assert.Equal(0, fixture.Queue.PendingChangesCount);
        Assert.Equal(value, fixture.Terrain.GetCellValue(1, 3, 1));
        Assert.Equal(random, fixture.Block.SharedRandom1.State);
    }

    [Fact]
    public void ClientOwnsVisualRandomAndCapsRepeatedObservationsAndDetachesOnClose()
    {
        using var fixture = new LeafWorld(RuntimeSide.Client, true);
        var project = fixture.Scope.Project;
        project.AddSubsystem(new SubsystemGameWidgets());
        project.AddSubsystem(new SubsystemParticles());
        var presentation = new SubsystemDeciduousLeavesPresentation();
        project.AddSubsystem(presentation);
        presentation.Load(new ValuesDictionary());
        var visualRandom = (Random)Field(presentation, "_random")!;
        visualRandom.Seed(8192);
        var expected = new Random(8192);
        var value = Autumn(fixture.Block.BlockIndex);
        expected.Bool(fixture.Block.GetLeafDropProbability(value) / 60f * 60f);
        var ruleRandom = fixture.Block.SharedRandom1.State;

        // Normal client chunk-ready notification reaches the owner; absent views cause no particle work.
        fixture.Rules.OnBlockGenerated(value, 1, 3, 1, true);
        Assert.Equal(expected.State, visualRandom.State);
        Assert.Equal(ruleRandom, fixture.Block.SharedRandom1.State);
        var pending = Field(presentation, "_leafParticles")!;
        var itemType = pending.GetType().GetGenericArguments()[0];
        var add = pending.GetType().GetMethod("Add", [itemType])!;
        var item = Activator.CreateInstance(itemType);
        for (var index = 0; index < 30000; index++) { add.Invoke(pending, [item]); }

        var beforeCap = visualRandom.State;
        for (var index = 0; index < 1000; index++) { fixture.Rules.OnPoll(value, 1, 3, 1, 0); }

        Assert.Equal(30000, (int)pending.GetType().GetProperty("Count")!.GetValue(pending)!);
        Assert.Equal(beforeCap, visualRandom.State);
        presentation.Dispose();
        Assert.Equal(0, (int)pending.GetType().GetProperty("Count")!.GetValue(pending)!);
        fixture.Rules.OnBlockGenerated(value, 1, 3, 1, true);
        presentation.Update(1f);
        Assert.Equal(beforeCap, visualRandom.State);
        Assert.Equal(0, fixture.Queue.PendingChangesCount);
    }

    private static int Autumn(int index)
    {
        var data = DeciduousLeavesBlock.SetTimeOfSeason(0, .8f);
        return Terrain.MakeBlockValue(index, DeciduousLeavesBlock.SetSeason(data, Season.Autumn));
    }

    private static object? Field(object owner, string name) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);

    private sealed class LeafWorld : IDisposable
    {
        public WorldCatalogScope Scope { get; }

        public Terrain Terrain { get; } = new();

        public SubsystemGameInfo Info { get; } = new() { WorldSettings = new WorldSettings() };

        public SubsystemCellChangeQueue Queue { get; } = new();

        public SubsystemDeciduousLeavesBlockBehavior Rules { get; } = new();

        public DeciduousLeavesBlock Block =>
            (DeciduousLeavesBlock)Scope.Catalog.GetBlock(ResourceId.Parse("game:oak_leaves"));

        public LeafWorld(RuntimeSide side, bool presentation)
        {
            var node = new NetNode(
                    NetNodeOptions.Default,
                    presentation,
                    Game.Network.Handling.PackageHandlerCatalog.Empty,
                    side == RuntimeSide.Authority ? NodeRole.Authority : NodeRole.Client
                )
                ;
            Scope = new WorldCatalogScope(node);
            var project = Scope.Project;
            Scope.Catalog.GetBlock(ResourceId.Parse("game:air")).Transparent = true;
            Terrain.AllocateChunk(0, 0).HasContents = true;
            project.AddSubsystem(new SubsystemTerrain { Terrain = Terrain });
            project.AddSubsystem(Info);
            project.AddSubsystem(new SubsystemTime());
            project.AddSubsystem(Queue);
            Queue.Load(new ValuesDictionary());
            project.AddSubsystem(Rules);
            Rules.Load(new ValuesDictionary());
        }

        public void Dispose() => Scope.Dispose();
    }
}

using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class BlocksScannerBudgetTest
{
    [Fact]
    public void BudgetYieldsBetweenColumnsAndRetainsTheSavedScanPosition()
    {
        using var fixture = new Fixture();
        fixture.Behavior.WhenPolled = () => fixture.Clock.Advance(TimeSpan.FromMilliseconds(4));
        fixture.Scanner.Update(0);
        Assert.Equal(new[] { new Point3(0, 4, 0) }, fixture.Behavior.Polled);
        var saved = new ValuesDictionary();
        fixture.Scanner.Save(saved);
        Assert.Equal(new Point2(0, 1), saved.GetValue<Point2>("PollPoint"));
        fixture.Scanner.Update(0);
        Assert.Equal(new[] { new Point3(0, 4, 0), new Point3(0, 4, 1) }, fixture.Behavior.Polled);
    }

    [Fact]
    public void UninitializedContentsAreSkippedWithoutCallingPollHandlers()
    {
        using var fixture = new Fixture();
        fixture.Chunk.IsContentInitialized = false;
        fixture.Scanner.Update(0);
        Assert.Empty(fixture.Behavior.Polled);
        Assert.Equal(-254f, fixture.PollCredits);
        var saved = new ValuesDictionary();
        fixture.Scanner.Save(saved);
        Assert.Equal(Point2.Zero, saved.GetValue<Point2>("PollPoint"));
    }

    [Fact]
    public void LostLightingConsumesColumnCreditsRatherThanCellCredits()
    {
        using var fixture = new Fixture();
        fixture.Chunk.DerivedRevisions.InvalidateLight();
        fixture.Scanner.Update(0);
        Assert.Empty(fixture.Behavior.Polled);
        Assert.Equal(-254f, fixture.PollCredits);
    }

    private sealed class Clock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }

    private sealed class Fixture : IDisposable
    {
        public WorldCatalogScope World { get; } = new(RuntimeSide.Authority);

        public Terrain Terrain { get; } = new();

        public Clock Clock { get; } = new();

        public PollBehavior Behavior { get; }

        public SubsystemBlocksScanner Scanner { get; }

        public TerrainChunk Chunk { get; }

        public float PollCredits => (float)typeof(SubsystemBlocksScanner)
            .GetField("_pollCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Scanner)!;

        public Fixture()
        {
            World.Project.AddSubsystem(new SubsystemTerrain { Terrain = Terrain });
            Assert.True(World.Catalog.TryGetBlockId(3, out var id));
            Behavior = new PollBehavior(id);
            World.Project.AddSubsystem(Behavior);
            Behavior.Load(new ValuesDictionary());
            var behaviors = new SubsystemBlockBehaviors();
            World.Project.AddSubsystem(behaviors);
            behaviors.Load(new ValuesDictionary());
            Scanner = new SubsystemBlocksScanner(Clock);
            World.Project.AddSubsystem(Scanner);
            Scanner.Load(
                new ValuesDictionary
                {
                    { "PollChunkCoordinates", Point2.Zero }, { "PollPoint", Point2.Zero }, { "PollPass", 0 }
                }
            );
            typeof(SubsystemBlocksScanner).GetField("_pollCount", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Scanner, 2f);
            Chunk = Terrain.AllocateChunk(0, 0);
            Chunk.IsContentInitialized = true;
            Chunk.SetCellValueFast(0, 4, 0, 3);
            Chunk.SetCellValueFast(0, 4, 1, 3);
            Chunk.SetTopHeightFast(0, 0, 4);
            Chunk.SetTopHeightFast(0, 1, 4);
            TerrainMeshTestData.PrepareLighting(Chunk);
        }

        public void Dispose()
        {
            try { World.Dispose(); }
            finally { Terrain.Dispose(); }
        }
    }

    private sealed class PollBehavior(ResourceId id) : SubsystemPollableBlockBehavior
    {
        public override ResourceId[] HandledBlocks => [id];

        public List<Point3> Polled { get; } = [];

        public Action? WhenPolled { get; set; }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            Polled.Add(new Point3(x, y, z));
            WhenPolled?.Invoke();
        }
    }
}

using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class IvyGrowthQueueTest
{
    [Fact]
    public void GrowthQueuesAtItsOriginalPeriodAndDoesNotOverwriteLaterConstruction()
    {
        using var fixture = new Fixture();
        fixture.Pending.Add(new Point3(1, 1, 1), 197);
        fixture.Ivy.Update(.1f);
        Assert.Equal(0, fixture.Queue.PendingChangesCount);
        fixture.NextGrowthPeriod();
        fixture.Ivy.Update(.1f);
        Assert.Empty(fixture.Pending);
        Assert.Equal(1, fixture.Queue.PendingChangesCount);
        Assert.Equal(0, fixture.Terrain.Terrain.GetCellValue(1, 1, 1));

        fixture.Terrain.Terrain.SetCellValueFast(1, 1, 1, 3);
        ((IUpdateable)fixture.Queue).Update(.1f);
        Assert.Equal(0, fixture.Queue.PendingChangesCount);
        Assert.Equal(3, fixture.Terrain.Terrain.GetCellValue(1, 1, 1));
    }

    [Fact]
    public void RejectedGrowthRemainsPendingAndOccupiedTargetsAreDiscarded()
    {
        using var fixture = new Fixture();
        fixture.Pending.Add(new Point3(1, 1, 1), 197);
        fixture.Pending.Add(new Point3(2, 1, 1), 197);
        fixture.Terrain.Terrain.SetCellValueFast(2, 1, 1, 3);
        fixture.Queue.Dispose();
        fixture.NextGrowthPeriod();
        fixture.Ivy.Update(.1f);
        Assert.Single(fixture.Pending);
        Assert.True(fixture.Pending.ContainsKey(new Point3(1, 1, 1)));
        Assert.Equal(0, fixture.Terrain.Terrain.GetCellValue(1, 1, 1));
        Assert.Equal(UpdateExecutionSide.Authority, fixture.Ivy.UpdateSide);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly WorldCatalogScope _world = new(RuntimeSide.Authority);

        public SubsystemTerrain Terrain { get; } = new() { Terrain = new Terrain() };

        public SubsystemTime Time { get; } = new();

        public SubsystemCellChangeQueue Queue { get; } = new();

        public SubsystemIvyBlockBehavior Ivy { get; } = new();

        public Dictionary<Point3, int> Pending => (Dictionary<Point3, int>)typeof(SubsystemIvyBlockBehavior)
            .GetField("_toUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Ivy)!;

        public Fixture()
        {
            _world.Project.AddSubsystem(Terrain);
            _world.Project.AddSubsystem(Time);
            _world.Project.AddSubsystem(Queue);
            _world.Project.AddSubsystem(Ivy);
            Terrain.Terrain.AllocateChunk(0, 0).HasContents = true;
            Queue.Load(new ValuesDictionary());
            Ivy.Load(new ValuesDictionary());
        }

        public void NextGrowthPeriod()
        {
            do { Time.NextFrame(.1f, static () => default); } while (Time.GameTime < 1 ||
                                                                     !Time.PeriodicGameTimeEvent(60, 0));
        }

        public void Dispose() => _world.Dispose();
    }
}

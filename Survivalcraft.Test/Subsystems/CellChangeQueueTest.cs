using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Subsystems;

[Collection(ConfigFileCollection.Name)]
public sealed class CellChangeQueueTest
{
    [Fact]
    public async Task BusyTerrainDefersTheBatchAndPreservesItsRequestUntilAFrameCanContinue()
    {
        using var fixture = new Fixture();
        Assert.True(fixture.Queue.TryQueueCellChange(1, 1, 1, 3));
        fixture.Terrain.Terrain.SetCellValueFast(1, 1, 1, 2); // Invalidate the conditional intent.
        fixture.StartPeriod();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() =>
            {
                fixture.Terrain.Access.Enter();
                try
                {
                    entered.Set();
                    release.Wait();
                }
                finally { fixture.Terrain.Access.Exit(); }
            }
        );
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var update = Task.Run(() => ((IUpdateable)fixture.Queue).Update(.1f));
        try
        {
            await update.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, fixture.Queue.PendingChangesCount);
            Assert.True(fixture.Terrain.WorkExecutor.HasMainRequest);
        }
        finally
        {
            release.Set();
            await Task.WhenAll(holder, update).WaitAsync(TimeSpan.FromSeconds(5));
        }

        ((IUpdateable)fixture.Queue).Update(.1f);
        Assert.Equal(0, fixture.Queue.PendingChangesCount);
        Assert.False(fixture.Terrain.WorkExecutor.HasMainRequest);
        Assert.Equal(2, fixture.Terrain.Terrain.GetCellValue(1, 1, 1));
    }

    [Fact]
    public void OrdinaryFramesContinueAnExistingBatchWithoutWaitingForAnotherTwentySecondPeriod()
    {
        using var fixture = new Fixture();
        for (var x = 0; x < 8; x++)
        {
            for (var z = 0; z < 8; z++)
            {
                Assert.True(fixture.Queue.TryQueueCellChange(x, 1, z, 3));
                fixture.Terrain.Terrain.SetCellValueFast(x, 1, z, 2);
            }
        }

        fixture.StartPeriod();
        ((IUpdateable)fixture.Queue).Update(.1f);
        Assert.InRange(fixture.Queue.PendingChangesCount, 32, 63);
        fixture.Time.NextFrame(.1f, static () => default);
        for (var frame = 0; frame < 64 && fixture.Queue.PendingChangesCount != 0; frame++)
        {
            ((IUpdateable)fixture.Queue).Update(.1f);
            fixture.Time.NextFrame(.1f, static () => default);
        }

        Assert.Equal(0, fixture.Queue.PendingChangesCount);
        Assert.False(fixture.Terrain.WorkExecutor.HasMainRequest);
    }

    [Fact]
    public void ClosingCancelsOnlyUncommittedNaturalIntentsAndStopsFurtherEnqueue()
    {
        using var fixture = new Fixture();
        Assert.True(fixture.Queue.TryQueueCellChange(1, 1, 1, 3));
        fixture.Queue.Dispose();
        Assert.Equal(0, fixture.Queue.PendingChangesCount);
        Assert.False(fixture.Queue.TryQueueCellChange(1, 1, 1, 4));
        Assert.Equal(1, fixture.Terrain.Terrain.GetCellValue(1, 1, 1));
        Assert.False(fixture.Terrain.WorkExecutor.HasMainRequest);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly WorldCatalogScope _world = new(RuntimeSide.Authority);

        public SubsystemTerrain Terrain { get; } = new() { Terrain = new Terrain() };

        public SubsystemTime Time { get; } = new();

        public SubsystemCellChangeQueue Queue { get; } = new();

        public Fixture()
        {
            _world.Project.AddSubsystem(Terrain);
            _world.Project.AddSubsystem(Time);
            _world.Project.AddSubsystem(Queue);
            var chunk = Terrain.Terrain.AllocateChunk(0, 0);
            chunk.HasContents = true;
            for (var x = 0; x < 16; x++)
            {
                for (var z = 0; z < 16; z++) { chunk.SetCellValueFast(x, 1, z, 1); }
            }

            Queue.Load(new ValuesDictionary());
        }

        public void StartPeriod()
        {
            for (var frame = 0; frame < 200; frame++) { Time.NextFrame(.1f, static () => default); }
        }

        public void Dispose() => _world.Dispose();
    }
}

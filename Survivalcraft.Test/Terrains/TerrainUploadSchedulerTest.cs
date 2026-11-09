using Game.Terrains;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainUploadSchedulerTest
{
    [Fact]
    public void CamerasShareOneFrameBudgetAndTheNextFrameGetsANewBudget()
    {
        using var terrain = new Terrain();
        var clock = new Clock();
        var scheduler = new TerrainUploadScheduler(clock);
        var chunks = Enumerable.Range(0, 8).Select(x => terrain.AllocateChunk(x, 0)).ToArray();
        var completed = new HashSet<TerrainChunk>();

        TerrainUploadScheduler.Candidate[] Pending() => chunks.Where(chunk => !completed.Contains(chunk))
            .Select(chunk => new TerrainUploadScheduler.Candidate(chunk, true, chunk.Coords.X)).ToArray();

        bool Upload(TerrainChunk chunk)
        {
            completed.Add(chunk);
            clock.Advance(1);
            return true;
        }

        Assert.Equal(4, scheduler.Upload(10, Pending(), Upload));
        Assert.Equal(0, scheduler.Upload(10, Pending(), Upload));
        Assert.Equal(4, completed.Count);
        Assert.Equal(4, scheduler.Upload(11, Pending(), Upload));
        Assert.Equal(8, completed.Count);
    }

    [Fact]
    public void OneLongUploadCanOverrunButCannotStartAnotherInThatFrame()
    {
        using var terrain = new Terrain();
        var clock = new Clock();
        var scheduler = new TerrainUploadScheduler(clock);
        var candidates = Enumerable.Range(0, 3)
            .Select(x => new TerrainUploadScheduler.Candidate(terrain.AllocateChunk(x, 0), true, x)).ToArray();

        bool Upload(TerrainChunk _)
        {
            clock.Advance(7);
            return true;
        }

        Assert.Equal(1, scheduler.Upload(1, candidates, Upload));
        Assert.Equal(7, scheduler.SpentMilliseconds);
        Assert.Equal(0, scheduler.Upload(1, candidates, Upload));
    }

    [Fact]
    public void FirstDisplayStartsNearAndMaintenanceReceivesTheNextOpportunity()
    {
        using var terrain = new Terrain();
        var clock = new Clock();
        var scheduler = new TerrainUploadScheduler(clock);
        var far = terrain.AllocateChunk(10, 0);
        var near = terrain.AllocateChunk(1, 0);
        var maintenance = terrain.AllocateChunk(0, 0);
        var uploaded = new List<TerrainChunk>();
        var candidates = new TerrainUploadScheduler.Candidate[]
        {
            new(far, true, 100), new(near, true, 1), new(maintenance, false, 0)
        };
        scheduler.Upload(
            1,
            candidates,
            chunk =>
            {
                uploaded.Add(chunk);
                clock.Advance(3);
                return true;
            }
        );

        Assert.Equal(new[] { near, maintenance }, uploaded);
    }

    [Fact]
    public void OldFirstDisplayWorkCannotStarveBehindContinuousNearAndMaintenanceArrivals()
    {
        using var terrain = new Terrain();
        var clock = new Clock();
        var scheduler = new TerrainUploadScheduler(clock);
        var old = terrain.AllocateChunk(100, 0);
        var uploaded = new List<TerrainChunk>();
        for (var frame = 0; frame < 7; frame++)
        {
            var near = terrain.AllocateChunk(frame, 0);
            var maintenance = terrain.AllocateChunk(frame, 1);
            var candidates = new TerrainUploadScheduler.Candidate[]
            {
                new(old, true, 10000), new(near, true, 1), new(maintenance, false, 0)
            };
            Assert.Equal(
                1,
                scheduler.Upload(
                    frame,
                    candidates,
                    chunk =>
                    {
                        uploaded.Add(chunk);
                        clock.Advance(4);
                        return true;
                    }
                )
            );
        }

        Assert.Contains(old, uploaded);
        Assert.Contains(uploaded, chunk => chunk.Coords.Y == 1);
    }

    [Fact]
    public void AnotherCamerasCandidateSetCannotEraseTheAgeOfAnOldVisibleUpload()
    {
        using var terrain = new Terrain();
        var clock = new Clock();
        var scheduler = new TerrainUploadScheduler(clock);
        var old = terrain.AllocateChunk(100, 0);
        var otherView = terrain.AllocateChunk(0, 100);
        var uploaded = new List<TerrainChunk>();
        for (var frame = 0; frame < 4; frame++)
        {
            var near = terrain.AllocateChunk(frame, 0);
            var firstView = new TerrainUploadScheduler.Candidate[]
            {
                new(near, true, 1), new(old, true, 10000)
            };
            Assert.Equal(
                1,
                scheduler.Upload(
                    frame,
                    firstView,
                    chunk =>
                    {
                        uploaded.Add(chunk);
                        clock.Advance(4);
                        return true;
                    }
                )
            );
            Assert.Equal(
                0,
                scheduler.Upload(
                    frame,
                    [new TerrainUploadScheduler.Candidate(otherView, false, 0)],
                    _ => true
                )
            );
        }

        Assert.Contains(old, uploaded);
    }

    [Fact]
    public void BusyUploadsAreTriedOnlyOncePerDrainAndRemainEligibleForALaterCamera()
    {
        using var terrain = new Terrain();
        var scheduler = new TerrainUploadScheduler(new Clock());
        var chunk = terrain.AllocateChunk(0, 0);
        var candidates = new[] { new TerrainUploadScheduler.Candidate(chunk, true, 0) };
        var attempts = 0;
        Assert.Equal(
            0,
            scheduler.Upload(
                1,
                candidates,
                _ =>
                {
                    attempts++;
                    return false;
                }
            )
        );
        Assert.Equal(1, attempts);
        Assert.Equal(1, scheduler.Upload(1, candidates, _ => true));
    }

    private sealed class Clock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => _ticks;

        public void Advance(int milliseconds) => _ticks += milliseconds;
    }
}

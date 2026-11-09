using System.Collections.Concurrent;

using Engine.Core.Logging;
using Engine.Core.Mathematics;

using Game.Modding;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.TerrainSerializers;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class AuthorityTerrainContentTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicationPollingNeverWaitsForABusyTerrainWorker(bool queued)
    {
        using var fixture = new Fixture();
        if (queued) { fixture.Generate(fixture.Chunk); }

        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = HoldAccess(fixture, entered, release);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var pump = Task.Run(() => fixture.Contents.PublishGeneratedContents());
        try
        {
            Assert.Equal(0, await pump.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(queued, fixture.Subsystem.WorkExecutor.HasMainRequest);
            Assert.Empty(fixture.HookThreads);
        }
        finally
        {
            release.Set();
            await Task.WhenAll(holder, pump).WaitAsync(TimeSpan.FromSeconds(5));
        }

        if (queued)
        {
            fixture.Subsystem.WorkExecutor.EnterMain();
            fixture.Subsystem.WorkExecutor.LeaveMain();
            Assert.True(fixture.Subsystem.WorkExecutor.HasMainRequest);
        }

        Assert.Equal(queued ? 1 : 0, fixture.Contents.PublishGeneratedContents());
        Assert.False(fixture.Subsystem.WorkExecutor.HasMainRequest);
    }

    [Fact]
    public async Task ACompletedBusinessHookCanAwaitCommitWithoutBlockingOrReplayingTheHook()
    {
        using var fixture = new Fixture();
        fixture.Generate(fixture.Chunk);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Task? holder = null;
        fixture.OnGenerated = _ =>
        {
            holder = HoldAccess(fixture, entered, release);
            if (!entered.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Test gate holder did not start.");
            }
        };

        var pump = Task.Run(() => fixture.Contents.PublishGeneratedContents());
        try
        {
            Assert.Equal(0, await pump.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(entered.IsSet);
            Assert.Single(fixture.HookThreads);
            Assert.False(fixture.Chunk.HasContents);
            Assert.True(fixture.Subsystem.WorkExecutor.HasMainRequest);
            Assert.Equal(TerrainGenerationStage.Publishing, fixture.Generation.GetStage(fixture.Chunk));
        }
        finally
        {
            release.Set();
            if (holder != null) { await holder.WaitAsync(TimeSpan.FromSeconds(5)); }

            await pump.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(1, fixture.Contents.PublishGeneratedContents());
        Assert.Single(fixture.HookThreads);
        Assert.True(fixture.Chunk.HasContents);
        Assert.False(fixture.Subsystem.WorkExecutor.HasMainRequest);
    }

    private static Task HoldAccess(Fixture fixture, ManualResetEventSlim entered, ManualResetEventSlim release)
    {
        return Task.Run(() =>
            {
                fixture.Subsystem.Access.Enter();
                try
                {
                    entered.Set();
                    release.Wait();
                }
                finally { fixture.Subsystem.Access.Exit(); }
            }
        );
    }

    [Fact]
    public void PumpingUnderAnOuterAccessLeaseCannotSilentlyRunBusinessHooksInsideIt()
    {
        using var fixture = new Fixture();
        fixture.Generate(fixture.Chunk);
        fixture.Subsystem.WorkExecutor.EnterMain();
        try
        {
            Assert.Throws<InvalidOperationException>(() => fixture.Contents.PublishGeneratedContents());
            Assert.Equal(TerrainGenerationStage.Publication, fixture.Generation.GetStage(fixture.Chunk));
        }
        finally { fixture.Subsystem.WorkExecutor.LeaveMain(); }

        Assert.Empty(fixture.HookThreads);
        Assert.Equal(1, fixture.Contents.PublishGeneratedContents());
        Assert.False(fixture.HookHeldGate);
    }

    [Fact]
    public async Task WorkerGeneratesButOnlyTheWorldPumpThreadRunsBusinessPublicationOutsideTheGate()
    {
        using var fixture = new Fixture();
        await fixture.ProduceOnWorker();
        Assert.Equal(4, fixture.Generator.PassThreads.Count);
        Assert.Empty(fixture.HookThreads);
        Assert.False(fixture.Chunk.HasContents);
        var pumpThread = Environment.CurrentManagedThreadId;

        Assert.Equal(1, fixture.Contents.PublishGeneratedContents());

        Assert.Equal(pumpThread, Assert.Single(fixture.HookThreads));
        Assert.All(fixture.Generator.PassThreads, thread => Assert.NotEqual(pumpThread, thread));
        Assert.False(fixture.HookHeldGate);
        Assert.False(fixture.HookSawPublishedContent);
        Assert.True(fixture.Chunk.HasContents);
        Assert.Equal(0, fixture.Contents.PublishGeneratedContents());
        Assert.Single(fixture.HookThreads);
    }

    [Fact]
    public void GeneratedHookChangesAreIncludedBeforeContentBecomesAvailable()
    {
        using var fixture = new Fixture();
        fixture.OnGenerated = context =>
        {
            context.Terrain.WorkExecutor.EnterMain();
            try
            {
                context.Chunk.SetCellValueFast(0, 0, 0, 42);
                context.Chunk.ModificationCounter++;
            }
            finally { context.Terrain.WorkExecutor.LeaveMain(); }
        };
        fixture.Generate(fixture.Chunk);
        Assert.Equal(1, fixture.Contents.PublishGeneratedContents());
        Assert.False(fixture.HookHeldGate);
        Assert.True(fixture.Chunk.HasContents);
        Assert.Equal(42, fixture.Chunk.Cells[0]);
        Assert.Equal(1, fixture.Chunk.ModificationCounter);
    }

    [Fact]
    public void ClosingTheProjectInsideTheHookNeverReentersItsDisposedExecutorOrPublishes()
    {
        using var fixture = new Fixture();
        fixture.OnGenerated = context => context.Project.Dispose();
        fixture.Generate(fixture.Chunk);

        Assert.Equal(0, fixture.Contents.PublishGeneratedContents());
        Assert.False(fixture.Chunk.HasContents);
        Assert.True(fixture.Chunk.IsDisposed);
        Assert.Equal(0, fixture.Contents.PublishGeneratedContents());
        Assert.Single(fixture.HookThreads);
    }

    [Fact]
    public void DisposingThePublicationOwnerInsideAHookCancelsQueuedResults()
    {
        using var fixture = new Fixture();
        fixture.OnGenerated = _ => fixture.Contents.Dispose();
        fixture.Generate(fixture.Chunk);
        Assert.Equal(0, fixture.Contents.PublishGeneratedContents());
        Assert.False(fixture.Chunk.HasContents);
        Assert.False(fixture.Generation.TryTakePublication(out _));
        Assert.Equal(0, fixture.Contents.PublishGeneratedContents());
        Assert.Single(fixture.HookThreads);
    }

    [Fact]
    public void RenewingDuringAHookCannotPublishOrResetTheNewGeneration()
    {
        using var fixture = new Fixture();
        fixture.OnGenerated = context =>
        {
            context.Terrain.WorkExecutor.EnterMain();
            try
            {
                context.Terrain.Terrain.RenewChunkAllocation(context.Chunk);
                fixture.Generation.TryAdvance(context.Chunk);
            }
            finally { context.Terrain.WorkExecutor.LeaveMain(); }
        };
        fixture.Generate(fixture.Chunk);
        Assert.Equal(0, fixture.Contents.PublishGeneratedContents());
        Assert.False(fixture.Chunk.HasContents);
        Assert.Equal(TerrainGenerationStage.FirstPass, fixture.Generation.GetStage(fixture.Chunk));
        Assert.Single(fixture.HookThreads);
    }

    [Fact]
    public void ReentrantPumpsCannotConsumeTheOuterPumpsRemainingBudget()
    {
        using var fixture = new Fixture();
        var nested = new List<int>();
        fixture.OnGenerated = _ => nested.Add(fixture.Contents.PublishGeneratedContents());
        fixture.Generate(fixture.Chunk);
        var other = fixture.Subsystem.Terrain.AllocateChunk(1, 0);
        fixture.Generate(other);

        Assert.Equal(1, fixture.Contents.PublishGeneratedContents(1));
        Assert.Single(fixture.HookThreads);
        Assert.False(other.HasContents);
        Assert.Equal(1, fixture.Contents.PublishGeneratedContents(1));
        Assert.Equal(new[] { 0, 0 }, nested);
        Assert.True(other.HasContents);
    }

    [Fact]
    public void RestoredContentNeverEmitsGeneratedBusinessHooks()
    {
        using var fixture = new Fixture(restore: true);
        fixture.Generate(fixture.Chunk);
        Assert.True(fixture.Chunk.HasContents);
        Assert.Equal(TerrainContentOrigin.Storage, fixture.Chunk.ContentOrigin);
        Assert.Empty(fixture.Generator.PassThreads);
        Assert.Equal(0, fixture.Contents.PublishGeneratedContents());
        Assert.Empty(fixture.HookThreads);
    }

    [Fact]
    public void UncaughtDispatchFailureRejectsTheTicketAndCannotReplayItsPublication()
    {
        using var fixture = new Fixture();
        fixture.Generate(fixture.Chunk);
        WorldRuleHooks.For(fixture.World.Project).Dispose();
        Assert.Throws<InvalidOperationException>(() => fixture.Contents.PublishGeneratedContents());
        Assert.Equal(TerrainGenerationStage.Rejected, fixture.Generation.GetStage(fixture.Chunk));
        Assert.False(fixture.Chunk.HasContents);
        Assert.Equal(0, fixture.Contents.PublishGeneratedContents());
    }

    [Fact]
    public void ModHooksExistingExceptionIsolationStillLogsAndContinuesPublication()
    {
        const string marker = "test-generation-publication-hook-failure";
        using var fixture = new Fixture();
        fixture.OnGenerated = _ => throw new InvalidOperationException(marker);
        fixture.Generate(fixture.Chunk);
        var sink = new MemorySink();
        var previousMinimum = Log.MinimumLogType;
        try
        {
            Log.MinimumLogType = LogType.Debug;
            Log.AddLogSink(sink);
            Assert.Equal(1, fixture.Contents.PublishGeneratedContents());
            Assert.True(fixture.Chunk.HasContents);
            Assert.Single(fixture.HookThreads);
            Assert.Contains(sink.Messages, message => message.Contains(marker, StringComparison.Ordinal));
            Assert.Equal(0, fixture.Contents.PublishGeneratedContents());
        }
        finally
        {
            Log.RemoveLogSink(sink);
            Log.MinimumLogType = previousMinimum;
        }
    }

    private sealed class Fixture : IDisposable
    {
        private TerrainWorkExecutor? _producer;

        public WorldCatalogScope World { get; } = new();

        public SubsystemTerrain Subsystem { get; }

        public TerrainChunk Chunk { get; }

        public RecordingGenerator Generator { get; } = new();

        public AuthorityTerrainContent Contents { get; }

        public AuthoritativeChunkGenerationPipeline Generation => Contents.Generation;

        public List<int> HookThreads { get; } = [];

        public bool HookHeldGate { get; private set; }

        public bool HookSawPublishedContent { get; private set; }

        public Action<TerrainChunkGeneratedContext>? OnGenerated { get; set; }

        public Fixture(bool restore = false)
        {
            Subsystem = new SubsystemTerrain { Terrain = new Terrain() };
            World.Project.AddSubsystem(Subsystem);
            Chunk = Subsystem.Terrain.AllocateChunk(0, 0);
            Contents = new AuthorityTerrainContent(
                Subsystem,
                new AuthoritativeChunkGenerationPipeline(Generator, _ => restore, _ => false)
            );
            WorldRuleHooks.For(World.Project).Dispose();
            var gameplay = new GameplayHooks();
            gameplay.ForOwner(new ModId("test.generation-publication")).OnTerrainChunkGenerated(context =>
                {
                    HookThreads.Add(Environment.CurrentManagedThreadId);
                    HookHeldGate |= context.Terrain.Access.IsHeldByCurrentThread;
                    HookSawPublishedContent |= context.Chunk.HasContents;
                    OnGenerated?.Invoke(context);
                }
            );
            gameplay.Freeze();
            var blockHooks = new BlockBehaviorHooks();
            blockHooks.Freeze();
            World.Project.BindRuleHooks(gameplay, blockHooks);
        }

        public Task ProduceOnWorker()
        {
            var produced = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _producer = new TerrainWorkExecutor(
                () =>
                {
                    try
                    {
                        Generation.TryAdvance(Chunk);
                        if (Generation.GetStage(Chunk) is TerrainGenerationStage.Publication
                            or TerrainGenerationStage.Complete)
                        {
                            produced.TrySetResult(true);
                            return true;
                        }

                        return false;
                    }
                    catch (Exception exception)
                    {
                        produced.TrySetException(exception);
                        return true;
                    }
                },
                Subsystem.Access
            );
            _producer.Start();
            _producer.Wake();
            return produced.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void Generate(TerrainChunk chunk)
        {
            Subsystem.WorkExecutor.EnterMain();
            try
            {
                for (var i = 0; i < 5; i++)
                {
                    if (Generation.GetStage(chunk) == TerrainGenerationStage.Complete) { break; }

                    Assert.True(Generation.TryAdvance(chunk));
                }
            }
            finally { Subsystem.WorkExecutor.LeaveMain(); }
        }

        public void Dispose()
        {
            try
            {
                _producer?.Dispose();
                Contents.Dispose();
            }
            finally { World.Dispose(); }
        }
    }

    private sealed class RecordingGenerator : ITerrainContentsGenerator
    {
        public List<int> PassThreads { get; } = [];

        public Vector3 FindCoarseSpawnPosition() => Vector3.Zero;

        public void GenerateChunkContentsPass1(TerrainChunk chunk) => Record(chunk, 1);
        public void GenerateChunkContentsPass2(TerrainChunk chunk) => Record(chunk, 2);
        public void GenerateChunkContentsPass3(TerrainChunk chunk) => Record(chunk, 3);
        public void GenerateChunkContentsPass4(TerrainChunk chunk) => Record(chunk, 4);

        private void Record(TerrainChunk chunk, int value)
        {
            PassThreads.Add(Environment.CurrentManagedThreadId);
            chunk.SetCellValueFast(0, 0, 0, value);
        }
    }

    private sealed class MemorySink : ILogSink
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public void Log(LogType type, string message) => Messages.Enqueue(message);
    }
}

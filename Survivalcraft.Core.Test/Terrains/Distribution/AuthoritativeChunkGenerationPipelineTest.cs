using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Distribution;
using Game.TerrainSerializers;

namespace Survivalcraft.Core.Test.Terrains.Distribution;

public sealed class AuthoritativeChunkGenerationPipelineTest
{
    [Fact]
    public void StalePublicationCleanupIsBoundedAndLeavesLaterValidWorkAvailable()
    {
        using var terrain = new Terrain();
        using var pipeline = new AuthoritativeChunkGenerationPipeline(new FakeGenerator(), _ => false, _ => false);
        for (var i = 0; i < 3; i++)
        {
            var retired = terrain.AllocateChunk(i, 0);
            Generate(pipeline, retired);
            terrain.RenewChunkAllocation(retired);
        }

        var current = terrain.AllocateChunk(4, 0);
        Generate(pipeline, current);
        for (var i = 0; i < 3; i++)
        {
            Assert.False(pipeline.TryTakePublication(out _, 1));
            Assert.True(pipeline.HasPendingPublications);
        }

        Assert.True(pipeline.TryTakePublication(out var publication, 1));
        Assert.Same(current, publication.Chunk);
        Assert.False(pipeline.HasPendingPublications);
        Assert.True(pipeline.CompletePublication(publication, true));
    }

    [Fact]
    public void GeneratedContentWaitsForOneExplicitPublicationDecision()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(3, 4);
        var generator = new FakeGenerator();
        using var pipeline = new AuthoritativeChunkGenerationPipeline(generator, _ => false, _ => false);
        Generate(pipeline, chunk);
        Assert.False(chunk.HasContents);
        Assert.Equal(TerrainContentOrigin.Generated, chunk.ContentOrigin);
        Assert.Equal(TerrainGenerationStage.Publication, pipeline.GetStage(chunk));
        Assert.False(pipeline.TryAdvance(chunk));
        Assert.True(pipeline.TryTakePublication(out var publication));
        Assert.Same(chunk, publication.Chunk);
        Assert.Equal(chunk.AllocationGeneration, publication.Generation);
        Assert.Equal(TerrainGenerationStage.Publishing, pipeline.GetStage(chunk));
        Assert.False(pipeline.TryTakePublication(out _));
        Assert.False(pipeline.TryAdvance(chunk));

        Assert.True(pipeline.CompletePublication(publication, true));
        Assert.False(pipeline.CompletePublication(publication, true));
        Assert.True(chunk.HasContents);
        Assert.Equal(TerrainGenerationStage.Complete, pipeline.GetStage(chunk));
        Assert.Equal(4, generator.PassCount);
        Assert.False(chunk.HasCurrentLight);
    }

    [Fact]
    public void RejectedPublicationCannotRegenerateOrBeAcceptedOnRetry()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var generator = new FakeGenerator();
        using var pipeline = new AuthoritativeChunkGenerationPipeline(generator, _ => false, _ => false);
        Generate(pipeline, chunk);
        Assert.True(pipeline.TryTakePublication(out var publication));
        Assert.False(pipeline.CompletePublication(publication, false));
        Assert.False(pipeline.CompletePublication(publication, true));
        Assert.Equal(TerrainGenerationStage.Rejected, pipeline.GetStage(chunk));
        Assert.False(chunk.HasContents);
        Assert.False(pipeline.TryAdvance(chunk));
        Assert.False(pipeline.TryTakePublication(out _));
        Assert.Equal(4, generator.PassCount);
    }

    [Fact]
    public void RestoredContentSkipsProceduralGenerationAndPublicationTickets()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var generator = new FakeGenerator();
        using var pipeline = new AuthoritativeChunkGenerationPipeline(
            generator,
            _ => true,
            _ => throw new Xunit.Sdk.XunitException("Load must not run after pending-save restore.")
        );
        Assert.True(pipeline.TryAdvance(chunk));
        Assert.Equal(TerrainGenerationStage.Complete, pipeline.GetStage(chunk));
        Assert.True(chunk.HasContents);
        Assert.Equal(TerrainContentOrigin.Storage, chunk.ContentOrigin);
        Assert.Equal(0, generator.PassCount);
        Assert.False(pipeline.TryTakePublication(out _));
    }

    [Fact]
    public void DiskRestoreIsTriedOnlyAfterPendingSaveMisses()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var calls = new List<string>();
        using var pipeline = new AuthoritativeChunkGenerationPipeline(
            new FakeGenerator(),
            _ =>
            {
                calls.Add("pending");
                return false;
            },
            _ =>
            {
                calls.Add("disk");
                return true;
            }
        );
        Assert.True(pipeline.TryAdvance(chunk));
        Assert.Equal(new[] { "pending", "disk" }, calls);
        Assert.True(chunk.HasContents);
        Assert.Equal(TerrainGenerationStage.Complete, pipeline.GetStage(chunk));
        Assert.False(pipeline.TryTakePublication(out _));
    }

    [Fact]
    public void SeedBasisSkipsTheFirstThreePassesButStillRequiresPublication()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var generator = new FakeGenerator { HasSeedBasis = true };
        using var pipeline = new AuthoritativeChunkGenerationPipeline(generator, _ => false, _ => false);
        for (var step = 0; step < 3; step++) { Assert.True(pipeline.TryAdvance(chunk)); }

        Assert.Equal(1, generator.PassCount);
        Assert.False(chunk.HasContents);
        Assert.True(pipeline.TryTakePublication(out var publication));
        Assert.True(pipeline.CompletePublication(publication, true));
        Assert.True(chunk.HasContents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenerationAndPublicationDoNotChangeLightingReadiness(bool complete)
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var request = chunk.DerivedRevisions.Capture();
        if (complete)
        {
            Assert.True(chunk.DerivedRevisions.PublishHeights(request));
            Assert.True(chunk.DerivedRevisions.PublishLightSeed(request));
            Assert.True(chunk.DerivedRevisions.PublishLight(request));
        }

        var generator = new FakeGenerator();
        using var pipeline = new AuthoritativeChunkGenerationPipeline(generator, _ => false, _ => false);
        Generate(pipeline, chunk);
        Assert.True(pipeline.TryTakePublication(out var publication));
        Assert.True(pipeline.CompletePublication(publication, true));
        Assert.Equal(request, chunk.DerivedRevisions.Capture());
        Assert.Equal(complete, chunk.HasCurrentLight);
        Assert.Equal(4, generator.PassCount);
    }

    [Fact]
    public void FailedPassDoesNotAdvanceOrPublishContent()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var generator = new FakeGenerator { FailSecondPass = true };
        using var pipeline = new AuthoritativeChunkGenerationPipeline(generator, _ => false, _ => false);
        Assert.True(pipeline.TryAdvance(chunk));
        Assert.True(pipeline.TryAdvance(chunk));
        Assert.Throws<InvalidOperationException>(() => pipeline.TryAdvance(chunk));
        Assert.Equal(TerrainGenerationStage.SecondPass, pipeline.GetStage(chunk));
        Assert.False(chunk.HasCurrentLight);
        Assert.False(chunk.HasContents);
        Assert.False(pipeline.TryTakePublication(out _));
    }

    [Fact]
    public void AllocationRenewalCannotReusePartialGenerationProgress()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var generator = new FakeGenerator();
        var restores = 0;
        using var pipeline = new AuthoritativeChunkGenerationPipeline(
            generator,
            _ =>
            {
                restores++;
                return false;
            },
            _ => false
        );
        Assert.True(pipeline.TryAdvance(chunk));
        Assert.True(pipeline.TryAdvance(chunk));
        terrain.RenewChunkAllocation(chunk);
        Assert.Equal(TerrainGenerationStage.Restore, pipeline.GetStage(chunk));
        Assert.True(pipeline.TryAdvance(chunk));
        Assert.Equal(2, restores);
        Assert.Equal(1, generator.PassCount);
    }

    [Fact]
    public void CompletingAnOldTicketCannotResetTheNewAllocationsProgress()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        using var pipeline = new AuthoritativeChunkGenerationPipeline(new FakeGenerator(), _ => false, _ => false);
        Generate(pipeline, chunk);
        Assert.True(pipeline.TryTakePublication(out var old));
        terrain.RenewChunkAllocation(chunk);
        Assert.True(pipeline.TryAdvance(chunk));
        Assert.True(pipeline.TryAdvance(chunk));
        Assert.Equal(TerrainGenerationStage.SecondPass, pipeline.GetStage(chunk));

        Assert.False(pipeline.CompletePublication(old, true));
        Assert.Equal(TerrainGenerationStage.SecondPass, pipeline.GetStage(chunk));
        for (var i = 0; i < 3; i++) { Assert.True(pipeline.TryAdvance(chunk)); }

        Assert.True(pipeline.TryTakePublication(out var current));
        Assert.NotEqual(old.Generation, current.Generation);
        Assert.True(pipeline.CompletePublication(current, true));
    }

    [Fact]
    public void ForeignPipelineCannotConsumeAnOwnersTicket()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        using var owner = new AuthoritativeChunkGenerationPipeline(new FakeGenerator(), _ => false, _ => false);
        using var foreign = new AuthoritativeChunkGenerationPipeline(new FakeGenerator(), _ => false, _ => false);
        Generate(owner, chunk);
        Assert.True(owner.TryTakePublication(out var publication));
        Assert.False(foreign.CompletePublication(publication, true));
        Assert.True(owner.CompletePublication(publication, true));
    }

    [Fact]
    public void FreedChunksCannotPublishOrResetTheNewChunkAtTheirCoordinates()
    {
        using var terrain = new Terrain();
        var old = terrain.AllocateChunk(0, 0);
        using var pipeline = new AuthoritativeChunkGenerationPipeline(new FakeGenerator(), _ => false, _ => false);
        Generate(pipeline, old);
        Assert.True(pipeline.TryTakePublication(out var publication));
        terrain.FreeChunk(old);
        var current = terrain.AllocateChunk(0, 0);
        Assert.True(pipeline.TryAdvance(current));
        Assert.False(pipeline.CompletePublication(publication, true));
        Assert.False(current.HasContents);
        Assert.Equal(TerrainGenerationStage.FirstPass, pipeline.GetStage(current));
    }

    [Fact]
    public void QueuedOldAllocationsAreSkippedWithoutTouchingTheNewProgress()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        using var pipeline = new AuthoritativeChunkGenerationPipeline(new FakeGenerator(), _ => false, _ => false);
        Generate(pipeline, chunk);
        terrain.RenewChunkAllocation(chunk);
        Assert.True(pipeline.TryAdvance(chunk));
        Assert.False(pipeline.TryTakePublication(out _));
        Assert.Equal(TerrainGenerationStage.FirstPass, pipeline.GetStage(chunk));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposalDropsQueuedPublicationsAndRejectsTakenTickets(bool take)
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var pipeline = new AuthoritativeChunkGenerationPipeline(new FakeGenerator(), _ => false, _ => false);
        Generate(pipeline, chunk);
        AuthoritativeChunkGenerationPipeline.Publication? publication = null;
        if (take) { Assert.True(pipeline.TryTakePublication(out publication)); }

        pipeline.Dispose();
        Assert.False(pipeline.TryTakePublication(out _));
        if (publication != null) { Assert.False(pipeline.CompletePublication(publication, true)); }

        Assert.False(chunk.HasContents);
        Assert.Throws<ObjectDisposedException>(() => pipeline.TryAdvance(chunk));
        pipeline.Dispose();
    }

    private static void Generate(AuthoritativeChunkGenerationPipeline pipeline, TerrainChunk chunk)
    {
        for (var i = 0; i < 5; i++) { Assert.True(pipeline.TryAdvance(chunk)); }
    }

    private sealed class FakeGenerator : ITerrainContentsGenerator
    {
        public int PassCount { get; private set; }

        public bool HasSeedBasis { get; init; }

        public bool FailSecondPass { get; init; }

        public bool TryTakeSeedGeneratedChunkBasis(TerrainChunk chunk) => HasSeedBasis;

        public Vector3 FindCoarseSpawnPosition() => Vector3.Zero;

        public void GenerateChunkContentsPass1(TerrainChunk chunk) => PassCount++;

        public void GenerateChunkContentsPass2(TerrainChunk chunk)
        {
            if (FailSecondPass) { throw new InvalidOperationException("Generation failed."); }

            PassCount++;
        }

        public void GenerateChunkContentsPass3(TerrainChunk chunk) => PassCount++;
        public void GenerateChunkContentsPass4(TerrainChunk chunk) => PassCount++;
    }
}

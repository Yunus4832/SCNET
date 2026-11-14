using System.Runtime.CompilerServices;

using Engine.Graphics;

using Game.Blocks.Presentation.Geometry;
using Game.ClientContent;
using Game.Modding.Blocks;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Lighting;
using Game.Terrains.Rendering;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class TerrainMeshColumnBudgetTest
{
    [Fact]
    public void NewLightPublicationDoesNotMixLightingAcrossResumedInteractiveColumns()
    {
        using var fixture = new Fixture();
        var input = fixture.Terrain.Lighting.CaptureInput(fixture.Chunk);
        Assert.True(
            fixture.Terrain.Lighting.TryPublish(
                fixture.Chunk,
                input,
                new TerrainLightSnapshot(new TerrainLightChannels(15, 0))
            )
        );
        using var expected = new TerrainChunkPresentation();
        using var actual = new TerrainChunkPresentation();
        expected.PrepareMeshUpdate(fixture.Chunk, true);
        actual.PrepareMeshUpdate(fixture.Chunk, true);
        var full = new TerrainMeshBuilder(fixture.Terrain, fixture.Generator, fixture.Texture);
        Assert.True(full.GenerateSlice(fixture.Chunk, expected, 0, TimeSpan.MaxValue));
        var bounded = new TerrainMeshBuilder(fixture.Terrain, fixture.Generator, fixture.Texture, new Clock());
        Assert.False(bounded.GenerateSlice(fixture.Chunk, actual, 0, TimeSpan.FromMilliseconds(1)));
        Assert.True(fixture.Terrain.Lighting.TryPublish(fixture.Chunk, input, new TerrainLightSnapshot(default)));
        var finished = false;
        for (var i = 0; i < 300 && !finished; i++)
        {
            actual.PrepareMeshUpdate(fixture.Chunk, true);
            finished = bounded.GenerateSlice(fixture.Chunk, actual, 0, TimeSpan.FromMilliseconds(1));
        }

        Assert.True(finished);
        var first = expected.Draws[fixture.Texture][0].Subsets;
        var second = actual.Draws[fixture.Texture][0].Subsets;
        for (var i = 0; i < first.Length; i++)
        {
            Assert.Equal(first[i].Vertices.ToArray(), second[i].Vertices.ToArray());
            Assert.Equal(first[i].Indices.ToArray(), second[i].Indices.ToArray());
        }
    }

    [Fact]
    public void CompactionYieldsWithinTheSliceBudgetAndPreservesCachedGeometry()
    {
        using var fixture = new Fixture();
        using var mesh = new TerrainChunkPresentation();
        var complete = new TerrainMeshBuilder(fixture.Terrain, fixture.Generator, fixture.Texture);
        Assert.True(complete.GenerateSlice(fixture.Chunk, mesh, 0, TimeSpan.MaxValue));
        var subset = mesh.Draws[fixture.Texture][0].Subsets.First(part => part.Indices.Count > 0);
        var expectedVertices = subset.Vertices.ToArray();
        var expectedIndices = subset.Indices.ToArray();
        subset.Vertices.Capacity = 8192;
        subset.Indices.Capacity = 8192;
        mesh.InvalidateSliceContentsHashes();
        var bounded = new TerrainMeshBuilder(fixture.Terrain, fixture.Generator, fixture.Texture, new Clock());
        var yieldedCompaction = false;
        var finished = false;
        for (var i = 0; i < 300 && !finished; i++)
        {
            finished = bounded.GenerateSlice(fixture.Chunk, mesh, 0, TimeSpan.FromMilliseconds(1));
            yieldedCompaction |= !finished && mesh.ActiveSlice is { Column: 256, CompactionBuffer: > 0 };
        }

        Assert.True(finished);
        Assert.True(yieldedCompaction);
        Assert.Equal(expectedVertices, subset.Vertices.ToArray());
        Assert.Equal(expectedIndices, subset.Indices.ToArray());
        Assert.InRange(subset.Vertices.Capacity, subset.Vertices.Count, 8191);
        Assert.InRange(subset.Indices.Capacity, subset.Indices.Count, 8191);
        Assert.True(complete.GenerateSlice(fixture.Chunk, mesh, 0, TimeSpan.MaxValue));
        Assert.Null(mesh.ActiveSlice);
    }

    [Fact]
    public void InterruptedColumnsProduceTheSameGeometryAsAnUninterruptedSlice()
    {
        using var fixture = new Fixture();
        using var incremental = new TerrainChunkPresentation();
        using var whole = new TerrainChunkPresentation();
        var small = new TerrainMeshBuilder(fixture.Terrain, fixture.Generator, fixture.Texture, new Clock());
        Assert.False(small.GenerateSlice(fixture.Chunk, incremental, 0, TimeSpan.FromMilliseconds(1)));
        Assert.NotNull(incremental.ActiveSlice);
        Assert.InRange(incremental.ActiveSlice!.Column, 1, 255);
        var finished = false;
        for (var i = 0; i < 256 && !finished; i++)
        {
            finished = small.GenerateSlice(fixture.Chunk, incremental, 0, TimeSpan.FromMilliseconds(1));
        }

        Assert.True(finished);
        Assert.Null(incremental.ActiveSlice);
        var large = new TerrainMeshBuilder(fixture.Terrain, fixture.Generator, fixture.Texture);
        Assert.True(large.GenerateSlice(fixture.Chunk, whole, 0, TimeSpan.MaxValue));
        var expected = whole.Draws[fixture.Texture][0].Subsets;
        var actual = incremental.Draws[fixture.Texture][0].Subsets;
        Assert.Equal(expected.Length, actual.Length);
        Assert.True(actual.Sum(subset => subset.Vertices.Count) > 0);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Vertices.ToArray(), actual[i].Vertices.ToArray());
            Assert.Equal(expected[i].Indices.ToArray(), actual[i].Indices.ToArray());
        }
    }

    [Fact]
    public void ChangedBuildInputsDiscardTheUnfinishedColumnCursor()
    {
        using var fixture = new Fixture();
        using var mesh = new TerrainChunkPresentation();
        mesh.PrepareMeshUpdate(fixture.Chunk);
        var builder = new TerrainMeshBuilder(fixture.Terrain, fixture.Generator, fixture.Texture, new Clock());
        Assert.False(builder.GenerateSlice(fixture.Chunk, mesh, 0, TimeSpan.FromMilliseconds(1)));
        Assert.Throws<InvalidOperationException>(mesh.CompleteMeshSlice);
        fixture.Chunk.NetworkContentVersion++;
        mesh.PrepareMeshUpdate(fixture.Chunk);
        Assert.Null(mesh.ActiveSlice);
        Assert.Equal(0, mesh.CompletedMeshSlices);
        Assert.False(mesh.CompleteMeshBuild(fixture.Chunk));
    }

    [Fact]
    public void OpaqueCubeTopologyIsStableAcrossDifferentSpatialLightingInputs()
    {
        using var fixture = new Fixture();
        var input = fixture.Terrain.Lighting.CaptureInput(fixture.Chunk);
        using var bright = new TerrainChunkPresentation();
        using var dark = new TerrainChunkPresentation();
        Assert.True(
            fixture.Terrain.Lighting.TryPublish(
                fixture.Chunk,
                input,
                new TerrainLightSnapshot(new TerrainLightChannels(15, 0))
            )
        );
        var builder = new TerrainMeshBuilder(fixture.Terrain, fixture.Generator, fixture.Texture);
        Assert.True(builder.GenerateSlice(fixture.Chunk, bright, 0, TimeSpan.MaxValue));
        Assert.True(fixture.Terrain.Lighting.TryPublish(fixture.Chunk, input, new TerrainLightSnapshot(default)));
        Assert.True(builder.GenerateSlice(fixture.Chunk, dark, 0, TimeSpan.MaxValue));
        var lightChanged = false;
        var first = bright.Draws[fixture.Texture][0].Subsets;
        var second = dark.Draws[fixture.Texture][0].Subsets;
        for (var i = 0; i < first.Length; i++)
        {
            Assert.Equal(first[i].Indices.ToArray(), second[i].Indices.ToArray());
            Assert.Equal(first[i].Vertices.Count, second[i].Vertices.Count);
            for (var j = 0; j < first[i].Vertices.Count; j++)
            {
                var a = first[i].Vertices.Array[j];
                var b = second[i].Vertices.Array[j];
                Assert.Equal((a.X, a.Y, a.Z, a.Tx, a.Ty, a.Color), (b.X, b.Y, b.Z, b.Tx, b.Ty, b.Color));
                lightChanged |= a.Lighting != b.Lighting;
            }
        }

        Assert.True(lightChanged);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void FrozenInputsAndIndependentGeometryContextsProduceIdenticalParallelResults(int workers)
    {
        using var fixture = new Fixture();
        for (var x = 1; x < 15; x++)
        {
            for (var z = 1; z < 15; z++)
            {
                fixture.Chunk.SetTopHeightFast(x, z, 14);
                for (var y = 1; y < 15; y++)
                {
                    fixture.Chunk.SetCellValueFast(x, y, z, (x + y + z) % 2 == 0 ? 3 : 0);
                }
            }
        }

        using var reference = new TerrainChunkPresentation();
        var serial = new TerrainMeshBuilder(fixture.Terrain, fixture.Generator, fixture.Texture);
        Assert.True(serial.GenerateSlice(fixture.Chunk, reference, 0, TimeSpan.MaxValue));
        var expected = reference.Draws[fixture.Texture][0].Subsets;
        Parallel.For(
            0,
            8,
            new ParallelOptions { MaxDegreeOfParallelism = workers },
            _ =>
            {
                var generator = fixture.CreateGenerator();
                var builder = new TerrainMeshBuilder(fixture.Terrain, generator, fixture.Texture);
                using var actual = new TerrainChunkPresentation();
                Assert.True(builder.GenerateSlice(fixture.Chunk, actual, 0, TimeSpan.MaxValue));
                var subsets = actual.Draws[fixture.Texture][0].Subsets;
                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.Equal(expected[i].Vertices.ToArray(), subsets[i].Vertices.ToArray());
                    Assert.Equal(expected[i].Indices.ToArray(), subsets[i].Indices.ToArray());
                }
            }
        );
    }

    private sealed class Clock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => _timestamp++;
    }

    private sealed class Fixture : IDisposable
    {
        public WorldCatalogScope World { get; } = new(RuntimeSide.Client);

        public Terrain Terrain { get; } = new();

        public BlockPresentationCatalog Presentations { get; }

        public BlockGeometryGenerator Generator { get; }

        public Texture2D Texture { get; } = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));

        public TerrainChunk Chunk { get; }

        public BlockGeometryGenerator CreateGenerator()
        {
            return new BlockGeometryGenerator(
                Terrain,
                World.Project.FindSubsystem<SubsystemTerrain>(true)!,
                null!,
                null!,
                null!,
                null!
            );
        }

        public Fixture()
        {
            // This object is only a dictionary key and never owns a graphics resource.
            GC.SuppressFinalize(Texture);
            World.Catalog.Blocks[0].Transparent = true;
            var subsystem = new SubsystemTerrain { Terrain = Terrain };
            World.Project.AddSubsystem(subsystem);
            Presentations = new BlockPresentationCatalog(
                World.Catalog,
                new BuiltInClientContentModule().BlockPresentations
            );
            var blockPresentation = new SubsystemBlockPresentation();
            blockPresentation.Bind(Presentations);
            World.Project.AddSubsystem(blockPresentation);
            Generator = new BlockGeometryGenerator(Terrain, subsystem, null!, null!, null!, null!);
            Chunk = Terrain.AllocateChunk(0, 0);
            Chunk.SetCellValueFast(8, 2, 8, 3);
            Chunk.SetTopHeightFast(8, 8, 2);
            TerrainMeshTestData.PrepareLighting(Chunk);
        }

        public void Dispose()
        {
            try { World.Dispose(); }
            finally
            {
                Presentations.Dispose();
                Terrain.Dispose();
            }
        }
    }
}

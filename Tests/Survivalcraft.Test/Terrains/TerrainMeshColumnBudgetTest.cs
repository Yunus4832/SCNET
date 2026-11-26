using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;
using Engine.Graphics;

using Game.Blocks;
using Game.Blocks.Presentation;
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
    public void WaterEdgeKeepsItsSurfaceThroughNeighborAllocationAndRebuildsForRealShore()
    {
        using var fixture = new Fixture();
        using var mesh = new TerrainChunkPresentation();
        using var neighborMesh = new TerrainChunkPresentation();
        var terrain = fixture.Terrain;
        var chunk = fixture.Chunk;
        var water = (WaterBlock)fixture.World.Catalog.GetBlock(ResourceId.Parse("game:water"));
        var waterValue = Terrain.MakeBlockValue(water.BlockIndex, data: FluidBlock.SetIsTop(0, true));
        const int y = 2;
        var surface = y + water.GetLevelHeight(0);
        var builder = new TerrainMeshBuilder(terrain, fixture.Generator, fixture.Texture);
        for (var x = 14; x <= 15; x++)
        {
            for (var z = 7; z <= 9; z++) { PlaceWater(chunk, x, z); }
        }

        var initial = BuildEdge(chunk, mesh);
        Assert.Equal(new[] { surface, surface }, EdgeHeights(initial));
        Assert.False(HasEastFace(initial));

        var east = terrain.AllocateChunk(1, 0);
        Assert.Equal(initial.SelectMany(quad => quad), BuildEdge(chunk, mesh).SelectMany(quad => quad));

        for (var x = 0; x <= 1; x++)
        {
            for (var z = 7; z <= 9; z++) { PlaceWater(east, x, z); }
        }

        TerrainMeshTestData.PrepareLighting(east);
        var ocean = BuildEdge(chunk, mesh);
        Assert.Equal(EdgeHeights(initial), EdgeHeights(ocean));
        Assert.Equal(EdgeHeights(ocean), EdgeHeights(BuildEdge(east, neighborMesh)));
        Assert.False(HasEastFace(ocean));

        // A loaded air column takes part in height and face generation at the real water edge.
        east.SetCellValueFast(0, y, 8, 0);
        east.SetTopHeightFast(0, 8, 0);
        var shore = BuildEdge(chunk, mesh);
        Assert.Equal(new[] { (float)y, (float)y }, EdgeHeights(shore));
        Assert.True(HasEastFace(shore));

        void PlaceWater(TerrainChunk target, int x, int z)
        {
            target.SetCellValueFast(x, y, z, waterValue);
            target.SetTopHeightFast(x, z, y);
        }

        TerrainVertex[][] BuildEdge(TerrainChunk target, TerrainChunkPresentation presentation)
        {
            Assert.True(builder.GenerateSlice(target, presentation, 0, TimeSpan.MaxValue));
            return presentation.Draws[fixture.Texture][0].TransparentSubsetsByFace[4].Vertices.ToArray()
                .Chunk(4)
                .Where(quad => quad.Min(v => v.Z) == 8 && quad.Max(v => v.Z) == 9 && quad.Any(v => v.X == 16))
                .ToArray();
        }

        static float[] EdgeHeights(TerrainVertex[][] quads)
        {
            return quads.Single(quad => quad.Select(v => v.X).Distinct().Count() == 2 && quad.All(v => v.Color.A == 0))
                .Where(v => v.X == 16)
                .OrderBy(v => v.Z)
                .Select(v => v.Y)
                .ToArray();
        }

        static bool HasEastFace(TerrainVertex[][] quads) => quads.Any(quad => quad.All(v => v.X == 16));
    }

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
    public void RelightingResumesWithinTheBudgetAndKeepsUploadedTopology()
    {
        using var fixture = new Fixture();
        for (var x = 2; x < 14; x++)
        {
            for (var z = 2; z < 14; z++)
            {
                fixture.Chunk.SetCellValueFast(x, 2, z, 3);
                fixture.Chunk.SetTopHeightFast(x, z, 2);
            }
        }

        var terrain = fixture.Terrain;
        var chunk = fixture.Chunk;
        var input = terrain.Lighting.CaptureInput(chunk);
        Assert.True(terrain.Lighting.TryPublish(chunk, input, new TerrainLightSnapshot(default)));
        using var mesh = new TerrainChunkPresentation();
        using var expected = new TerrainChunkPresentation();
        var builder = new TerrainMeshBuilder(terrain, fixture.Generator, fixture.Texture);
        mesh.PrepareMeshUpdate(chunk);
        Assert.True(builder.GenerateSlice(chunk, mesh, 0, TimeSpan.MaxValue));
        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.True(mesh.CompleteMeshBuild(chunk));
        mesh.MarkUploaded();
        Assert.False(mesh.HasChangedLighting);

        chunk.DerivedRevisions.InvalidatePropagation();
        input = terrain.Lighting.CaptureInput(chunk);
        var lit = new TerrainLightSnapshot(new TerrainLightChannels(0, 15));
        Assert.True(terrain.Lighting.TryPublish(chunk, input, lit));
        Assert.True(chunk.DerivedRevisions.PublishLight(input.Request));
        mesh.PrepareMeshUpdate(chunk);
        var budget = TimeSpan.FromMilliseconds(2);
        var bounded = new TerrainMeshBuilder(terrain, fixture.Generator, fixture.Texture, new Clock());
        Assert.False(bounded.GenerateSlice(chunk, mesh, 0, budget));
        Assert.NotNull(mesh.ActiveSlice?.RelightSubsets);
        Assert.True(mesh.CanReuseUploadedTopology);
        var finished = false;
        for (var i = 0; i < 512 && !finished; i++) { finished = bounded.GenerateSlice(chunk, mesh, 0, budget); }

        Assert.True(finished);
        Assert.True(mesh.CanReuseUploadedTopology);
        Assert.True(mesh.HasChangedLighting);
        expected.PrepareMeshUpdate(chunk);
        Assert.True(builder.GenerateSlice(chunk, expected, 0, TimeSpan.MaxValue));
        var refreshedSubsets = mesh.Draws[fixture.Texture][0].Subsets;
        var rebuiltSubsets = expected.Draws[fixture.Texture][0].Subsets;
        for (var i = 0; i < refreshedSubsets.Length; i++)
        {
            Assert.Equal(rebuiltSubsets[i].Vertices.ToArray(), refreshedSubsets[i].Vertices.ToArray());
            Assert.Equal(rebuiltSubsets[i].Indices.ToArray(), refreshedSubsets[i].Indices.ToArray());
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(15, 0, 15, 0)]
    [InlineData(0, 12, 0, 11)]
    public void PredictedOpeningLightsOnlyTheNewAirSample(int sky, int block, int expectedSky, int expectedBlock)
    {
        using var fixture = new Fixture();
        var chunk = fixture.Chunk;
        var samples = new byte[chunk.Cells.Length];
        samples[TerrainChunk.CalculateCellIndex(8, 3, 8)] = new TerrainLightChannels(sky, block).Packed;
        var lights = new TerrainLightSnapshot?[9];
        lights[4] = TerrainLightSnapshot.Capture(samples);
        var generator = fixture.Generator;
        generator.SetLightSnapshots(chunk.Coords, lights);
        var index = TerrainChunk.CalculateCellIndex(8, 2, 8);
        chunk.SetGeometryPrediction(index, 3, 0);
        Assert.Equal(new TerrainLightChannels(expectedSky, expectedBlock), generator.ReadLitCell(chunk, index).Light);
        Assert.Equal(default, lights[4]!.Get(8, 2, 8));
        chunk.RemoveGeometryPrediction(index);
        Assert.Equal(default, generator.ReadLitCell(chunk, index).Light);
    }

    [Fact]
    public void LightbulbIntensityChangesWithIdenticalSpatialLightReuseUploadedTopology()
    {
        using var fixture = new Fixture();
        using var mesh = new TerrainChunkPresentation();
        var terrain = fixture.Terrain;
        var chunk = fixture.Chunk;
        var lamp = fixture.World.Catalog.GetBlock(ResourceId.Parse("game:lightbulb"));
        var presentation = (LightbulbBlockPresentation)fixture.Presentations.Get(lamp.BlockIndex);
        var bulb = new BlockMesh();
        bulb.Vertices.Add(new BlockMeshVertex { Color = Color.White });
        bulb.Vertices.Add(new BlockMeshVertex { Position = Vector3.UnitX, Color = Color.White });
        bulb.Vertices.Add(new BlockMeshVertex { Position = Vector3.UnitY, Color = Color.White });
        bulb.Indices.Add(0);
        bulb.Indices.Add(1);
        bulb.Indices.Add(2);
        presentation.BulbBlockMeshes[4] = bulb;
        presentation.SidesBlockMeshes[4] = new BlockMesh();
        var subsystem = fixture.World.Project.FindSubsystem<SubsystemTerrain>(true)!;
        var electricity = new SubsystemElectricity();
        fixture.World.Project.InitializeSubsystem(electricity);
        typeof(SubsystemElectricity).GetProperty(nameof(SubsystemElectricity.SubsystemTerrain))!
            .SetValue(electricity, subsystem);
        var generator = new BlockGeometryGenerator(terrain, subsystem, electricity, null!, null!, null!);
        var mounting = LightbulbBlock.SetMountingFace(0, 4);
        var allocation = terrain.Lighting.CaptureInput(chunk);
        var light = new TerrainLightSnapshot(new TerrainLightChannels(0, 15));
        Assert.True(terrain.Lighting.TryPublish(chunk, allocation, light));
        var builder = new TerrainMeshBuilder(terrain, generator, fixture.Texture);
        chunk.SetTopHeightFast(8, 8, 2);

        var unlit = Build(0);
        Assert.NotEmpty(unlit.Vertices);
        Assert.All(unlit.Vertices, vertex => Assert.Equal(0, vertex.emissiveMaterial.A));
        mesh.MarkUploaded();

        var lit = Build(15);
        Assert.True(mesh.CanReuseUploadedTopology);
        Assert.True(mesh.HasChangedLighting);
        Assert.Equal(unlit.Indices, lit.Indices);
        Assert.Equal(unlit.Vertices.Select(Geometry), lit.Vertices.Select(Geometry));
        Assert.All(lit.Vertices, vertex => Assert.Equal(255, vertex.emissiveMaterial.A));
        mesh.MarkUploaded();

        mesh.MarkInteractiveCell(2);
        mesh.RequestMeshRebuild();
        var preview = Build(0, true);
        Assert.Equal(lit.Vertices, preview.Vertices);
        Assert.True(mesh.HasDisplayedLightPreview);
        mesh.MarkUploaded();
        Assert.True(mesh.FinishInteractiveUpload());
        mesh.RequestMeshRebuild();

        var unlitAgain = Build(0);
        Assert.True(mesh.CanReuseUploadedTopology);
        Assert.True(mesh.HasChangedLighting);
        Assert.Equal(unlit.Vertices, unlitAgain.Vertices);

        (TerrainVertex[] Vertices, int[] Indices) Build(int intensity, bool preview = false)
        {
            chunk.SetCellValueFast(
                8,
                2,
                8,
                Terrain.MakeBlockValue(lamp.BlockIndex, data: LightbulbBlock.SetLightIntensity(mounting, intensity))
            );
            chunk.NetworkContentVersion++;
            mesh.PrepareMeshUpdate(chunk, preview);
            Assert.True(builder.GenerateSlice(chunk, mesh, 0, TimeSpan.MaxValue));
            TerrainMeshTestData.CompleteSlices(mesh);
            Assert.True(mesh.CompleteMeshBuild(chunk));
            var subset = mesh.Draws[fixture.Texture][0].SubsetAlphaTest;
            return (subset.Vertices.ToArray(), subset.Indices.ToArray());
        }

        static TerrainGeometryVertex Geometry(TerrainVertex vertex) =>
            new(new Vector3(vertex.X, vertex.Y, vertex.Z), vertex.Tx, vertex.Ty, vertex.Color);
    }

    [Fact]
    public void BoundaryMeshJoinsAChangeToTheNeighborSectionItActuallySamples()
    {
        using var fixture = new Fixture();
        var chunk = fixture.Chunk;
        var neighbor = fixture.Terrain.AllocateChunk(1, 0);
        TerrainMeshTestData.PrepareLighting(neighbor);
        var input = fixture.Terrain.Lighting.CaptureInput(neighbor);
        var light = new TerrainLightSnapshot(default);
        Assert.True(fixture.Terrain.Lighting.TryPublish(neighbor, input, light));
        chunk.SetCellValueFast(15, 2, 8, Terrain.MakeBlockValue(3));
        chunk.SetTopHeightFast(15, 8, 2);
        using var mesh = new TerrainChunkPresentation();
        mesh.PrepareMeshUpdate(chunk);
        var builder = new TerrainMeshBuilder(fixture.Terrain, fixture.Generator, fixture.Texture);
        Assert.True(builder.GenerateSlice(chunk, mesh, 0, TimeSpan.MaxValue));
        TerrainMeshTestData.CompleteSlices(mesh);
        Assert.True(mesh.CompleteMeshBuild(chunk));
        mesh.MarkUploaded();

        var allocation = new ChunkAllocationId(neighbor.Coords, neighbor.AllocationGeneration);
        var sampled = new TerrainLightPresentationBatch.Source(allocation, 2, 1);
        Assert.True(TerrainLightUploadGroup.IsAffected(chunk, mesh, sampled));
        Assert.False(TerrainLightUploadGroup.IsAffected(chunk, mesh, sampled with { Sections = 1 << 4 }));
        Assert.False(
            TerrainLightUploadGroup.IsAffected(
                chunk,
                mesh,
                sampled with { Boundaries = default(TerrainLightBoundaryChanges) }
            )
        );

        var before = mesh.generatedSliceInputs[0];
        var bright = new TerrainLightSection(new TerrainLightChannels(0, 15));
        Assert.True(fixture.Terrain.Lighting.TryPublish(neighbor, input, light.WithSection(4, bright)));
        Assert.Equal(before, builder.CalculateSliceInputs(chunk, 0, lightReads: mesh.LightReadsForSlice(0)));
        Assert.True(fixture.Terrain.Lighting.TryPublish(neighbor, input, light.WithSection(0, bright)));
        var changed = builder.CalculateSliceInputs(chunk, 0, lightReads: mesh.LightReadsForSlice(0));
        Assert.Equal(before.Geometry, changed.Geometry);
        Assert.NotEqual(before.Lighting, changed.Lighting);
    }

    [Fact]
    public void HeightAdjustedFacesReplayTheOriginalLightSamples()
    {
        using var fixture = new Fixture();
        var generator = fixture.Generator;
        var chunk = fixture.Chunk;
        var lights = new TerrainLightSnapshot?[9];
        lights[4] = new TerrainLightSnapshot(default);
        generator.SetLightSnapshots(chunk.Coords, lights);
        var dark = Build();

        var cells = new byte[65536];
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var y = 0; y < 5; y++)
                {
                    cells[TerrainChunk.CalculateCellIndex(x, y, z)] = (byte)(1 + (x + 2 * y + z) % 15);
                }
            }
        }

        lights[4] = TerrainLightSnapshot.Capture(cells);
        generator.SetLightSnapshots(chunk.Coords, lights);
        var expected = Build();
        for (var group = 0; group < dark.Subsets.Length; group++)
        {
            var subset = dark.Subsets[group];
            var start = 0;
            for (var runIndex = 0; runIndex < subset.LightingRunCount; runIndex++)
            {
                var run = subset.GetLightingRun(runIndex);
                generator.RefreshLighting(subset, run, start, run.End);
                start = run.End;
            }

            Assert.Equal(subset.Vertices.Count, start);
            Assert.Equal(expected.Subsets[group].Vertices.ToArray(), subset.Vertices.ToArray());
            Assert.Equal(expected.Subsets[group].Indices.ToArray(), subset.Indices.ToArray());
        }

        TerrainGeometry Build()
        {
            var geometry = new TerrainGeometry();
            generator.GenerateCubeVertices(
                fixture.Presentations.Get(3),
                3,
                8,
                2,
                8,
                0f,
                .25f,
                .5f,
                .75f,
                Color.White,
                Color.White,
                Color.White,
                Color.White,
                Color.White,
                -1,
                geometry.OpaqueSubsetsByFace
            );
            return geometry;
        }
    }

    [Fact]
    public void LightingRunGapsPreventReplayUntilTheSubsetIsRebuilt()
    {
        var subset = new TerrainGeometrySubset();
        subset.Vertices.Count = 8;
        subset.RecordLighting(0, 4, 1);
        subset.RecordLighting(4, 4, 1);
        Assert.True(subset.HasCompleteLighting);
        Assert.Equal(1, subset.LightingRunCount);

        subset.ClearLighting();
        subset.RecordLighting(0, 2, 1);
        subset.RecordLighting(3, 5, 2);
        Assert.False(subset.HasCompleteLighting);

        subset.ClearLighting();
        subset.RecordLighting(0, 4, 1);
        subset.RecordLighting(4, 4, 2);
        Assert.True(subset.HasCompleteLighting);
        Assert.Equal(new TerrainVertexLightRun(4, 1), subset.GetLightingRun(0));
        Assert.Equal(new TerrainVertexLightRun(8, 2), subset.GetLightingRun(1));
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

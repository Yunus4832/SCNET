using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.Terrains.Lighting;
using Game.Terrains.Rendering;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains.Distribution;

[Collection(ConfigFileCollection.Name)]
public sealed class ClientChunkDerivationPipelineTest
{
    [Fact]
    public void UploadedContentsRemainDrawableForFadeWhileLightingIsBusyOrDirty()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.NetworkContentVersion = 1;
        var presentation = presentations.Get(chunk);
        TerrainMeshTestData.Complete(chunk, presentation);
        Assert.False(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentation));
        presentation.MarkUploaded();
        chunk.DerivedRevisions.BeginLightWrite();
        try
        {
            Assert.False(chunk.HasCurrentLight);
            Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentation));
        }
        finally { chunk.DerivedRevisions.EndLightWrite(); }

        chunk.DerivedRevisions.InvalidateLight();
        Assert.False(chunk.HasCurrentLight);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentation));
        terrain.FreeChunk(chunk);
        Assert.False(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentation));
    }

    [Fact]
    public void GeometryIsCurrentOnlyAfterCompletingInstalledContentVersion()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(1, 2);
        chunk.HasContents = true;
        chunk.NetworkContentVersion = 7;


        Assert.False(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Replica,
                chunk,
                presentations.Get(chunk)
            )
        );

        TerrainMeshTestData.Complete(chunk, presentations.Get(chunk));

        Assert.True(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Replica,
                chunk,
                presentations.Get(chunk)
            )
        );
        Assert.True(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Authority,
                chunk,
                presentations.Get(chunk)
            )
        );
    }

    [Fact]
    public void NewContentInvalidatesTargetAndLoadedNeighborGeometryVersions()
    {
        using var presentations = new TerrainChunkPresentationStore();
        var terrain = new Terrain();
        var target = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        target.HasContents = true;
        target.NetworkContentVersion = 4;
        TerrainMeshTestData.CompleteAtVersion(target, presentations.Get(target), 3);
        neighbor.HasContents = true;


        TerrainMeshTestData.CompleteAtVersion(neighbor, presentations.Get(neighbor), 8);

        using var scope = new WorldCatalogScope(RuntimeSide.Client);
        var world = new SubsystemTerrain { Terrain = terrain };
        scope.Project.InitializeSubsystem(world);
        var content = new ClientChunkContentCoordinator(terrain);
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.ClientChunkContents))!.SetValue(world, content);
        using var computations = new TerrainComputationScheduler(
            world,
            new TerrainLightingCalculator(
                terrain,
                scope.Catalog,
                true,
                candidate => candidate.HasContents && !candidate.NeedsContentValidation
            )
        );
        content.ContentInstalled += computations.ContentAvailable;
        Assert.True(
            content.TryInstall(
                new ClientChunkSnapshot(
                    new ChunkAllocationId(target.Coords, target.AllocationGeneration),
                    5,
                    new int[AuthorityChunkSnapshot.CellCount],
                    new long[AuthorityChunkSnapshot.ShaftCount]
                )
            )
        );
        new ClientChunkDerivationPipeline(presentations).Begin(target);

        Assert.False(target.HasCurrentHeightData);
        Assert.False(target.HasCurrentLight);
        Assert.Equal(3, presentations.Get(target).ClientGeometryContentVersion);
        Assert.Equal(8, presentations.Get(neighbor).ClientGeometryContentVersion);
        Assert.False(neighbor.HasCurrentLight);
        Assert.True(neighbor.DerivedRevisions.HasCurrentHeights);
    }

    [Fact]
    public void ClientKeepsLastUploadedGeometryVisibleDuringDerivedRebuild()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.NetworkContentVersion = 6;

        TerrainMeshTestData.CompleteAtVersion(chunk, presentations.Get(chunk), 6);
        presentations.Get(chunk).GeometryUploaded = true;

        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));

        chunk.NetworkContentVersion = 7;
        Assert.False(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Replica,
                chunk,
                presentations.Get(chunk)
            )
        );
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));
    }

    [Fact]
    public void ClientDoesNotDrawInitialContentBeforeGeometryUpload()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.NetworkContentVersion = 1;
        TerrainMeshTestData.CompleteAtVersion(chunk, presentations.Get(chunk), 1);
        presentations.Get(chunk).GeometryUploaded = false;

        Assert.True(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Replica,
                chunk,
                presentations.Get(chunk)
            )
        );
        Assert.False(
            ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk))
        );
    }

    [Fact]
    public void GuiAuthorityKeepsUploadedGeometryVisibleDuringDerivedRebuild()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);

        presentations.Get(chunk).GeometryUploaded = true;

        Assert.True(
            ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Authority, chunk, presentations.Get(chunk))
        );

        presentations.Get(chunk).GeometryUploaded = false;
        Assert.False(
            ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Authority, chunk, presentations.Get(chunk))
        );
    }

    [Fact]
    public void ClientDerivationAcceptsTheInstalledLightingState()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(1, 2);
        chunk.HasContents = true;


        var request = chunk.DerivedRevisions.Capture();
        new ClientChunkDerivationPipeline(presentations).Begin(chunk);
        Assert.Equal(request, chunk.DerivedRevisions.Capture());
        Assert.False(chunk.HasCurrentLight);
    }
}

using Engine.Core;

using Game;
using Game.Network.Packages;
using Game.Network.Packages.Handlers;
using Game.Network.Serialization;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class ClientTerrainPredictionTest
{
    private static readonly Point3 _cell = new(15, 64, 15);

    [Fact]
    public void PickupLightSamplingSkipsPredictedOpaquePlacementUntilItIsRemovedOrConfirmed()
    {
        using var world = new WorldCatalogScope();
        world.Catalog.Blocks[0].Transparent = true;
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        Assert.False(SubsystemPickables.IsInsidePredictedOpaqueBlock(terrain, _cell));
        var id = prediction.Begin(_cell, 0, 3, 0);
        Assert.True(SubsystemPickables.IsInsidePredictedOpaqueBlock(terrain, _cell));
        prediction.Complete(id, _cell, 0);
        Assert.False(SubsystemPickables.IsInsidePredictedOpaqueBlock(terrain, _cell));

        prediction.Begin(_cell, 0, 3, 0);
        chunk.SetCellValueFast(15, 64, 15, 3);
        prediction.OnCellChanged(_cell);
        Assert.False(SubsystemPickables.IsInsidePredictedOpaqueBlock(terrain, _cell));

        prediction.Begin(_cell, 3, 0, 0);
        Assert.False(SubsystemPickables.IsInsidePredictedOpaqueBlock(terrain, _cell));
    }

    [Fact]
    public void DigRayHitsPendingPlacementInsteadOfBlockBelowButPhysicsRayDoesNot()
    {
        using var world = new WorldCatalogScope();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(15, 63, 15, 3);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        var id = prediction.Begin(_cell, 0, 7, 0);
        var start = new Vector3(15.5f, 66, 15.5f);
        var end = new Vector3(15.5f, 61, 15.5f);

        var physical = subsystem.Raycast(start, end, true, true, null);
        var digging = subsystem.Raycast(start, end, true, true, null, includePredictedBlocks: true);
        Assert.Equal(63, physical!.Value.CellFace.Y);
        Assert.Equal(_cell, digging!.Value.CellFace.Point);
        Assert.Equal(7, Terrain.ExtractContents(digging.Value.Value));
        Assert.True(prediction.IsPending(digging.Value.CellFace.Point));

        chunk.SetCellValueFast(15, 64, 15, 7);
        prediction.OnCellChanged(_cell);
        prediction.Complete(id, _cell, 0);
        Assert.False(prediction.IsPending(_cell));
        Assert.Equal(_cell, subsystem.Raycast(start, end, true, true, null)!.Value.CellFace.Point);
    }

    [Fact]
    public void RejectedOrExpiredPlacementStopsBlockingTheDigRay()
    {
        using var world = new WorldCatalogScope();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(15, 63, 15, 3);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        var start = new Vector3(15.5f, 66, 15.5f);
        var end = new Vector3(15.5f, 61, 15.5f);
        var id = prediction.Begin(_cell, 0, 7, 0);
        prediction.Complete(id, _cell, 0);
        Assert.False(prediction.IsPending(_cell));
        Assert.Equal(63, subsystem.Raycast(start, end, true, true, null, includePredictedBlocks: true)!.Value.CellFace.Y);

        prediction.Begin(_cell, 0, 7, 0);
        prediction.Update(ClientTerrainPrediction.TimeoutSeconds);
        Assert.False(prediction.IsPending(_cell));
        Assert.Equal(63, subsystem.Raycast(start, end, true, true, null, includePredictedBlocks: true)!.Value.CellFace.Y);
    }

    [Fact]
    public void DigPredictionDoesNotMakeTheDigRayPassThroughAnUnconfirmedRemoval()
    {
        using var world = new WorldCatalogScope();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(15, 64, 15, 7);
        chunk.SetCellValueFast(15, 63, 15, 3);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        prediction.Begin(_cell, 7, 0, 0);
        var hit = subsystem.Raycast(new Vector3(15.5f, 66, 15.5f), new Vector3(15.5f, 61, 15.5f),
            true, true, null, includePredictedBlocks: true);
        Assert.Equal(_cell, hit!.Value.CellFace.Point);
        Assert.True(prediction.IsPending(hit.Value.CellFace.Point));
    }

    [Fact]
    public void PlacementShowsOrientedBlockWithoutChangingAirAndRestoresOnRejection()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var air = Terrain.MakeBlockValue(0, 15, 0);
        var block = Terrain.MakeBlockValue(3, 0, 5);
        chunk.SetCellValueFast(15, 64, 15, air);
        chunk.NetworkContentVersion = 10;
        var revision = chunk.NetworkContentRevision;
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        var id = prediction.Begin(_cell, air, block, 0);
        Assert.True(id > 0);
        Assert.Equal(3, Terrain.ExtractContents(chunk.GetCellValueForGeometry(15, 64, 15)));
        Assert.Equal(5, Terrain.ExtractData(chunk.GetCellValueForGeometry(15, 64, 15)));
        Assert.Equal(air, chunk.GetCellValueFast(15, 64, 15));
        Assert.Equal(revision, chunk.NetworkContentRevision);
        prediction.Complete(id, _cell, 10);
        Assert.Equal(air, chunk.GetCellValueForGeometry(15, 64, 15));
    }

    [Fact]
    public void DigAndPlacementShareIdsWithoutCancellingEachOther()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(15, 64, 15, 3);
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        var dig = prediction.Begin(_cell, 3, 0, 0);
        var place = prediction.Begin(new Point3(15, 65, 15), 0, 7, 0);
        Assert.NotEqual(dig, place);
        prediction.Complete(dig, _cell, 0);
        Assert.Equal(3, chunk.GetCellValueForGeometry(15, 64, 15));
        Assert.Equal(7, chunk.GetCellValueForGeometry(15, 65, 15));
        chunk.SetCellValueFast(15, 65, 15, 7);
        prediction.OnCellChanged(new Point3(15, 65, 15));
        prediction.Complete(place, new Point3(15, 65, 15), 0);
        Assert.Equal(7, chunk.GetCellValueForGeometry(15, 65, 15));
    }

    [Fact]
    public void PredictedExcavationRecalculatesSunlightWithoutCommittingTheBlock()
    {
        using var world = new WorldCatalogScope();
        world.Catalog.Blocks[0].Transparent = true;
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var y = 0; y <= 64; y++)
                {
                    chunk.SetCellValueFast(x, y, z, 3);
                }
            }
        }

        TerrainUpdater.GenerateChunkSunLightAndHeight(chunk, 15);
        Assert.Equal(0, chunk.GetCellLightFast(15, 64, 15));
        var revision = chunk.NetworkContentRevision;
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        var id = prediction.Begin(_cell, 3, 0, 0);
        TerrainUpdater.GenerateChunkSunLightAndHeight(chunk, 15);
        var predicted = chunk.GetCellValueForGeometry(15, 64, 15);
        Assert.Equal(15, Terrain.ExtractLight(predicted));
        Assert.Equal(3, chunk.GetCellContentsFast(15, 64, 15));
        Assert.Equal(revision, chunk.NetworkContentRevision);

        prediction.Complete(id, _cell, 0);
        TerrainUpdater.GenerateChunkSunLightAndHeight(chunk, 15);
        Assert.Equal(0, chunk.GetCellLightFast(15, 64, 15));
        Assert.Equal(3, chunk.GetCellContentsFast(15, 64, 15));

        chunk.SetCellValueFast(15, 64, 15, 0);
        TerrainUpdater.GenerateChunkSunLightAndHeight(chunk, 15);
        Assert.Equal(predicted, chunk.GetCellValueFast(15, 64, 15));
    }

    [Fact]
    public void PredictionOnlyChangesGeometryAndPreservesLightAndRevision()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var value = Terrain.MakeBlockValue(3, 12, 7);
        chunk.SetCellValueFast(15, 64, 15, value);
        var revision = chunk.NetworkContentRevision;
        var invalidated = new List<Point2>();
        var prediction = new ClientTerrainPrediction(terrain, invalidated.Add);
        Assert.True(prediction.Begin(_cell, value, 0, 0) > 0);

        Assert.Equal(value, terrain.GetCellValueFast(15, 64, 15));
        Assert.Equal(0, Terrain.ExtractContents(terrain.GetCellValueForGeometry(15, 64, 15)));
        Assert.Equal(12, Terrain.ExtractLight(terrain.GetCellValueForGeometry(15, 64, 15)));
        Assert.Equal(revision, chunk.NetworkContentRevision);
        Assert.Equal(0, chunk.ModificationCounter);
        Assert.Equal(new Point2(0, 0), Assert.Single(invalidated));
    }

    [Fact]
    public void RejectionRestoresAuthoritativeValueWithoutWritingTerrain()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(15, 64, 15, 3);
        chunk.NetworkContentVersion = 10;
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        var id = prediction.Begin(_cell, 3, 0, 0);
        prediction.Complete(id, _cell, 10);
        Assert.Equal(3, terrain.GetCellValueForGeometry(15, 64, 15));
        Assert.Equal(3, terrain.GetCellValueFast(15, 64, 15));
    }

    [Fact]
    public void CompletionBeforeDeltaWaitsForRequiredContentVersion()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(15, 64, 15, 3);
        chunk.NetworkContentVersion = 10;
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        var id = prediction.Begin(_cell, 3, 0, 0);
        prediction.Complete(id, _cell, 11);
        prediction.Update(1);
        Assert.Equal(0, terrain.GetCellValueForGeometry(15, 64, 15));

        chunk.SetCellValueFast(15, 64, 15, 0);
        chunk.NetworkContentVersion = 11;
        prediction.OnCellChanged(_cell);
        prediction.Update(2);
        Assert.Equal(0, terrain.GetCellValueFast(15, 64, 15));
        Assert.Equal(0, terrain.GetCellValueForGeometry(15, 64, 15));
    }

    [Fact]
    public void ConcurrentReplacementAndLateReplyNeverRestoreOldBlock()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(15, 64, 15, 3);
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        var id = prediction.Begin(_cell, 3, 0, 0);
        chunk.SetCellValueFast(15, 64, 15, 7);
        Assert.Equal(7, terrain.GetCellValueForGeometry(15, 64, 15));
        prediction.OnCellChanged(_cell);
        prediction.Complete(id, _cell, 0);
        Assert.Equal(7, terrain.GetCellValueForGeometry(15, 64, 15));
    }

    [Fact]
    public void OldReplyCannotCancelNewPredictionAtSameCell()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(15, 64, 15, 3);
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        var old = prediction.Begin(_cell, 3, 0, 0);
        var current = prediction.Begin(_cell, 3, 0, 0.1);
        Assert.NotEqual(old, current);
        prediction.Complete(old, _cell, 0);
        Assert.Equal(0, terrain.GetCellValueForGeometry(15, 64, 15));
        prediction.Update(0.1 + ClientTerrainPrediction.TimeoutSeconds);
        Assert.Equal(3, terrain.GetCellValueForGeometry(15, 64, 15));
    }

    [Fact]
    public void SnapshotAndChunkReallocationRemoveOverrides()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(15, 64, 15, 3);
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        prediction.Begin(_cell, 3, 0, 0);
        prediction.OnContentInstalled(chunk);
        Assert.Equal(3, terrain.GetCellValueForGeometry(15, 64, 15));

        prediction.Begin(_cell, 3, 0, 0);
        terrain.FreeChunk(chunk);
        var replacement = terrain.AllocateChunk(0, 0);
        replacement.SetCellValueFast(15, 64, 15, 7);
        prediction.Update(1);
        Assert.Equal(7, terrain.GetCellValueForGeometry(15, 64, 15));
    }

    [Fact]
    public void CompletionPacketRoundTripsAndOnlyAcceptsServerDirection()
    {
        var package = new SubsystemTerrainPackage
        {
            Type = SubsystemTerrainPackage.DataType.CellEditCompleted,
            EditRequestId = 123,
            EditedCell = _cell,
            EditContentVersion = 789
        };
        using var writer = new PackageStreamWriter();
        package.WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var restored = new SubsystemTerrainPackage();
        restored.ReadData(reader);
        Assert.Equal(package.EditRequestId, restored.EditRequestId);
        Assert.Equal(package.EditedCell, restored.EditedCell);
        Assert.Equal(package.EditContentVersion, restored.EditContentVersion);
        Assert.True(SubsystemTerrainPackageHandler.AcceptsDirection(restored.Type, false));
        Assert.False(SubsystemTerrainPackageHandler.AcceptsDirection(restored.Type, true));
    }
}

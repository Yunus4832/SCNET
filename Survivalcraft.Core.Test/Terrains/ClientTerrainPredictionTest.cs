using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class ClientTerrainPredictionTest
{
    private static readonly Point3 _cell = new(15, 64, 15);

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
}

using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Execution;
using Game.Terrains.Lighting;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class ClientTerrainConfirmationTest
{
    [Fact]
    public void MatchingPredictionConfirmationPreservesHeightAndLightRequests()
    {
        using var scope = new WorldCatalogScope(RuntimeSide.Client);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(15, 64, 15, 3);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        scope.Project.AddSubsystem(subsystem);
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellAuthority))!
            .SetValue(subsystem, new TerrainCellAuthority(terrain));
        using var scheduler = new TerrainComputationScheduler(
            subsystem,
            new TerrainLightingCalculator(terrain, scope.Catalog, true, _ => true)
        );
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Computations))!.SetValue(subsystem, scheduler);
        var behaviors = new SubsystemBlockBehaviors();
        scope.Project.AddSubsystem(behaviors);
        behaviors.Load(new ValuesDictionary());
        typeof(SubsystemTerrain).GetField("_subsystemBlockBehaviors", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(subsystem, behaviors);
        var prediction = new ClientTerrainPrediction(
            terrain,
            cell => scheduler.InvalidateContentNeighborhood(terrain.GetChunkAtCell(cell.X, cell.Z, false)!.Coords, 1)
        );
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.CellPrediction))!.SetValue(subsystem, prediction);
        prediction.Begin(new Point3(15, 64, 15), 3, 0, 0);
        TerrainMeshTestData.PrepareLighting(chunk);
        var request = chunk.DerivedRevisions.Capture();
        var revision = subsystem.TerrainInteractionRevision;
        subsystem.ChangeCellNet(15, 64, 15, 0);
        Assert.Equal(0, terrain.GetCellValue(15, 64, 15));
        Assert.False(prediction.IsPending(new Point3(15, 64, 15)));
        Assert.Equal(request, chunk.DerivedRevisions.Capture());
        Assert.True(chunk.HasCurrentHeightData);
        Assert.True(chunk.HasCurrentLight);
        Assert.Equal(revision, subsystem.TerrainInteractionRevision);
    }
}

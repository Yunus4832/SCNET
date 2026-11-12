using Engine.Core.Collections;
using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Blocks.Presentation.Geometry;
using Game.ClientContent;
using Game.ElectricElements;
using Game.Modding.Blocks;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class ElectricWirePredictionTest
{
    [Theory]
    [InlineData(14, false, false, false)]
    [InlineData(14, false, false, true)]
    [InlineData(14, false, true, false)]
    [InlineData(14, false, true, true)]
    [InlineData(14, true, false, false)]
    [InlineData(14, true, false, true)]
    [InlineData(14, true, true, false)]
    [InlineData(14, true, true, true)]
    [InlineData(15, false, false, false)]
    [InlineData(15, false, false, true)]
    [InlineData(15, false, true, false)]
    [InlineData(15, false, true, true)]
    [InlineData(15, true, false, false)]
    [InlineData(15, true, false, true)]
    [InlineData(15, true, true, false)]
    [InlineData(15, true, true, true)]
    public void PredictionDrawsConnectionsUntilItIsConfirmedOrCancelled(
        int x,
        bool predictOrigin,
        bool wireNeighbor,
        bool confirm
    )
    {
        using var world = new WorldCatalogScope(RuntimeSide.Client);
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0);
        terrain.AllocateChunk(1, 0);
        var switchBlock = (SwitchBlock)world.Catalog.GetBlock(ResourceId.Parse("game:switch"));
        var wireBlock = (WireBlock)world.Catalog.GetBlock(ResourceId.Parse("game:wire"));
        var switchValue = Terrain.MakeBlockValue(switchBlock.BlockIndex, 4 << 1);
        var gate = world.Catalog.GetBlock(ResourceId.Parse("game:and_gate"));
        var neighborValue = wireNeighbor
            ? wireBlock.SetWireFacesBitmask(Terrain.MakeBlockValue(wireBlock.BlockIndex), 1 << 4)
            : Terrain.MakeBlockValue(gate.BlockIndex, 4 << 2);
        var origin = new Point3(x, 64, 8);
        var neighbor = new Point3(x + 1, 64, 8);
        var predictedCell = predictOrigin ? origin : neighbor;
        var predictedValue = predictOrigin ? switchValue : neighborValue;
        if (predictOrigin) { terrain.SetCellValueFast(neighbor.X, neighbor.Y, neighbor.Z, neighborValue); }
        else { terrain.SetCellValueFast(origin.X, origin.Y, origin.Z, switchValue); }

        var subsystem = new SubsystemTerrain { Terrain = terrain };
        world.Project.InitializeSubsystem(subsystem);
        var electricity = new SubsystemElectricity();
        world.Project.InitializeSubsystem(electricity);
        typeof(SubsystemElectricity).GetProperty(nameof(SubsystemElectricity.SubsystemTerrain))!
            .SetValue(electricity, subsystem);
        using var presentations = new BlockPresentationCatalog(
            world.Catalog,
            new BuiltInClientContentModule().BlockPresentations
        );
        var blockPresentation = new SubsystemBlockPresentation();
        blockPresentation.Bind(presentations);
        world.Project.AddSubsystem(blockPresentation);
        var generator = new BlockGeometryGenerator(terrain, subsystem, electricity, null!, null!, null!);
        var prediction = new ClientTerrainPrediction(terrain, _ => { });
        Assert.NotEqual(0, prediction.Begin(predictedCell, 0, predictedValue, 0));
        Assert.Equal(0, terrain.GetCellValue(predictedCell.X, predictedCell.Y, predictedCell.Z));
        var connections = new DynamicArray<ElectricConnectionPath>();
        electricity.GetAllConnectedNeighbors(x, 64, 8, 4, connections);
        Assert.Empty(connections);
        var predicted = new TerrainGeometrySubset();
        generator.GenerateWireVertices(switchValue, x, 64, 8, 4, 0.5f, Vector2.Zero, predicted);
        Assert.NotEmpty(predicted.Indices);

        if (confirm) { terrain.SetCellValueFast(predictedCell.X, predictedCell.Y, predictedCell.Z, predictedValue); }

        prediction.OnCellChanged(predictedCell);
        var confirmed = new TerrainGeometrySubset();
        generator.GenerateWireVertices(switchValue, x, 64, 8, 4, 0.5f, Vector2.Zero, confirmed);
        electricity.GetAllConnectedNeighbors(x, 64, 8, 4, connections);
        if (confirm)
        {
            Assert.Equal(predicted.Indices.ToArray(), confirmed.Indices.ToArray());
            Assert.Equal(predicted.Vertices.Count, confirmed.Vertices.Count);
            Assert.NotEmpty(connections);
        }
        else
        {
            Assert.Empty(confirmed.Indices);
            Assert.Empty(connections);
        }
    }
}

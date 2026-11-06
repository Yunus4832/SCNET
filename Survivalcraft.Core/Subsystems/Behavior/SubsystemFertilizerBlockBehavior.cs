using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemFertilizerBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemSpatialSoundRequests _soundRequests = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:saltpeter_chunk")];

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        var terrainRaycastResult = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
        if (terrainRaycastResult is not { CellFace.Face: 4 })
        {
            return false;
        }

        var y = terrainRaycastResult.Value.CellFace.Y;
        for (var i = terrainRaycastResult.Value.CellFace.X - 1; i <= terrainRaycastResult.Value.CellFace.X + 1; i++)
        {
            for (var j = terrainRaycastResult.Value.CellFace.Z - 1; j <= terrainRaycastResult.Value.CellFace.Z + 1; j++)
            {
                var cellValue = _subsystemTerrain.Terrain.GetCellValue(i, y, j);
                if (Terrain.ExtractContents(cellValue) != 168)
                {
                    continue;
                }

                var data = SoilBlock.SetNitrogen(Terrain.ExtractData(cellValue), 3);
                var value = Terrain.ReplaceData(cellValue, data);
                _subsystemTerrain.ChangeCell(i, y, j, value);
            }
        }

        _soundRequests.RequestRandomSound(
            "Audio/Impacts/Dirt",
            0.5f,
            0f,
            new Vector3(
                terrainRaycastResult.Value.CellFace.X,
                terrainRaycastResult.Value.CellFace.Y,
                terrainRaycastResult.Value.CellFace.Z
            ),
            3f,
            true
        );
        var position = new Vector3(
            terrainRaycastResult.Value.CellFace.X + 0.5f,
            terrainRaycastResult.Value.CellFace.Y + 1.5f,
            terrainRaycastResult.Value.CellFace.Z + 0.5f
        );
        _subsystemTerrain.RequestDestructionDebris(componentMiner.ActiveBlockValue, position, 1.25f);
        componentMiner.RemoveActiveTool(1);
        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _soundRequests = Project.FindSubsystem<SubsystemSpatialSoundRequests>(true)!;
    }
}

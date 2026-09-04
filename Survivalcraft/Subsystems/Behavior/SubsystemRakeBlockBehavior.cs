using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemRakeBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemParticles _subsystemParticles = null!;
    private SubsystemTerrain _subsystemTerrain = null!;

    public override int[] HandledBlocks =>
    [
        169,
        219,
        171,
        172
    ];

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        var terrainRaycastResult = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
        if (!terrainRaycastResult.HasValue)
        {
            return false;
        }

        if (terrainRaycastResult.Value.CellFace.Face == 4)
        {
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(terrainRaycastResult.Value.CellFace.X,
                terrainRaycastResult.Value.CellFace.Y, terrainRaycastResult.Value.CellFace.Z);
            var num = Terrain.ExtractContents(cellValue);
            var block = BlocksManager.Blocks[num];
            switch (num)
            {
                case 2:
                    {
                        var value2 = Terrain.ReplaceContents(cellValue, 168);
                        _subsystemTerrain.ChangeCell(terrainRaycastResult.Value.CellFace.X,
                            terrainRaycastResult.Value.CellFace.Y, terrainRaycastResult.Value.CellFace.Z, value2);
                        _subsystemAudio.PlayRandomSound("Audio/Impacts/Dirt", 0.5f, 0f,
                            new Vector3(terrainRaycastResult.Value.CellFace.X, terrainRaycastResult.Value.CellFace.Y,
                                terrainRaycastResult.Value.CellFace.Z), 3f, true);
                        var position2 = new Vector3(terrainRaycastResult.Value.CellFace.X + 0.5f,
                            terrainRaycastResult.Value.CellFace.Y + 1.25f,
                            terrainRaycastResult.Value.CellFace.Z + 0.5f);
                        _subsystemParticles.AddParticleSystem(
                            block.CreateDebrisParticleSystem(_subsystemTerrain, position2, cellValue, 0.5f));
                        break;
                    }
                case 8:
                    {
                        var value = Terrain.ReplaceContents(cellValue, 2);
                        _subsystemTerrain.ChangeCell(terrainRaycastResult.Value.CellFace.X,
                            terrainRaycastResult.Value.CellFace.Y, terrainRaycastResult.Value.CellFace.Z, value);
                        _subsystemAudio.PlayRandomSound("Audio/Impacts/Plant", 0.5f, 0f,
                            new Vector3(terrainRaycastResult.Value.CellFace.X, terrainRaycastResult.Value.CellFace.Y,
                                terrainRaycastResult.Value.CellFace.Z), 3f, true);
                        var position = new Vector3(terrainRaycastResult.Value.CellFace.X + 0.5f,
                            terrainRaycastResult.Value.CellFace.Y + 1.2f, terrainRaycastResult.Value.CellFace.Z + 0.5f);
                        _subsystemParticles.AddParticleSystem(
                            block.CreateDebrisParticleSystem(_subsystemTerrain, position, cellValue, 0.75f));
                        break;
                    }
            }
        }

        componentMiner.DamageActiveTool(1);
        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
    }
}

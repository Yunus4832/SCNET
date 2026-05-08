using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemFenceGateBlockBehavior : SubsystemBlockBehavior
{
    private static readonly Random _sharedRandom = new();

    private SubsystemElectricity _subsystemElectricity = null!;

    public override int[] HandledBlocks => [];

    public bool OpenCloseGate(int x, int y, int z, bool open)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        if (BlocksManager.Blocks[num] is not FenceGateBlock)
        {
            return false;
        }

        var data = FenceGateBlock.SetOpen(Terrain.ExtractData(cellValue), open);
        var value = Terrain.ReplaceData(cellValue, data);
        SubsystemTerrain.ChangeCell(x, y, z, value);
        var name = open ? "Audio/Doors/DoorOpen" : "Audio/Doors/DoorClose";
        SubsystemTerrain.Project.FindSubsystem<SubsystemAudio>(true)!.PlaySound(name, 0.7f,
            _sharedRandom.Float(-0.1f, 0.1f), new Vector3(x, y, z), 4f, true);
        return true;

    }

    private bool IsGateElectricallyConnected(int x, int y, int z)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        var data = Terrain.ExtractData(cellValue);
        if (BlocksManager.Blocks[num] is not FenceGateBlock)
        {
            return false;
        }

        var electricElement = _subsystemElectricity.GetElectricElement(x, y, z, FenceGateBlock.GetHingeFace(data));
        return electricElement is { Connections.Count: > 0 };
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        var cellFace = raycastResult.CellFace;
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
        var num = Terrain.ExtractContents(cellValue);
        var data = Terrain.ExtractData(cellValue);
        if (num != 166 && IsGateElectricallyConnected(cellFace.X, cellFace.Y, cellFace.Z))
        {
            return true;
        }

        var open = FenceGateBlock.GetOpen(data);
        return OpenCloseGate(cellFace.X, cellFace.Y, cellFace.Z, !open);

    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemElectricity = Project.FindSubsystem<SubsystemElectricity>(true)!;
    }
}

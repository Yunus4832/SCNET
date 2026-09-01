using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemTrapdoorBlockBehavior : SubsystemBlockBehavior
{
    private static readonly Random _sharedRandom = new();

    private SubsystemElectricity _subsystemElectricity = null!;

    public override int[] HandledBlocks => [83, 84];

    public bool IsTrapdoorElectricallyConnected(int x, int y, int z)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        var data = Terrain.ExtractData(cellValue);
        if (BlocksManager.Blocks[num] is not TrapdoorBlock)
        {
            return false;
        }

        var electricElement =
            _subsystemElectricity.GetElectricElement(x, y, z, TrapdoorBlock.GetMountingFace(data));
        return electricElement is { Connections.Count: > 0 };
    }

    public bool OpenCloseTrapdoor(int x, int y, int z, bool open)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        if (BlocksManager.Blocks[num] is not TrapdoorBlock)
        {
            return false;
        }

        var data = TrapdoorBlock.SetOpen(Terrain.ExtractData(cellValue), open);
        var value = Terrain.ReplaceData(cellValue, data);
        SubsystemTerrain.ChangeCell(x, y, z, value);
        var name = open ? "Audio/Doors/DoorOpen" : "Audio/Doors/DoorClose";
        SubsystemTerrain.Project.FindSubsystem<SubsystemAudio>(true)!.PlaySound(name, 0.7f,
            _sharedRandom.Float(-0.1f, 0.1f), new Vector3(x, y, z), 4f, true);
        return true;
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        var cellFace = raycastResult.CellFace;
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
        var num = Terrain.ExtractContents(cellValue);
        var data = Terrain.ExtractData(cellValue);
        if (num != 83 && IsTrapdoorElectricallyConnected(cellFace.X, cellFace.Y, cellFace.Z))
        {
            return true;
        }

        var open = TrapdoorBlock.GetOpen(data);
        return OpenCloseTrapdoor(cellFace.X, cellFace.Y, cellFace.Z, !open);
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        var obj = BlocksManager.Blocks[num];
        var data = Terrain.ExtractData(cellValue);
        if (obj is not TrapdoorBlock)
        {
            return;
        }

        var rotation = TrapdoorBlock.GetRotation(data);
        var upsideDown = TrapdoorBlock.GetUpsideDown(data);
        var flag = false;
        var point = CellFace.FaceToPoint3(rotation);
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x - point.X, y - point.Y, z - point.Z);
        flag |= !BlocksManager.Blocks[cellContents].Transparent;
        if (upsideDown)
        {
            var cellContents2 = SubsystemTerrain.Terrain.GetCellContents(x, y + 1, z);
            flag |= !BlocksManager.Blocks[cellContents2].Transparent;
            var cellContents3 = SubsystemTerrain.Terrain.GetCellContents(x - point.X, y - point.Y + 1, z - point.Z);
            flag |= !BlocksManager.Blocks[cellContents3].Transparent;
        }
        else
        {
            var cellContents4 = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
            flag |= !BlocksManager.Blocks[cellContents4].Transparent;
            var cellContents5 = SubsystemTerrain.Terrain.GetCellContents(x - point.X, y - point.Y - 1, z - point.Z);
            flag |= !BlocksManager.Blocks[cellContents5].Transparent;
        }

        if (!flag)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemElectricity = Project.FindSubsystem<SubsystemElectricity>(true)!;
    }
}

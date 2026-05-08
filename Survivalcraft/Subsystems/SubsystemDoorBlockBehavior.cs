using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemDoorBlockBehavior : SubsystemBlockBehavior
{
    private static readonly Random _sharedRandom = new();

    private SubsystemElectricity _subsystemElectricity = null!;

    public override int[] HandledBlocks =>
    [
        56,
        57,
        58
    ];

    public bool OpenCloseDoor(int x, int y, int z, bool open)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        if (BlocksManager.Blocks[num] is not DoorBlock)
        {
            return false;
        }

        var data = DoorBlock.SetOpen(Terrain.ExtractData(cellValue), open);
        var value = Terrain.ReplaceData(cellValue, data);
        SubsystemTerrain.ChangeCell(x, y, z, value);
        var name = open ? "Audio/Doors/DoorOpen" : "Audio/Doors/DoorClose";
        SubsystemTerrain.Project.FindSubsystem<SubsystemAudio>(true)!.PlaySound(name, 0.7f,
            _sharedRandom.Float(-0.1f, 0.1f), new Vector3(x, y, z), 4f, true);
        return true;

    }

    private bool IsDoorElectricallyConnected(int x, int y, int z)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        var data = Terrain.ExtractData(cellValue);
        if (BlocksManager.Blocks[num] is not DoorBlock)
        {
            return false;
        }

        var num2 = DoorBlock.IsBottomPart(SubsystemTerrain.Terrain, x, y, z) ? y : y - 1;
        for (var i = num2; i <= num2 + 1; i++)
        {
            var electricElement = _subsystemElectricity.GetElectricElement(x, i, z, DoorBlock.GetHingeFace(data));
            if (electricElement is { Connections.Count: > 0 })
            {
                return true;
            }
        }

        return false;
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        var cellFace = raycastResult.CellFace;
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
        var num = Terrain.ExtractContents(cellValue);
        var data = Terrain.ExtractData(cellValue);
        if (num == WoodenDoorBlock.Index || !IsDoorElectricallyConnected(cellFace.X, cellFace.Y, cellFace.Z))
        {
            var open = DoorBlock.GetOpen(data);
            return OpenCloseDoor(cellFace.X, cellFace.Y, cellFace.Z, !open);
        }

        return true;
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
        var cellContents2 = SubsystemTerrain.Terrain.GetCellContents(x, y + 1, z);
        if (!BlocksManager.Blocks[cellContents].Transparent && cellContents2 == 0)
        {
            SubsystemTerrain.ChangeCell(x, y + 1, z, value);
        }
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        if (DoorBlock.IsTopPart(SubsystemTerrain.Terrain, x, y, z))
        {
            SubsystemTerrain.ChangeCell(x, y - 1, z, 0);
        }

        if (DoorBlock.IsBottomPart(SubsystemTerrain.Terrain, x, y, z))
        {
            SubsystemTerrain.ChangeCell(x, y + 1, z, 0);
        }
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        var obj = BlocksManager.Blocks[num];
        var data = Terrain.ExtractData(cellValue);
        if (!(obj is DoorBlock))
        {
            return;
        }

        if (neighborX == x && neighborY == y && neighborZ == z)
        {
            if (DoorBlock.IsBottomPart(SubsystemTerrain.Terrain, x, y, z))
            {
                var value = Terrain.ReplaceData(SubsystemTerrain.Terrain.GetCellValue(x, y + 1, z), data);
                SubsystemTerrain.ChangeCell(x, y + 1, z, value);
            }

            if (DoorBlock.IsTopPart(SubsystemTerrain.Terrain, x, y, z))
            {
                var value2 = Terrain.ReplaceData(SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z), data);
                SubsystemTerrain.ChangeCell(x, y - 1, z, value2);
            }
        }

        if (DoorBlock.IsBottomPart(SubsystemTerrain.Terrain, x, y, z))
        {
            var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
            if (BlocksManager.Blocks[cellContents].Transparent)
            {
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
            }
        }

        if (!DoorBlock.IsBottomPart(SubsystemTerrain.Terrain, x, y, z) &&
            !DoorBlock.IsTopPart(SubsystemTerrain.Terrain, x, y, z))
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

namespace Game.Blocks;

public abstract class SlabBlock(int fullBlockIndex) : Block, IPaintableBlock
{
    public BoundingBox[][] CollisionBoxes =
    [
        [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f))],
        [new BoundingBox(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f))]
    ];


    public int FullBlockIndex = fullBlockIndex;


    public virtual int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public virtual int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        var data = Terrain.ExtractData(value);
        return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(data, color));
    }


    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        if (GetIsTop(Terrain.ExtractData(value)))
        {
            return face != 4;
        }

        return face != 5;
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var num = Terrain.ExtractContents(value);
        var data = Terrain.ExtractData(value);
        var num2 = Terrain.ExtractContents(raycastResult.Value);
        var data2 = Terrain.ExtractData(raycastResult.Value);
        BlockPlacementData result;
        if (num2 == num &&
            ((GetIsTop(data2) && raycastResult.CellFace.Face == 5) ||
             (!GetIsTop(data2) && raycastResult.CellFace.Face == 4)))
        {
            var value2 = Terrain.MakeBlockValue(FullBlockIndex, 0, 0);
            if (Catalog.Blocks[FullBlockIndex] is IPaintableBlock paintableBlock)
            {
                var color = GetColor(data);
                value2 = paintableBlock.Paint(subsystemTerrain, value2, color);
            }

            var cellFace = raycastResult.CellFace;
            cellFace.Point -= CellFace.FaceToPoint3(cellFace.Face);
            result = default;
            result.Value = value2;
            result.CellFace = cellFace;
            return result;
        }

        var isTop = raycastResult.CellFace.Face >= 4
            ? raycastResult.CellFace.Face == 5
            : raycastResult.HitPoint().Y - raycastResult.CellFace.Y > 0.5f;
        result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, SetIsTop(data, isTop));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = GetIsTop(Terrain.ExtractData(value)) ? 1 : 0;
        return CollisionBoxes[num];
    }

    public override bool AllowsDestructionDebris(int oldValue, int newValue) =>
        Terrain.ExtractContents(newValue) != FullBlockIndex;

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        showDebris = AllowsDestructionDebris(oldValue, newValue);
        if (showDebris)
        {
            var data = Terrain.ExtractData(oldValue);
            var data2 = SetColor(0, GetColor(data));
            var value = Terrain.MakeBlockValue(BlockIndex, 0, data2);
            dropValues.Add(
                new BlockDropValue
                {
                    Value = value,
                    Count = 1
                }
            );
        }
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, null));
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
    }

    public static bool GetIsTop(int data)
    {
        return (data & 1) != 0;
    }

    public static int SetIsTop(int data, bool isTop)
    {
        return (data & -2) | (isTop ? 1 : 0);
    }

    public static int? GetColor(int data)
    {
        if ((data & 2) != 0)
        {
            return (data >> 2) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -63) | 2 | ((color.Value & 0xF) << 2);
        }

        return data & -63;
    }
}

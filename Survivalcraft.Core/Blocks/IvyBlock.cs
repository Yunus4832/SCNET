namespace Game.Blocks;

public class IvyBlock : Block
{
    public readonly BoundingBox[][] BoundingBoxes = new BoundingBox[][]
    {
        [new BoundingBox(new Vector3(0f, 0f, 0.9375f), new Vector3(1f, 1f, 1f))],
        [new BoundingBox(new Vector3(0.9375f, 0f, 0f), new Vector3(1f, 1f, 1f))],
        [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.0625f))],
        [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(0.0625f, 1f, 1f))]
    };

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var face = GetFace(Terrain.ExtractData(value));
        return face is >= 0 and < 4 ? BoundingBoxes[face] : base.GetCustomCollisionBoxes(terrain, value);
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        if (raycastResult.CellFace.Face >= 4)
        {
            return result;
        }

        result.CellFace = raycastResult.CellFace;
        result.Value =
            Terrain.MakeBlockValue(BlockIndex, SetFace(0, CellFace.OppositeFace(raycastResult.CellFace.Face)));

        return result;
    }


    public static int GetFace(int data)
    {
        return data & 3;
    }

    public static int SetFace(int data, int face)
    {
        return (data & -4) | (face & 3);
    }

    public static bool IsGrowthStopCell(int x, int y, int z)
    {
        return MathUtils.Hash((uint)(x + y * 451 + z * 77437)) % 5u == 0;
    }
}

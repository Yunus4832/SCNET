namespace Game.Blocks;

public class FurnaceBlock : Block
{
    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        return false;
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var forward = eyeForward;
        var num = Vector3.Dot(forward, Vector3.UnitZ);
        var num2 = Vector3.Dot(forward, Vector3.UnitX);
        var num3 = Vector3.Dot(forward, -Vector3.UnitZ);
        var num4 = Vector3.Dot(forward, -Vector3.UnitX);
        var data = 0;
        if (num.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 2;
        }
        else if (num2.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 3;
        }
        else if (num3.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 0;
        }
        else if (num4.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 1;
        }

        BlockPlacementData result = default;
        // Lit furnaces are placed as their unlit counterpart.
        result.Value = Terrain.MakeBlockValue(Catalog.GetBlock(ResourceId.Parse("game:furnace")).BlockIndex, data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }
}

namespace Game.Blocks;

public class ChestBlock : CubeBlock
{
    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var forward = eyeForward;
        var positiveZAlignment = Vector3.Dot(forward, Vector3.UnitZ);
        var positiveXAlignment = Vector3.Dot(forward, Vector3.UnitX);
        var negativeZAlignment = Vector3.Dot(forward, -Vector3.UnitZ);
        var negativeXAlignment = Vector3.Dot(forward, -Vector3.UnitX);
        var data = 0;
        if (positiveZAlignment.CloseTo(
                MathUtils.Max(positiveZAlignment, positiveXAlignment, negativeZAlignment, negativeXAlignment)
            ))
        {
            data = 2;
        }
        else if (positiveXAlignment.CloseTo(
                     MathUtils.Max(positiveZAlignment, positiveXAlignment, negativeZAlignment, negativeXAlignment)
                 ))
        {
            data = 3;
        }
        else if (negativeZAlignment.CloseTo(
                     MathUtils.Max(positiveZAlignment, positiveXAlignment, negativeZAlignment, negativeXAlignment)
                 ))
        {
            data = 0;
        }
        else if (negativeXAlignment.CloseTo(
                     MathUtils.Max(positiveZAlignment, positiveXAlignment, negativeZAlignment, negativeXAlignment)
                 ))
        {
            data = 1;
        }

        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }
}

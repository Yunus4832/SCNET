namespace Game.Blocks;

public class ChestBlock : CubeBlock
{
    public const int Index = 45;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return face switch
        {
            4 or 5 => 42,
            _ => Terrain.ExtractData(value) switch
            {
                0 => face switch
                {
                    0 => 27,
                    2 => 26,
                    _ => 25
                },
                1 => face switch
                {
                    1 => 27,
                    3 => 26,
                    _ => 25
                },
                2 => face switch
                {
                    2 => 27,
                    0 => 26,
                    _ => 25
                },
                _ => face switch
                {
                    3 => 27,
                    1 => 26,
                    _ => 25
                }
            }
        };
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation)
            .Forward;
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
        result.Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, 45), data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }
}

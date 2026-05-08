namespace Game.Blocks;

public abstract class WoodBlock(int cutTextureSlot, int sideTextureSlot) : CubeBlock
{
    public int CutTextureSlot = cutTextureSlot;

    public int SideTextureSlot = sideTextureSlot;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        switch (GetCutFace(Terrain.ExtractData(value)))
        {
            case 4:
                generator.GenerateCubeVertices(this, value, x, y, z, Color.White, geometry.OpaqueSubsetsByFace);
                break;
            case 0:
                generator.GenerateCubeVertices(this, value, x, y, z, 1, 0, 0, Color.White,
                    geometry.OpaqueSubsetsByFace);
                break;
            default:
                generator.GenerateCubeVertices(this, value, x, y, z, 0, 1, 1, Color.White,
                    geometry.OpaqueSubsetsByFace);
                break;
        }
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        var cutFace = GetCutFace(Terrain.ExtractData(value));
        if (cutFace == face || CellFace.OppositeFace(cutFace) == face)
        {
            return CutTextureSlot;
        }

        return SideTextureSlot;
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
        var num = float.NegativeInfinity;
        var cutFace = 0;
        for (var i = 0; i < 6; i++)
        {
            var num2 = Vector3.Dot(CellFace.FaceToVector3(i), forward);
            if (!(num2 > num))
            {
                continue;
            }

            num = num2;
            cutFace = i;
        }

        BlockPlacementData result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, SetCutFace(0, cutFace));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        var data = Terrain.ExtractData(oldValue);
        data = SetCutFace(data, 4);
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(BlockIndex, 0, data),
            Count = 1
        });
        showDebris = true;
    }

    public static int GetCutFace(int data)
    {
        data &= 3;
        return data switch
        {
            0 => 4,
            1 => 0,
            _ => 1
        };
    }

    public static int SetCutFace(int data, int cutFace)
    {
        data &= -4;
        return cutFace switch
        {
            0 or 2 => data | 1,
            1 or 3 => data | 2,
            _ => data
        };
    }
}

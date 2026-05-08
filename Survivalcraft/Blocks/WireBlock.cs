using Engine.Graphics;

namespace Game.Blocks;

public class WireBlock : Block, IElectricWireElementBlock, IElectricElementBlock, IPaintableBlock
{
    public const int Index = 133;

    public static readonly Color WireColor = new(79, 36, 21);

    public readonly BoundingBox[] CollisionBoxesByFace = new BoundingBox[6];

    public readonly BlockMesh StandaloneBlockMesh = new();

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        throw new InvalidOperationException("WireBlock not support CreateElectricElement");
    }

    public ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        if (!WireExistsOnFace(value, face))
        {
            return null;
        }

        return ElectricConnectorType.InputOutput;
    }

    public int GetConnectionMask(int value)
    {
        var color = GetColor(Terrain.ExtractData(value));
        if (!color.HasValue)
        {
            return int.MaxValue;
        }

        return 1 << color.Value;
    }

    public int GetConnectedWireFacesMask(int value, int face)
    {
        var num = 0;
        if (!WireExistsOnFace(value, face))
        {
            return num;
        }

        var num2 = CellFace.OppositeFace(face);
        var flag = false;
        for (var i = 0; i < 6; i++)
        {
            if (i == face)
            {
                num |= 1 << i;
            }
            else if (i != num2 && WireExistsOnFace(value, i))
            {
                num |= 1 << i;
                flag = true;
            }
        }

        if (flag && WireExistsOnFace(value, num2))
        {
            num |= 1 << num2;
        }

        return num;
    }

    public int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public int Paint(SubsystemTerrain? subsystemTerrain, int value, int? color)
    {
        var data = Terrain.ExtractData(value);
        return Terrain.ReplaceData(value, SetColor(data, color));
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Wire");
        var wireMesh = model.FindMesh("Wire")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            wireMesh.ParentBone ??
            throw new InvalidOperationException("Required WireMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(wireMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.9375f, 0f, 0f));
        for (var i = 0; i < 6; i++)
        {
            var v = CellFace.FaceToVector3(i);
            var v2 = new Vector3(0.5f, 0.5f, 0.5f) - 0.5f * v;
            Vector3 v3;
            Vector3 v4;
            if (v.X != 0f)
            {
                v3 = new Vector3(0f, 1f, 0f);
                v4 = new Vector3(0f, 0f, 1f);
            }
            else if (v.Y != 0f)
            {
                v3 = new Vector3(1f, 0f, 0f);
                v4 = new Vector3(0f, 0f, 1f);
            }
            else
            {
                v3 = new Vector3(1f, 0f, 0f);
                v4 = new Vector3(0f, 1f, 0f);
            }

            var v5 = v2 - 0.5f * v3 - 0.5f * v4;
            var v6 = v2 + 0.5f * v3 + 0.5f * v4 + 0.05f * v;
            CollisionBoxesByFace[i] = new BoundingBox(Vector3.Min(v5, v6), Vector3.Max(v5, v6));
        }
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var array = new BoundingBox[6];
        for (var i = 0; i < 6; i++)
        {
            array[i] = WireExistsOnFace(value, i) ? CollisionBoxesByFace[i] : default;
        }

        return array;
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        for (var i = 0; i < 6; i++)
        {
            if (WireExistsOnFace(value, i))
            {
                generator.GenerateWireVertices(value, x, y, z, i, 0f, Vector2.Zero, geometry.SubsetOpaque);
            }
        }
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        var paintColor = GetPaintColor(value);
        var color2 = paintColor.HasValue
            ? color * SubsystemPalette.GetColor(environmentData, paintColor)
            : 1.25f * WireColor * color;
        BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMesh, color2, 2f * size, ref matrix,
            environmentData);
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var point = CellFace.FaceToPoint3(raycastResult.CellFace.Face);
        var cellValue = subsystemTerrain.Terrain.GetCellValue(raycastResult.CellFace.X + point.X,
            raycastResult.CellFace.Y + point.Y, raycastResult.CellFace.Z + point.Z);
        var num = Terrain.ExtractContents(cellValue);
        var block = BlocksManager.Blocks[num];
        var wireFacesBitmask = GetWireFacesBitmask(cellValue);
        var num2 = wireFacesBitmask | (1 << raycastResult.CellFace.Face);
        BlockPlacementData result;
        if (num2 != wireFacesBitmask || !(block is WireBlock))
        {
            result = default;
            result.Value = SetWireFacesBitmask(value, num2);
            result.CellFace = raycastResult.CellFace;
            return result;
        }

        result = default;
        return result;
    }

    public override BlockPlacementData GetDigValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        int toolValue,
        TerrainRaycastResult raycastResult
    )
    {
        var wireFacesBitmask = GetWireFacesBitmask(value);
        wireFacesBitmask &= ~(1 << raycastResult.CollisionBoxIndex);
        BlockPlacementData result = default;
        result.Value = SetWireFacesBitmask(value, wireFacesBitmask);
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
        var paintColor = GetPaintColor(oldValue);
        for (var i = 0; i < 6; i++)
        {
            if (WireExistsOnFace(oldValue, i) && !WireExistsOnFace(newValue, i))
            {
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(133, 0, SetColor(0, paintColor)),
                    Count = 1
                });
            }
        }

        showDebris = dropValues.Count > 0;
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(133);
        yield return Terrain.MakeBlockValue(133, 0, SetColor(0, 0));
        yield return Terrain.MakeBlockValue(133, 0, SetColor(0, 8));
        yield return Terrain.MakeBlockValue(133, 0, SetColor(0, 15));
        yield return Terrain.MakeBlockValue(133, 0, SetColor(0, 11));
        yield return Terrain.MakeBlockValue(133, 0, SetColor(0, 12));
        yield return Terrain.MakeBlockValue(133, 0, SetColor(0, 13));
        yield return Terrain.MakeBlockValue(133, 0, SetColor(0, 14));
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var paintColor = GetPaintColor(value);
        return SubsystemPalette.GetName(paintColor, base.GetDisplayName(subsystemTerrain, value));
    }

    public static bool WireExistsOnFace(int value, int face)
    {
        return (GetWireFacesBitmask(value) & (1 << face)) != 0;
    }

    public static int GetWireFacesBitmask(int value)
    {
        if (Terrain.ExtractContents(value) == 133)
        {
            return Terrain.ExtractData(value) & 0x3F;
        }

        return 0;
    }

    public static int SetWireFacesBitmask(int value, int bitmask)
    {
        var num = Terrain.ExtractData(value);
        num &= -64;
        num |= bitmask & 0x3F;
        return Terrain.ReplaceData(Terrain.ReplaceContents(value, 133), num);
    }

    public static int? GetColor(int data)
    {
        if ((data & 0x40) != 0)
        {
            return (data >> 7) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -1985) | 0x40 | ((color.Value & 0xF) << 7);
        }

        return data & -1985;
    }
}

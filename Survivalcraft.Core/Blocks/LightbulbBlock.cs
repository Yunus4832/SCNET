using Game.Blocks.Collision;

namespace Game.Blocks;

public class LightbulbBlock : MountedElectricElementBlock, IPaintableBlock
{
    public const string TypeName = nameof(LightbulbBlock);


    public readonly BoundingBox[][] CollisionBoxes = new BoundingBox[6][];

    public Color CopperColor = new(118, 56, 32);


    public int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public int Paint(SubsystemTerrain? subsystemTerrain, int value, int? color)
    {
        var data = Terrain.ExtractData(value);
        return Terrain.ReplaceData(value, SetColor(data, color));
    }

    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Lightbulbs.glb");

    public override void Initialize()
    {
        var sidesPositions = new CollisionGeometryBuilder[6];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var sidesNode = model.GetNode("Sides")!;
        var nodeAbsoluteTransform2 = sidesNode.AbsoluteTransform;
        for (var i = 0; i < 6; i++)
        {
            var m = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            sidesPositions[i] = new CollisionGeometryBuilder();
            sidesPositions[i].Append(sidesNode.Primitives[0], nodeAbsoluteTransform2 * m);
            CollisionBoxes[i] = [sidesPositions[i].GetBounds()];
        }
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex);
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
    }


    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }

    public override int GetEmittedLightAmount(int value)
    {
        return GetLightIntensity(Terrain.ExtractData(value));
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        result.Value = Terrain.MakeBlockValue(
            BlockIndex,
            0,
            SetMountingFace(Terrain.ExtractData(value), raycastResult.CellFace.Face)
        );
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
        var color = GetColor(Terrain.ExtractData(oldValue));
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, color)),
                Count = 1
            }
        );
        showDebris = true;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var mountingFace = GetMountingFace(Terrain.ExtractData(value));
        return mountingFace >= CollisionBoxes.Length ? [] : CollisionBoxes[mountingFace];
    }


    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new LightBulbElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)), value);
    }

    public override ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        var face2 = GetFace(value);
        if (face == face2 && SubsystemElectricity.GetConnectorDirection(face2, 0, connectorFace).HasValue)
        {
            return ElectricConnectorType.Input;
        }

        return null;
    }

    public static int GetMountingFace(int data)
    {
        return data & 7;
    }

    public static int SetMountingFace(int data, int face)
    {
        return (data & -8) | (face & 7);
    }

    public static int GetLightIntensity(int data)
    {
        return (data >> 3) & 0xF;
    }

    public static int SetLightIntensity(int data, int intensity)
    {
        return (data & -121) | ((intensity & 0xF) << 3);
    }

    public static int? GetColor(int data)
    {
        if ((data & 0x80) != 0)
        {
            return (data >> 8) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -3969) | 0x80 | ((color.Value & 0xF) << 8);
        }

        return data & -3969;
    }
}

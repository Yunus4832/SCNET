using System.Globalization;

using Game.Blocks.Collision;

namespace Game.Blocks;

public class FourLedBlock : MountedElectricElementBlock
{
    public BoundingBox[][] CollisionBoxesByFace = new BoundingBox[6][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Leds.glb");

    public override void Initialize()
    {
        var blockPositionsByFace = new CollisionGeometryBuilder[6];
        var fourLedNode = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value).GetNode("FourLed")!;
        var nodeAbsoluteTransform = fourLedNode.AbsoluteTransform;
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
            blockPositionsByFace[i] = new CollisionGeometryBuilder();
            blockPositionsByFace[i].Append(fourLedNode.Primitives[0], nodeAbsoluteTransform * m);
            CollisionBoxesByFace[i] = [blockPositionsByFace[i].GetBounds()];
        }
    }


    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        var color = 0;
        while (color < 8)
        {
            var craftingRecipe = new CraftingRecipe
            {
                ResultCount = 4,
                ResultValue = Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, color)),
                RemainsCount = 1,
                RemainsValue = Terrain.MakeBlockValue(90),
                RequiredHeatLevel = 0f,
                Description = new LocalizedText(GetType().Name, "1", string.Empty),
                Ingredients =
                {
                    [0] = "glass",
                    [1] = "glass",
                    [2] = "glass",
                    [4] = "paintbucket:" + color.ToString(CultureInfo.InvariantCulture),
                    [6] = "copperingot",
                    [7] = "copperingot",
                    [8] = "copperingot"
                }
            };
            yield return craftingRecipe;
            var num = color + 1;
            color = num;
        }
    }

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        var mountingFace = GetMountingFace(Terrain.ExtractData(value));
        return face != CellFace.OppositeFace(mountingFace);
    }

    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < 8)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var data = SetMountingFace(Terrain.ExtractData(value), raycastResult.CellFace.Face);
        var value2 = Terrain.ReplaceData(value, data);
        BlockPlacementData result = default;
        result.Value = value2;
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
        return mountingFace >= CollisionBoxesByFace.Length ? [] : CollisionBoxesByFace[mountingFace];
    }


    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new FourLedElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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

    public static int GetColor(int data)
    {
        return (data >> 3) & 7;
    }

    public static int SetColor(int data, int color)
    {
        return (data & -57) | ((color & 7) << 3);
    }

    public static int GetMountingFace(int data)
    {
        return data & 7;
    }

    public static int SetMountingFace(int data, int face)
    {
        return (data & -8) | (face & 7);
    }
}

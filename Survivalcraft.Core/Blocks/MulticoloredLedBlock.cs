using Game.Blocks.Collision;

namespace Game.Blocks;

public class MulticoloredLedBlock : MountedElectricElementBlock
{
    public BoundingBox[][] CollisionBoxesByData = new BoundingBox[6][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Leds.glb");

    public override void Initialize()
    {
        var blockPositionsByData = new CollisionGeometryBuilder[6];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var ledNode = model.GetNode("Led")!;
        var ledBulbNode = model.GetNode("LedBulb")!;
        var nodeAbsoluteTransform = ledNode.AbsoluteTransform;
        var nodeAbsoluteTransform2 = ledBulbNode.AbsoluteTransform;
        for (var i = 0; i < 6; i++)
        {
            var num = SetMountingFace(0, i);
            var m2 = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            blockPositionsByData[num] = new CollisionGeometryBuilder();
            blockPositionsByData[num].Append(ledNode.Primitives[0], nodeAbsoluteTransform * m2);
            blockPositionsByData[num].Append(ledBulbNode.Primitives[0], nodeAbsoluteTransform2 * m2);
            CollisionBoxesByData[num] = new BoundingBox[1]
            {
                blockPositionsByData[num].GetBounds()
            };
        }
    }


    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        var craftingRecipe = new CraftingRecipe
        {
            ResultCount = 4,
            ResultValue = Terrain.MakeBlockValue(BlockIndex, 0),
            RequiredHeatLevel = 0f,
            Description = new LocalizedText(GetType().Name, "1", string.Empty),
            Ingredients =
            {
                [1] = "glass",
                [4] = "wire",
                [6] = "copperingot",
                [7] = "copperingot",
                [8] = "copperingot"
            }
        };
        yield return craftingRecipe;
    }

    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0);
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

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num >= CollisionBoxesByData.Length ? [] : CollisionBoxesByData[num];
    }


    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new MulticoloredLedElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
}

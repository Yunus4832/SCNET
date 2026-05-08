using System.Globalization;
using Engine.Graphics;

namespace Game.Blocks;

public class LedBlock : MountedElectricElementBlock
{
    public const int Index = 152;

    public static readonly Color[] LedColors =
    [
        new(255, 255, 255),
        new(0, 255, 255),
        new(255, 0, 0),
        new(0, 0, 255),
        new(255, 240, 0),
        new(0, 255, 0),
        new(255, 120, 0),
        new(255, 0, 255)
    ];

    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[64];

    public readonly BoundingBox[][] CollisionBoxesByData = new BoundingBox[64][];

    public readonly BlockMesh[] StandaloneBlockMeshesByColor = new BlockMesh[8];

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Leds");
        var ledMesh = model.FindMesh("Led")!;
        var ledBulbMesh = model.FindMesh("LedBulb")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            ledMesh.ParentBone ??
            throw new InvalidOperationException("Required LedMesh.ParentBone is null")
        );
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            ledBulbMesh.ParentBone ??
            throw new InvalidOperationException("Required LedBulbMesh.ParentBone is null")
        );
        for (var i = 0; i < 8; i++)
        {
            var color = LedColors[i];
            color *= 0.5f;
            color.A = byte.MaxValue;
            var m = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
            StandaloneBlockMeshesByColor[i] = new BlockMesh();
            StandaloneBlockMeshesByColor[i].AppendModelMeshPart(ledMesh.MeshParts[0], boneAbsoluteTransform * m,
                false, false, false, false, Color.White);
            StandaloneBlockMeshesByColor[i].AppendModelMeshPart(ledBulbMesh.MeshParts[0], boneAbsoluteTransform2 * m,
                false, false, false, false, color);
            for (var j = 0; j < 6; j++)
            {
                var num = SetMountingFace(SetColor(0, i), j);
                var m2 = j >= 4
                    ? j != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(j * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                BlockMeshesByData[num] = new BlockMesh();
                BlockMeshesByData[num].AppendModelMeshPart(ledMesh.MeshParts[0], boneAbsoluteTransform * m2, false,
                    false, false, false, Color.White);
                BlockMeshesByData[num].AppendModelMeshPart(ledBulbMesh.MeshParts[0], boneAbsoluteTransform2 * m2,
                    false, false, false, false, color);
                CollisionBoxesByData[num] = [BlockMeshesByData[num].CalculateBoundingBox()];
            }
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
                ResultValue = Terrain.MakeBlockValue(152, 0, SetColor(0, color)),
                RemainsCount = 1,
                RemainsValue = Terrain.MakeBlockValue(90),
                RequiredHeatLevel = 0f,
                Description = LanguageControl.Get(GetType().Name, 1)
            };
            craftingRecipe.Ingredients[1] = "glass";
            craftingRecipe.Ingredients[4] = "paintbucket:" + color.ToString(CultureInfo.InvariantCulture);
            craftingRecipe.Ingredients[6] = "copperingot";
            craftingRecipe.Ingredients[7] = "copperingot";
            craftingRecipe.Ingredients[8] = "copperingot";
            yield return craftingRecipe;
            var num = color + 1;
            color = num;
        }
    }

    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var color = GetColor(data);
        return LanguageControl.Get("LedBlock", color) +
               LanguageControl.GetBlock($"{GetType().Name}:{data.ToString()}", "DisplayName");
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < 8)
        {
            yield return Terrain.MakeBlockValue(152, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
    }

    public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
    {
        var data = SetMountingFace(Terrain.ExtractData(value), raycastResult.CellFace.Face);
        var value2 = Terrain.ReplaceData(value, data);
        BlockPlacementData result = default;
        result.Value = value2;
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel,
        List<BlockDropValue> dropValues, out bool showDebris)
    {
        var color = GetColor(Terrain.ExtractData(oldValue));
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(152, 0, SetColor(0, color)),
            Count = 1
        });
        showDebris = true;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num >= CollisionBoxesByData.Length ? [] : CollisionBoxesByData[num];
    }

    public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value,
        int x, int y, int z)
    {
        var num = Terrain.ExtractData(value);
        if (num >= BlockMeshesByData.Length)
        {
            return;
        }

        generator.GenerateMeshVertices(this, x, y, z, BlockMeshesByData[num], Color.White, null,
            geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, GetFace(value), 0.25f, Vector2.Zero, geometry.SubsetOpaque);
    }

    public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size,
        ref Matrix matrix, DrawBlockEnvironmentData environmentData)
    {
        var color2 = GetColor(Terrain.ExtractData(value));
        BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMeshesByColor[color2], color, 2f * size,
            ref matrix, environmentData);
    }

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new LedElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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

    public static int GetColor(int data)
    {
        return (data >> 3) & 7;
    }

    public static int SetColor(int data, int color)
    {
        return (data & -57) | ((color & 7) << 3);
    }
}

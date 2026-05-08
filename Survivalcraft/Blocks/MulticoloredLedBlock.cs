using Engine.Graphics;

namespace Game.Blocks;

public class MulticoloredLedBlock : MountedElectricElementBlock
{
    public const int Index = 254;

    public BlockMesh[] BlockMeshesByData = new BlockMesh[6];

    public BoundingBox[][] CollisionBoxesByData = new BoundingBox[6][];

    public BlockMesh StandaloneBlockMesh = new();

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
            throw new InvalidOperationException("Required BulbMesh.ParentBone is null")
        );
        var m = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh = new BlockMesh();
        StandaloneBlockMesh.AppendModelMeshPart(ledMesh.MeshParts[0], boneAbsoluteTransform * m, false, false,
            false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(ledBulbMesh.MeshParts[0], boneAbsoluteTransform2 * m, false, false,
            false, false, new Color(48, 48, 48));
        for (var i = 0; i < 6; i++)
        {
            var num = SetMountingFace(0, i);
            var m2 = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            BlockMeshesByData[num] = new BlockMesh();
            BlockMeshesByData[num].AppendModelMeshPart(ledMesh.MeshParts[0], boneAbsoluteTransform * m2, false,
                false, false, false, Color.White);
            BlockMeshesByData[num].AppendModelMeshPart(ledBulbMesh.MeshParts[0], boneAbsoluteTransform2 * m2, false,
                false, false, false, new Color(48, 48, 48));
            CollisionBoxesByData[num] = new BoundingBox[1]
            {
                BlockMeshesByData[num].CalculateBoundingBox()
            };
        }
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        var craftingRecipe = new CraftingRecipe
        {
            ResultCount = 4,
            ResultValue = Terrain.MakeBlockValue(254, 0, 0),
            RequiredHeatLevel = 0f,
            Description = LanguageControl.Get(GetType().Name, 1),
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
        yield return Terrain.MakeBlockValue(254, 0, 0);
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

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num >= CollisionBoxesByData.Length ? [] : CollisionBoxesByData[num];
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
        var num = Terrain.ExtractData(value);
        if (num >= BlockMeshesByData.Length)
        {
            return;
        }

        generator.GenerateMeshVertices(this, x, y, z, BlockMeshesByData[num], Color.White, null,
            geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, GetFace(value), 0.25f, Vector2.Zero, geometry.SubsetOpaque);
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
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            2f * size,
            ref matrix,
            environmentData
        );
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

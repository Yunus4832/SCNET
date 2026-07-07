using Engine.Graphics;

namespace Game.Blocks;

public class OneLedBlock : MountedElectricElementBlock
{
    public const int Index = 253;

    public readonly BlockMesh[] BlockMeshesByFace = new BlockMesh[6];

    public readonly BoundingBox[][] CollisionBoxesByFace = new BoundingBox[6][];

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var ledMesh = ContentManager.Get<Model>("Models/Leds").FindMesh("OneLed")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            ledMesh.ParentBone ??
            throw new InvalidOperationException("Required LedMesh.ParentBone is null")
        );
        for (var i = 0; i < 6; i++)
        {
            var m = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            BlockMeshesByFace[i] = new BlockMesh();
            BlockMeshesByFace[i].AppendModelMeshPart(ledMesh.MeshParts[0], boneAbsoluteTransform * m, false, false,
                false, false, Color.White);
            CollisionBoxesByFace[i] = [BlockMeshesByFace[i].CalculateBoundingBox()];
        }

        var m2 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh = new BlockMesh();
        StandaloneBlockMesh.AppendModelMeshPart(ledMesh.MeshParts[0], boneAbsoluteTransform * m2, false, false,
            false, false, Color.White);
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        var craftingRecipe = new CraftingRecipe
        {
            ResultCount = 4,
            ResultValue = Terrain.MakeBlockValue(253, 0, 0),
            RequiredHeatLevel = 0f,
            Description = LanguageManager.Get(GetType().Name, 1),
            Ingredients =
            {
                [0] = "glass",
                [1] = "glass",
                [2] = "glass",
                [4] = "wire",
                [6] = "copperingot",
                [7] = "copperingot",
                [8] = "copperingot"
            }
        };
        yield return craftingRecipe;
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

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(253, 0, 0),
            Count = 1
        });
        showDebris = true;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var mountingFace = GetMountingFace(Terrain.ExtractData(value));
        return mountingFace >= CollisionBoxesByFace.Length ? [] : CollisionBoxesByFace[mountingFace];
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
        var mountingFace = GetMountingFace(Terrain.ExtractData(value));
        if (mountingFace >= BlockMeshesByFace.Length)
        {
            return;
        }

        generator.GenerateMeshVertices(this, x, y, z, BlockMeshesByFace[mountingFace], Color.White, null,
            geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, mountingFace, 1f, Vector2.Zero, geometry.SubsetOpaque);
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
        BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMesh, color, 2f * size, ref matrix,
            environmentData);
    }

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new OneLedElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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

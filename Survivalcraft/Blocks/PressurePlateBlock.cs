using Engine.Graphics;

namespace Game.Blocks;

public class PressurePlateBlock : MountedElectricElementBlock
{
    public const int Index = 144;

    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[16];

    public readonly BoundingBox[][] CollisionBoxesByData = new BoundingBox[16][];

    public readonly int[] CreativeValuesByMaterial =
    [
        Terrain.MakeBlockValue(144, 0, 0),
        Terrain.MakeBlockValue(144, 0, 1)
    ];

    public readonly string[] DisplayNamesByMaterial =
    [
        "木质压力板",
        "石质压力板"
    ];

    public readonly BlockMesh[] StandaloneBlockMeshesByMaterial = new BlockMesh[2];

    public readonly int[] TextureSlotsByMaterial =
    [
        4,
        1
    ];

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/PressurePlate");
        for (var i = 0; i < 2; i++)
        {
            var pressurePlateMesh = model.FindMesh("PressurePlate")!;
            var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
                pressurePlateMesh.ParentBone ??
                throw new InvalidOperationException("Required PressurePlateMesh.ParentBone is null")
            );
            var num = TextureSlotsByMaterial[i];
            for (var j = 0; j < 6; j++)
            {
                var num2 = SetMountingFace(SetMaterial(0, i), j);
                var matrix = j >= 4
                    ? j != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(j * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                BlockMeshesByData[num2] = new BlockMesh();
                BlockMeshesByData[num2].AppendModelMeshPart(pressurePlateMesh.MeshParts[0],
                    boneAbsoluteTransform * matrix, false, false, false, false, Color.White);
                BlockMeshesByData[num2]
                    .TransformTextureCoordinates(Matrix.CreateTranslation(num % 16 / 16f, num / 16 / 16f, 0f));
                BlockMeshesByData[num2].GenerateSidesData();
                var vector = Vector3.Transform(new Vector3(-0.5f, 0f, -0.5f), matrix);
                var vector2 = Vector3.Transform(new Vector3(0.5f, 0.0625f, 0.5f), matrix);
                vector.X = MathUtils.Round(vector.X * 100f) / 100f;
                vector.Y = MathUtils.Round(vector.Y * 100f) / 100f;
                vector.Z = MathUtils.Round(vector.Z * 100f) / 100f;
                vector2.X = MathUtils.Round(vector2.X * 100f) / 100f;
                vector2.Y = MathUtils.Round(vector2.Y * 100f) / 100f;
                vector2.Z = MathUtils.Round(vector2.Z * 100f) / 100f;
                CollisionBoxesByData[num2] =
                [
                    new BoundingBox(
                        new Vector3(MathUtils.Min(vector.X, vector2.X), MathUtils.Min(vector.Y, vector2.Y),
                            MathUtils.Min(vector.Z, vector2.Z)),
                        new Vector3(MathUtils.Max(vector.X, vector2.X), MathUtils.Max(vector.Y, vector2.Y),
                            MathUtils.Max(vector.Z, vector2.Z)))
                ];
            }

            var identity = Matrix.Identity;
            StandaloneBlockMeshesByMaterial[i] = new BlockMesh();
            StandaloneBlockMeshesByMaterial[i].AppendModelMeshPart(pressurePlateMesh.MeshParts[0],
                boneAbsoluteTransform * identity, false, false, false, false, Color.White);
            StandaloneBlockMeshesByMaterial[i]
                .TransformTextureCoordinates(Matrix.CreateTranslation(num % 16 / 16f, num / 16 / 16f, 0f));
        }
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var material = GetMaterial(Terrain.ExtractData(value));
        return DisplayNamesByMaterial[material];
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        return CreativeValuesByMaterial;
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var material = GetMaterial(Terrain.ExtractData(value));
        return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, Color.White,
            TextureSlotsByMaterial[material]);
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
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
        var material = GetMaterial(Terrain.ExtractData(oldValue));
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(144, 0, SetMaterial(0, material)),
            Count = 1
        });
        showDebris = true;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num >= CollisionBoxesByData.Length ? [] : CollisionBoxesByData[num];
    }

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        return face != CellFace.OppositeFace(GetFace(value));
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
        if (num >= BlockMeshesByData.Length || BlockMeshesByData[num] == null)
        {
            return;
        }

        generator.GenerateMeshVertices(this, x, y, z, BlockMeshesByData[num], Color.White, null,
            geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, GetFace(value), 0.8125f, Vector2.Zero,
            geometry.SubsetOpaque);
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
        var material = GetMaterial(Terrain.ExtractData(value));
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMeshesByMaterial[material],
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
        return new PressurePlateElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
            return ElectricConnectorType.Output;
        }

        return null;
    }

    public static int GetMaterial(int data)
    {
        return data & 1;
    }

    public static int SetMaterial(int data, int material)
    {
        return (data & -2) | (material & 1);
    }

    public static int GetMountingFace(int data)
    {
        return (data >> 1) & 7;
    }

    public static int SetMountingFace(int data, int face)
    {
        return (data & -15) | ((face & 7) << 1);
    }

    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }
}

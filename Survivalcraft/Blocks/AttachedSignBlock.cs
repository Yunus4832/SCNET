using Engine.Graphics;

namespace Game.Blocks;

public abstract class AttachedSignBlock(
    string modelName,
    int coloredTextureSlot,
    int postedSignBlockIndex
) : SignBlock, IElectricElementBlock, IPaintableBlock
{
    public readonly BlockMesh[] BlockMeshes = new BlockMesh[4];

    public readonly BoundingBox[][] CollisionBoxes = new BoundingBox[4][];

    public readonly BlockMesh[] ColoredBlockMeshes = new BlockMesh[4];

    public int ColoredTextureSlot = coloredTextureSlot;

    public string ModelName = modelName;

    public int PostedSignBlockIndex = postedSignBlockIndex;

    public readonly BlockMesh StandaloneBlockMesh = new();

    public readonly BlockMesh StandaloneColoredBlockMesh = new();

    public readonly BlockMesh[] SurfaceMeshes = new BlockMesh[4];

    public readonly Vector3[] SurfaceNormals = new Vector3[4];

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        return new SignElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(data)));
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
        var data = Terrain.ExtractData(value);
        if (face != GetFace(data) ||
            !SubsystemElectricity.GetConnectorDirection(face, 0, connectorFace).HasValue)
        {
            return null;
        }

        return ElectricConnectorType.Input;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        var data = Terrain.ExtractData(value);
        return Terrain.ReplaceData(value, SetColor(data, color));
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var signMesh = model.FindMesh("Sign")!;
        var surfaceMesh = model.FindMesh("Surface")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            signMesh.ParentBone ??
            throw new InvalidOperationException("Required SignMesh.ParentBone is null")
        );
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            surfaceMesh.ParentBone ??
            throw new InvalidOperationException("Required SurfaceMesh.ParentBone is null")
        );
        for (var i = 0; i < 4; i++)
        {
            var radians = (float)Math.PI / 2f * i;
            var m = Matrix.CreateTranslation(0f, 0f, -15f / 32f) * Matrix.CreateRotationY(radians) *
                    Matrix.CreateTranslation(0.5f, -0.3125f, 0.5f);
            var blockMesh = new BlockMesh();
            blockMesh.AppendModelMeshPart(signMesh.MeshParts[0], boneAbsoluteTransform * m, false, false,
                false, false, Color.White);
            BlockMeshes[i] = new BlockMesh();
            BlockMeshes[i].AppendBlockMesh(blockMesh);
            ColoredBlockMeshes[i] = new BlockMesh();
            ColoredBlockMeshes[i].AppendBlockMesh(BlockMeshes[i]);
            BlockMeshes[i].TransformTextureCoordinates(Matrix.CreateTranslation(TextureSlot % 16 / 16f,
                TextureSlot / 16 / 16f, 0f));
            ColoredBlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
            CollisionBoxes[i] = new BoundingBox[1];
            CollisionBoxes[i][0] = blockMesh.CalculateBoundingBox();
            SurfaceMeshes[i] = new BlockMesh();
            SurfaceMeshes[i].AppendModelMeshPart(surfaceMesh.MeshParts[0], boneAbsoluteTransform2 * m,
                false, false, false, false, Color.White);
            SurfaceNormals[i] = -m.Forward;
        }

        StandaloneBlockMesh.AppendModelMeshPart(signMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.6f, 0f), false, false, false, false, Color.White);
        StandaloneColoredBlockMesh.AppendBlockMesh(StandaloneBlockMesh);
        StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(TextureSlot % 16 / 16f,
            TextureSlot / 16 / 16f, 0f));
        StandaloneColoredBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
        base.Initialize();
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
        showDebris = true;
        var color = GetColor(Terrain.ExtractData(oldValue));
        var data = PostedSignBlock.SetColor(0, color);
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(PostedSignBlockIndex, 0, data),
            Count = 1
        });
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var color = GetColor(Terrain.ExtractData(value));
        if (color.HasValue)
        {
            return new BlockDebrisParticleSystem(
                subsystemTerrain,
                position,
                strength,
                DestructionDebrisScale,
                SubsystemPalette.GetColor(subsystemTerrain, color), ColoredTextureSlot
            );
        }

        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            Color.White,
            TextureSlot
        );
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
        var data = Terrain.ExtractData(value);
        var face = GetFace(data);
        var color = GetColor(data);
        if (color.HasValue)
        {
            generator.GenerateMeshVertices(this, x, y, z, ColoredBlockMeshes[face],
                SubsystemPalette.GetColor(generator, color), null, geometry.SubsetOpaque);
        }
        else
        {
            generator.GenerateMeshVertices(this, x, y, z, BlockMeshes[face], Color.White, null,
                geometry.SubsetOpaque);
        }

        generator.GenerateWireVertices(value, x, y, z, GetFace(data), 0.375f, Vector2.Zero, geometry.SubsetOpaque);
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
        var color2 = GetColor(Terrain.ExtractData(value));
        if (color2.HasValue)
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneColoredBlockMesh,
                color * SubsystemPalette.GetColor(environmentData, color2),
                1.25f * size,
                ref matrix,
                environmentData
            );
            return;
        }

        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            1.25f * size,
            ref matrix,
            environmentData
        );
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var face = GetFace(Terrain.ExtractData(value));
        return CollisionBoxes[face];
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        return default;
    }

    public override BlockMesh GetSignSurfaceBlockMesh(int data) => SurfaceMeshes[GetFace(data)];

    public override Vector3 GetSignSurfaceNormal(int data) => SurfaceNormals[GetFace(data)];

    public static int GetFace(int data) => data & 3;

    public static int SetFace(int data, int face) => (data & -4) | (face & 3);

    public static int? GetColor(int data)
    {
        if ((data & 4) != 0)
        {
            return (data >> 3) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -125) | 4 | ((color.Value & 0xF) << 3);
        }

        return data & -125;
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = true;
        var data = Terrain.ExtractData(value);
        return pistonFace == GetFace(data);
    }
}

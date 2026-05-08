using Engine.Graphics;

namespace Game.Blocks;

public abstract class PostedSignBlock(
    string modelName,
    int coloredTextureSlot,
    int attachedSignBlockIndex
) : SignBlock, IElectricElementBlock, IPaintableBlock
{
    public int AttachedSignBlockIndex = attachedSignBlockIndex;

    public BlockMesh[] BlockMeshes = new BlockMesh[16];

    public BoundingBox[][] CollisionBoxes = new BoundingBox[16][];

    public BlockMesh[] ColoredBlockMeshes = new BlockMesh[16];

    public int ColoredTextureSlot = coloredTextureSlot;

    public Vector3[] Directions = new Vector3[16];

    public string ModelName = modelName;

    public BlockMesh StandaloneBlockMesh = new();

    public BlockMesh StandaloneColoredBlockMesh = new();

    public BlockMesh[] SurfaceMeshes = new BlockMesh[16];

    public Vector3[] SurfaceNormals = new Vector3[16];

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        return new SignElectricElement(subsystemElectricity, new CellFace(x, y, z, GetHanging(data) ? 5 : 4));
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
        if (GetHanging(Terrain.ExtractData(value)))
        {
            if (face != 5 || !SubsystemElectricity.GetConnectorDirection(face, 0, connectorFace).HasValue)
            {
                return null;
            }

            return ElectricConnectorType.Input;
        }

        if (face != 4 || !SubsystemElectricity.GetConnectorDirection(face, 0, connectorFace).HasValue)
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
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            signMesh.ParentBone ??
            throw new InvalidOperationException("Required SignMesh.ParentBone is null")
        );
        var postMesh = model.FindMesh("Post")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            postMesh.ParentBone ??
            throw new InvalidOperationException("Required PostMesh.ParentBone is null")
        );
        var surfaceMesh = model.FindMesh("Surface")!;
        var boneAbsoluteTransform3 = BlockMesh.GetBoneAbsoluteTransform(
            surfaceMesh.ParentBone ??
            throw new InvalidOperationException("Required SurfaceMesh.ParentBone is null")
        );
        for (var i = 0; i < 16; i++)
        {
            var hanging = GetHanging(i);
            var m = Matrix.CreateRotationY(GetDirection(i) * (float)Math.PI / 4f) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            if (hanging)
            {
                m *= Matrix.CreateScale(1f, -1f, 1f) * Matrix.CreateTranslation(0f, 1f, 0f);
            }

            Directions[i] = m.Forward;
            var blockMesh = new BlockMesh();
            blockMesh.AppendModelMeshPart(signMesh.MeshParts[0], boneAbsoluteTransform * m, false,
                hanging, false, false, Color.White);
            var blockMesh2 = new BlockMesh();
            blockMesh2.AppendModelMeshPart(postMesh.MeshParts[0], boneAbsoluteTransform2 * m, false,
                hanging, false, false, Color.White);
            BlockMeshes[i] = new BlockMesh();
            BlockMeshes[i].AppendBlockMesh(blockMesh);
            BlockMeshes[i].AppendBlockMesh(blockMesh2);
            ColoredBlockMeshes[i] = new BlockMesh();
            ColoredBlockMeshes[i].AppendBlockMesh(BlockMeshes[i]);
            BlockMeshes[i].TransformTextureCoordinates(Matrix.CreateTranslation(TextureSlot % 16 / 16f,
                TextureSlot / 16 / 16f, 0f));
            ColoredBlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
            CollisionBoxes[i] = new BoundingBox[2];
            CollisionBoxes[i][0] = blockMesh.CalculateBoundingBox();
            CollisionBoxes[i][1] = blockMesh2.CalculateBoundingBox();
            SurfaceMeshes[i] = new BlockMesh();
            SurfaceMeshes[i].AppendModelMeshPart(surfaceMesh.MeshParts[0], boneAbsoluteTransform3 * m,
                false, hanging, false, false, Color.White);
            SurfaceNormals[i] = -m.Forward;
            if (hanging)
            {
                for (var j = 0; j < SurfaceMeshes[i].Vertices.Count; j++)
                {
                    var textureCoordinates = SurfaceMeshes[i].Vertices.Array[j].TextureCoordinates;
                    textureCoordinates.Y = 1f - textureCoordinates.Y;
                    SurfaceMeshes[i].Vertices.Array[j].TextureCoordinates = textureCoordinates;
                }
            }
        }

        StandaloneBlockMesh.AppendModelMeshPart(signMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.6f, 0f), false, false, false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(postMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.6f, 0f), false, false, false, false, Color.White);
        StandaloneColoredBlockMesh.AppendBlockMesh(StandaloneBlockMesh);
        StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(TextureSlot % 16 / 16f,
            TextureSlot / 16 / 16f, 0f));
        StandaloneColoredBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
        base.Initialize();
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var color = GetColor(Terrain.ExtractData(value));
        return SubsystemPalette.GetName(color, base.GetDisplayName(subsystemTerrain, value));
    }

    public override string GetCategory(int value)
    {
        if (!GetColor(Terrain.ExtractData(value)).HasValue)
        {
            return base.GetCategory(value);
        }

        return "Painted";
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, null));
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
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
        var data = SetColor(0, color);
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(BlockIndex, 0, data),
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
            return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale,
                SubsystemPalette.GetColor(subsystemTerrain, color), ColoredTextureSlot);
        }

        return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, Color.White,
            TextureSlot);
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
        var variant = GetVariant(data);
        var color = GetColor(data);
        if (color.HasValue)
        {
            generator.GenerateMeshVertices(this, x, y, z, ColoredBlockMeshes[variant],
                SubsystemPalette.GetColor(generator, color), null, geometry.SubsetOpaque);
        }
        else
        {
            generator.GenerateMeshVertices(this, x, y, z, BlockMeshes[variant], Color.White, null,
                geometry.SubsetOpaque);
        }

        generator.GenerateWireVertices(value, x, y, z, GetHanging(data) ? 5 : 4, 0.01f, Vector2.Zero,
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
        var color2 = GetColor(Terrain.ExtractData(value));
        if (color2.HasValue)
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneColoredBlockMesh,
                color * SubsystemPalette.GetColor(environmentData, color2), 1.25f * size, ref matrix, environmentData);
        }
        else
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMesh, color, 1.25f * size, ref matrix,
                environmentData);
        }
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var variant = GetVariant(Terrain.ExtractData(value));
        return CollisionBoxes[variant];
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var color = GetColor(Terrain.ExtractData(value));
        BlockPlacementData result;
        if (raycastResult.CellFace.Face < 4)
        {
            var data = AttachedSignBlock.SetFace(AttachedSignBlock.SetColor(0, color), raycastResult.CellFace.Face);
            result = default;
            result.Value = Terrain.MakeBlockValue(AttachedSignBlockIndex, 0, data);
            result.CellFace = raycastResult.CellFace;
            return result;
        }

        var forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation)
            .Forward;
        var num = float.MinValue;
        var direction = 0;
        for (var i = 0; i < 8; i++)
        {
            var num2 = Vector3.Dot(forward, Directions[i]);
            if (num2 > num)
            {
                num = num2;
                direction = i;
            }
        }

        var data2 = SetHanging(SetDirection(SetColor(0, color), direction), raycastResult.CellFace.Face == 5);
        result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, data2);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BlockMesh GetSignSurfaceBlockMesh(int data)
    {
        return SurfaceMeshes[GetVariant(data)];
    }

    public override Vector3 GetSignSurfaceNormal(int data)
    {
        return SurfaceNormals[GetVariant(data)];
    }

    public static int GetDirection(int data)
    {
        return data & 7;
    }

    public static int SetDirection(int data, int direction)
    {
        return (data & -8) | (direction & 7);
    }

    public static bool GetHanging(int data)
    {
        return (data & 8) != 0;
    }

    public static int SetHanging(int data, bool hanging)
    {
        if (!hanging)
        {
            return data & -9;
        }

        return data | 8;
    }

    public static int GetVariant(int data)
    {
        return data & 0xF;
    }

    public static int SetVariant(int data, int variant)
    {
        return (data & -16) | (variant & 0xF);
    }

    public static int? GetColor(int data)
    {
        if ((data & 0x10) != 0)
        {
            return (data >> 5) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -497) | 0x10 | ((color.Value & 0xF) << 5);
        }

        return data & -497;
    }
}

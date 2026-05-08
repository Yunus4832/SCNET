using Engine.Graphics;

namespace Game.Blocks;

public abstract class FenceBlock(
    string modelName,
    bool doubleSidedPlanks,
    bool useAlphaTest,
    int coloredTextureSlot,
    Color postColor,
    Color unpaintedColor
) : Block, IPaintableBlock
{
    public BlockMesh[] BlockMeshes = new BlockMesh[16];

    public BoundingBox[][] CollisionBoxes = new BoundingBox[16][];

    public BlockMesh[] ColoredBlockMeshes = new BlockMesh[16];

    public int ColoredTextureSlot = coloredTextureSlot;

    public bool DoubleSidedPlanks = doubleSidedPlanks;

    public string ModelName = modelName;

    public Color PostColor = postColor;

    public BlockMesh StandaloneBlockMesh = new();

    public BlockMesh StandaloneColoredBlockMesh = new();

    public Color UnpaintedColor = unpaintedColor;

    public bool UseAlphaTest = useAlphaTest;

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
        var postMesh = model.FindMesh("Post")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            postMesh.ParentBone ??
            throw new InvalidOperationException("Required PostMesh.ParentBone is null")
        );
        var planksMesh = model.FindMesh("Planks")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            planksMesh.ParentBone ??
            throw new InvalidOperationException("Required PlanksMesh.ParentBone is null")
        );
        for (var i = 0; i < 16; i++)
        {
            var num = (i & 1) != 0;
            var flag = (i & 2) != 0;
            var flag2 = (i & 4) != 0;
            var flag3 = (i & 8) != 0;
            var list = new List<BoundingBox>();
            var m = Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            var blockMesh = new BlockMesh();
            blockMesh.AppendModelMeshPart(postMesh.MeshParts[0], boneAbsoluteTransform * m, false, false,
                false, false, Color.White);
            var item = blockMesh.CalculateBoundingBox();
            var min = item.Min;
            var max = item.Max;

            min.X -= 0.1f;
            min.Z -= 0.1f;
            max.X += 0.1f;
            max.Z += 0.1f;

            item.Min = min;
            item.Max = max;
            list.Add(item);
            var blockMesh2 = new BlockMesh();
            if (num)
            {
                var blockMesh3 = new BlockMesh();
                var m2 = Matrix.CreateRotationY(0f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh3.AppendModelMeshPart(planksMesh.MeshParts[0], boneAbsoluteTransform2 * m2,
                    false, false, false, false, Color.White);
                if (DoubleSidedPlanks)
                {
                    blockMesh3.AppendModelMeshPart(planksMesh.MeshParts[0], boneAbsoluteTransform2 * m2,
                        false, true, false, true, Color.White);
                }

                blockMesh2.AppendBlockMesh(blockMesh3);
                var item2 = blockMesh3.CalculateBoundingBox();
                list.Add(item2);
            }

            if (flag)
            {
                var blockMesh4 = new BlockMesh();
                var m3 = Matrix.CreateRotationY((float)Math.PI) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh4.AppendModelMeshPart(planksMesh.MeshParts[0], boneAbsoluteTransform2 * m3,
                    false, false, false, false, Color.White);
                if (DoubleSidedPlanks)
                {
                    blockMesh4.AppendModelMeshPart(planksMesh.MeshParts[0], boneAbsoluteTransform2 * m3,
                        false, true, false, true, Color.White);
                }

                blockMesh2.AppendBlockMesh(blockMesh4);
                var item3 = blockMesh4.CalculateBoundingBox();
                list.Add(item3);
            }

            if (flag2)
            {
                var blockMesh5 = new BlockMesh();
                var m4 = Matrix.CreateRotationY(4.712389f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh5.AppendModelMeshPart(planksMesh.MeshParts[0], boneAbsoluteTransform2 * m4,
                    false, false, false, false, Color.White);
                if (DoubleSidedPlanks)
                {
                    blockMesh5.AppendModelMeshPart(planksMesh.MeshParts[0], boneAbsoluteTransform2 * m4,
                        false, true, false, true, Color.White);
                }

                blockMesh2.AppendBlockMesh(blockMesh5);
                var item4 = blockMesh5.CalculateBoundingBox();
                list.Add(item4);
            }

            if (flag3)
            {
                var blockMesh6 = new BlockMesh();
                var m5 = Matrix.CreateRotationY((float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh6.AppendModelMeshPart(planksMesh.MeshParts[0], boneAbsoluteTransform2 * m5,
                    false, false, false, false, Color.White);
                if (DoubleSidedPlanks)
                {
                    blockMesh6.AppendModelMeshPart(planksMesh.MeshParts[0], boneAbsoluteTransform2 * m5,
                        false, true, false, true, Color.White);
                }

                blockMesh2.AppendBlockMesh(blockMesh6);
                var item5 = blockMesh6.CalculateBoundingBox();
                list.Add(item5);
            }

            blockMesh.ModulateColor(PostColor);
            BlockMeshes[i] = new BlockMesh();
            BlockMeshes[i].AppendBlockMesh(blockMesh);
            BlockMeshes[i].AppendBlockMesh(blockMesh2);
            BlockMeshes[i].TransformTextureCoordinates(Matrix.CreateTranslation(TextureSlot % 16 / 16f,
                TextureSlot / 16 / 16f, 0f));
            BlockMeshes[i].GenerateSidesData();
            ColoredBlockMeshes[i] = new BlockMesh();
            ColoredBlockMeshes[i].AppendBlockMesh(blockMesh);
            ColoredBlockMeshes[i].AppendBlockMesh(blockMesh2);
            ColoredBlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
            ColoredBlockMeshes[i].GenerateSidesData();
            CollisionBoxes[i] = list.ToArray();
        }

        StandaloneBlockMesh.AppendModelMeshPart(postMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(-0.5f, -0.5f, 0f), false, false, false, false,
            Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(postMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0.5f, -0.5f, 0f), false, false, false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(planksMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateRotationY(0f) * Matrix.CreateTranslation(-0.5f, -0.5f, 0f), false,
            false, false, false, Color.White);
        if (DoubleSidedPlanks)
        {
            StandaloneBlockMesh.AppendModelMeshPart(planksMesh.MeshParts[0],
                boneAbsoluteTransform2 * Matrix.CreateRotationY(0f) * Matrix.CreateTranslation(-0.5f, -0.5f, 0f), false,
                true, false, true, Color.White);
        }

        StandaloneBlockMesh.AppendModelMeshPart(planksMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateRotationY((float)Math.PI) * Matrix.CreateTranslation(0.5f, -0.5f, 0f),
            false, false, false, false, Color.White);
        if (DoubleSidedPlanks)
        {
            StandaloneBlockMesh.AppendModelMeshPart(planksMesh.MeshParts[0],
                boneAbsoluteTransform2 * Matrix.CreateRotationY((float)Math.PI) *
                Matrix.CreateTranslation(0.5f, -0.5f, 0f), false, true, false, true, Color.White);
        }

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
        var data = SetVariant(Terrain.ExtractData(oldValue), 0);
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
            return new BlockDebrisParticleSystem(
                subsystemTerrain,
                position,
                strength,
                DestructionDebrisScale,
                SubsystemPalette.GetColor(subsystemTerrain, color),
                ColoredTextureSlot
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
        var variant = GetVariant(data);
        var color = GetColor(data);
        if (color.HasValue)
        {
            generator.GenerateMeshVertices(this, x, y, z, ColoredBlockMeshes[variant],
                SubsystemPalette.GetColor(generator, color), null,
                UseAlphaTest ? geometry.SubsetAlphaTest : geometry.SubsetOpaque);
        }
        else
        {
            generator.GenerateMeshVertices(this, x, y, z, BlockMeshes[variant], UnpaintedColor, null,
                UseAlphaTest ? geometry.SubsetAlphaTest : geometry.SubsetOpaque);
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
        var color2 = GetColor(Terrain.ExtractData(value));
        if (color2.HasValue)
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneColoredBlockMesh,
                color * SubsystemPalette.GetColor(environmentData, color2),
                size,
                ref matrix,
                environmentData
            );
            return;
        }

        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color * UnpaintedColor,
            size,
            ref matrix,
            environmentData
        );
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var variant = GetVariant(Terrain.ExtractData(value));
        return CollisionBoxes[variant];
    }

    public virtual bool ShouldConnectTo(int value)
    {
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        if (block is not FenceBlock)
        {
            return block is FenceGateBlock;
        }

        return true;
    }

    public static int GetVariant(int data) => data & 0xF;

    public static int SetVariant(int data, int variant) => (data & -16) | (variant & 0xF);

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

    public override bool IsFaceNonAttachable(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int attachBlockValue
    )
    {
        var block = BlocksManager.Blocks[Terrain.ExtractContents(attachBlockValue)];
        return block is not BasePumpkinBlock &&
               base.IsFaceNonAttachable(subsystemTerrain, face, value, attachBlockValue);
    }
}

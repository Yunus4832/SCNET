using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

namespace Game.Blocks.Presentation;

public abstract class FenceBlockPresentation(
    FenceBlock block,
    BlockPresentationCatalog catalog,
    string modelName,
    bool doubleSidedPlanks,
    bool useAlphaTest,
    int coloredTextureSlot,
    Color postColor,
    Color unpaintedColor
) : BlockPresentation(block, catalog)
{
    protected new FenceBlock Block => (FenceBlock)base.Block;

    public BlockMesh[] BlockMeshes = new BlockMesh[16];

    public BlockMesh[] ColoredBlockMeshes = new BlockMesh[16];

    public int ColoredTextureSlot = coloredTextureSlot;

    public BlockMesh StandaloneBlockMesh = new();

    public BlockMesh StandaloneColoredBlockMesh = new();

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var color = FenceBlock.GetColor(Terrain.ExtractData(value));
        return PalettePresentation.GetName(color, base.GetDisplayName(subsystemTerrain, value));
    }

    public override string GetCategory(int value)
    {
        if (!FenceBlock.GetColor(Terrain.ExtractData(value)).HasValue)
        {
            return base.GetCategory(value);
        }

        return "Painted";
    }

    public override BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        var color = FenceBlock.GetColor(Terrain.ExtractData(value));
        if (color.HasValue)
        {
            return new BlockDebrisParticleSystem(
                subsystemTerrain,
                position,
                strength,
                DestructionDebrisScale,
                PalettePresentation.GetColor(subsystemTerrain, color),
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
        var variant = FenceBlock.GetVariant(data);
        var color = FenceBlock.GetColor(data);
        if (color.HasValue)
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                ColoredBlockMeshes[variant],
                PalettePresentation.GetColor(generator.SubsystemPalette, color),
                null,
                UseAlphaTest ? geometry.SubsetAlphaTest : geometry.SubsetOpaque
            );
        }
        else
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                BlockMeshes[variant],
                UnpaintedColor,
                null,
                UseAlphaTest ? geometry.SubsetAlphaTest : geometry.SubsetOpaque
            );
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
        var color2 = FenceBlock.GetColor(Terrain.ExtractData(value));
        if (color2.HasValue)
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneColoredBlockMesh,
                color * PalettePresentation.GetColor(environmentData.SubsystemTerrain, color2),
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

    public string ModelName = modelName;

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var postNode = model.FindNode("Post")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(postNode);
        var planksNode = model.FindNode("Planks")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(planksNode);
        for (var i = 0; i < 16; i++)
        {
            var num = (i & 1) != 0;
            var flag = (i & 2) != 0;
            var flag2 = (i & 4) != 0;
            var flag3 = (i & 8) != 0;
            var m = Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            var blockMesh = new BlockMesh();
            blockMesh.AppendModelPrimitive(
                postNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * m,
                false,
                false,
                false,
                false,
                Color.White
            );
            var blockMesh2 = new BlockMesh();
            if (num)
            {
                var blockMesh3 = new BlockMesh();
                var m2 = Matrix.CreateRotationY(0f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh3.AppendModelPrimitive(
                    planksNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform2 * m2,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
                if (DoubleSidedPlanks)
                {
                    blockMesh3.AppendModelPrimitive(
                        planksNode.RequireMesh().Primitives[0],
                        nodeAbsoluteTransform2 * m2,
                        false,
                        true,
                        false,
                        true,
                        Color.White
                    );
                }

                blockMesh2.AppendBlockMesh(blockMesh3);
            }

            if (flag)
            {
                var blockMesh4 = new BlockMesh();
                var m3 = Matrix.CreateRotationY((float)Math.PI) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh4.AppendModelPrimitive(
                    planksNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform2 * m3,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
                if (DoubleSidedPlanks)
                {
                    blockMesh4.AppendModelPrimitive(
                        planksNode.RequireMesh().Primitives[0],
                        nodeAbsoluteTransform2 * m3,
                        false,
                        true,
                        false,
                        true,
                        Color.White
                    );
                }

                blockMesh2.AppendBlockMesh(blockMesh4);
            }

            if (flag2)
            {
                var blockMesh5 = new BlockMesh();
                var m4 = Matrix.CreateRotationY(4.712389f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh5.AppendModelPrimitive(
                    planksNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform2 * m4,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
                if (DoubleSidedPlanks)
                {
                    blockMesh5.AppendModelPrimitive(
                        planksNode.RequireMesh().Primitives[0],
                        nodeAbsoluteTransform2 * m4,
                        false,
                        true,
                        false,
                        true,
                        Color.White
                    );
                }

                blockMesh2.AppendBlockMesh(blockMesh5);
            }

            if (flag3)
            {
                var blockMesh6 = new BlockMesh();
                var m5 = Matrix.CreateRotationY((float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh6.AppendModelPrimitive(
                    planksNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform2 * m5,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
                if (DoubleSidedPlanks)
                {
                    blockMesh6.AppendModelPrimitive(
                        planksNode.RequireMesh().Primitives[0],
                        nodeAbsoluteTransform2 * m5,
                        false,
                        true,
                        false,
                        true,
                        Color.White
                    );
                }

                blockMesh2.AppendBlockMesh(blockMesh6);
            }

            BlockMeshes[i] = new BlockMesh();
            BlockMeshes[i].AppendBlockMesh(blockMesh);
            BlockMeshes[i].AppendBlockMesh(blockMesh2);
            BlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(
                    TextureSlot % 16 / 16f,
                    TextureSlot / 16 / 16f,
                    0f
                )
            );
            BlockMeshes[i].GenerateSidesData();
            ColoredBlockMeshes[i] = new BlockMesh();
            ColoredBlockMeshes[i].AppendBlockMesh(blockMesh);
            ColoredBlockMeshes[i].AppendBlockMesh(blockMesh2);
            ColoredBlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f)
            );
            ColoredBlockMeshes[i].GenerateSidesData();
        }

        StandaloneBlockMesh.AppendModelPrimitive(
            postNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(-0.5f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            postNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0.5f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            planksNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateRotationY(0f) * Matrix.CreateTranslation(-0.5f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        if (DoubleSidedPlanks)
        {
            StandaloneBlockMesh.AppendModelPrimitive(
                planksNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * Matrix.CreateRotationY(0f) * Matrix.CreateTranslation(-0.5f, -0.5f, 0f),
                false,
                true,
                false,
                true,
                Color.White
            );
        }

        StandaloneBlockMesh.AppendModelPrimitive(
            planksNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateRotationY((float)Math.PI) * Matrix.CreateTranslation(0.5f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        if (DoubleSidedPlanks)
        {
            StandaloneBlockMesh.AppendModelPrimitive(
                planksNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 *
                Matrix.CreateRotationY((float)Math.PI) *
                Matrix.CreateTranslation(0.5f, -0.5f, 0f),
                false,
                true,
                false,
                true,
                Color.White
            );
        }

        StandaloneColoredBlockMesh.AppendBlockMesh(StandaloneBlockMesh);
        StandaloneBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(
                TextureSlot % 16 / 16f,
                TextureSlot / 16 / 16f,
                0f
            )
        );
        StandaloneColoredBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f)
        );
        base.Initialize();
    }


    public bool DoubleSidedPlanks = doubleSidedPlanks;


    public Color PostColor = postColor;


    public Color UnpaintedColor = unpaintedColor;


    public bool UseAlphaTest = useAlphaTest;
}

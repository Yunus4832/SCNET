using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

using static Game.Blocks.FenceGateBlock;

namespace Game.Blocks.Presentation;

public abstract class FenceGateBlockPresentation(
    FenceGateBlock block,
    BlockPresentationCatalog catalog,
    string modelName,
    bool doubleSided,
    bool useAlphaTest,
    int coloredTextureSlot,
    Color postColor,
    Color unpaintedColor
) : BlockPresentation(block, catalog)
{
    protected new FenceGateBlock Block => (FenceGateBlock)base.Block;

    public BlockMesh[] BlockMeshes = new BlockMesh[16];

    public BlockMesh[] ColoredBlockMeshes = new BlockMesh[16];

    public int ColoredTextureSlot = coloredTextureSlot;

    public BlockMesh StandaloneBlockMesh = new();

    public BlockMesh StandaloneColoredBlockMesh = new();

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var color = GetColor(Terrain.ExtractData(value));
        return PalettePresentation.GetName(color, base.GetDisplayName(subsystemTerrain, value));
    }

    public override string GetCategory(int value)
    {
        return !GetColor(Terrain.ExtractData(value)).HasValue ? base.GetCategory(value) : "Painted";
    }

    public override BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        var color = GetColor(Terrain.ExtractData(value));
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
        var variant = GetVariant(data);
        var color = GetColor(data);
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

        generator.GenerateWireVertices(
            value,
            x,
            y,
            z,
            GetHingeFace(data),
            Block.PivotDistance * 2f,
            Vector2.Zero,
            geometry.SubsetOpaque
        );
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
            var rotation = GetRotation(i);
            var open = GetOpen(i);
            var rightHanded = GetRightHanded(i);
            float num = !rightHanded ? 1 : -1;
            var identity = Matrix.Identity;
            identity *= Matrix.CreateScale(0f - num, 1f, 1f);
            identity *= Matrix.CreateTranslation((0.5f - Block.PivotDistance) * num, 0f, 0f) *
                        Matrix.CreateRotationY(open ? num * (float)Math.PI / 2f : 0f) *
                        Matrix.CreateTranslation((0f - (0.5f - Block.PivotDistance)) * num, 0f, 0f);
            identity *= Matrix.CreateTranslation(0f, 0f, 0f) *
                        Matrix.CreateRotationY(rotation * (float)Math.PI / 2f) *
                        Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            BlockMeshes[i] = new BlockMesh();
            BlockMeshes[i].AppendModelPrimitive(
                postNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * identity,
                false,
                !rightHanded,
                false,
                false,
                PostColor
            );
            BlockMeshes[i].AppendModelPrimitive(
                planksNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * identity,
                false,
                !rightHanded,
                false,
                false,
                Color.White
            );
            if (DoubleSided)
            {
                BlockMeshes[i].AppendModelPrimitive(
                    planksNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform2 * identity,
                    false,
                    rightHanded,
                    false,
                    true,
                    Color.White
                );
            }

            ColoredBlockMeshes[i] = new BlockMesh();
            ColoredBlockMeshes[i].AppendBlockMesh(BlockMeshes[i]);
            BlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(
                    TextureSlot % 16 / 16f,
                    TextureSlot / 16 / 16f,
                    0f
                )
            );
            ColoredBlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f)
            );
        }

        StandaloneBlockMesh.AppendModelPrimitive(
            postNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            PostColor
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            planksNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        if (DoubleSided)
        {
            StandaloneBlockMesh.AppendModelPrimitive(
                planksNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f),
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


    public bool DoubleSided = doubleSided;


    public Color PostColor = postColor;


    public Color UnpaintedColor = unpaintedColor;


    public bool UseAlphaTest = useAlphaTest;
}

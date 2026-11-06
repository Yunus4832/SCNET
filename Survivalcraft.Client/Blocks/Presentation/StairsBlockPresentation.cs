using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

using static Game.Blocks.StairsBlock;

namespace Game.Blocks.Presentation;

public abstract class StairsBlockPresentation(
    StairsBlock block,
    BlockPresentationCatalog catalog,
    int coloredTextureSlot
) : BlockPresentation(block, catalog)
{
    protected new StairsBlock Block => (StairsBlock)base.Block;

    public BlockMesh[] ColoredBlockMeshes = new BlockMesh[24];

    public int ColoredTextureSlot = coloredTextureSlot;

    public BlockMesh StandaloneColoredBlockMesh = new();

    public BlockMesh StandaloneUncoloredBlockMesh = new();

    public BlockMesh[] UncoloredBlockMeshes = new BlockMesh[24];

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
        var color = GetColor(data);
        if (color.HasValue)
        {
            generator.GenerateShadedMeshVertices(
                this,
                x,
                y,
                z,
                ColoredBlockMeshes[GetVariant(data)],
                PalettePresentation.GetColor(generator.SubsystemPalette, color),
                null,
                [],
                geometry.SubsetOpaque
            );
        }
        else
        {
            generator.GenerateShadedMeshVertices(
                this,
                x,
                y,
                z,
                UncoloredBlockMeshes[GetVariant(data)],
                Color.White,
                null,
                [],
                geometry.SubsetOpaque
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
        }
        else
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneUncoloredBlockMesh,
                color,
                size,
                ref matrix,
                environmentData
            );
        }
    }

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
            base.GetFaceTextureSlot(0, value)
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Stairs");
        var stairsNode = model.FindNode("Stairs")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(stairsNode);
        var stairsOuterCornerNode = model.FindNode("StairsOuterCorner")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(stairsOuterCornerNode);
        var stairsInnerCornerNode = model.FindNode("StairsInnerCorner")!;
        var nodeAbsoluteTransform3 = BlockMesh.GetNodeAbsoluteTransform(stairsInnerCornerNode);
        for (var i = 0; i < 24; i++)
        {
            var rotation = GetRotation(i);
            var isUpsideDown = GetIsUpsideDown(i);
            var cornerType = GetCornerType(i);
            var m = !isUpsideDown
                ? Matrix.CreateRotationY(rotation * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationY(rotation * (float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0.5f, -0.5f, 0.5f) *
                  Matrix.CreateScale(1f, -1f, 1f) *
                  Matrix.CreateTranslation(0f, 0.5f, 0f);
            var blockMesh = new BlockMesh();
            switch (cornerType)
            {
                case CornerType.None:
                    blockMesh.AppendModelPrimitive(
                        stairsNode.RequireMesh().Primitives[0],
                        nodeAbsoluteTransform * m,
                        false,
                        isUpsideDown,
                        false,
                        false,
                        Color.White
                    );
                    break;
                case CornerType.OneQuarter:
                    blockMesh.AppendModelPrimitive(
                        stairsOuterCornerNode.RequireMesh().Primitives[0],
                        nodeAbsoluteTransform2 * m,
                        false,
                        isUpsideDown,
                        false,
                        false,
                        Color.White
                    );
                    break;
                case CornerType.ThreeQuarters:
                    blockMesh.AppendModelPrimitive(
                        stairsInnerCornerNode.RequireMesh().Primitives[0],
                        nodeAbsoluteTransform3 * m,
                        false,
                        isUpsideDown,
                        false,
                        false,
                        Color.White
                    );
                    break;
            }

            float num = isUpsideDown ? rotation : -rotation;
            blockMesh.TransformTextureCoordinates(
                Matrix.CreateTranslation(-0.03125f, -0.03125f, 0f) *
                Matrix.CreateRotationZ(num * (float)Math.PI / 2f) *
                Matrix.CreateTranslation(0.03125f, 0.03125f, 0f),
                16
            );
            blockMesh.TransformTextureCoordinates(
                Matrix.CreateTranslation(-0.03125f, -0.03125f, 0f) *
                Matrix.CreateRotationZ((0f - num) * (float)Math.PI / 2f) *
                Matrix.CreateTranslation(0.03125f, 0.03125f, 0f),
                32
            );
            if (isUpsideDown)
            {
                blockMesh.TransformTextureCoordinates(
                    Matrix.CreateTranslation(-0.03125f, -0.03125f, 0f) *
                    Matrix.CreateScale(1f, -1f, 1f) *
                    Matrix.CreateTranslation(0.03125f, 0.03125f, 0f)
                );
            }

            ColoredBlockMeshes[i] = new BlockMesh();
            ColoredBlockMeshes[i].AppendBlockMesh(blockMesh);
            ColoredBlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f)
            );
            ColoredBlockMeshes[i].GenerateSidesData();
            UncoloredBlockMeshes[i] = new BlockMesh();
            UncoloredBlockMeshes[i].AppendBlockMesh(blockMesh);
            UncoloredBlockMeshes[i]
                .TransformTextureCoordinates(
                    Matrix.CreateTranslation(
                        TextureSlot % 16 / 16f,
                        TextureSlot / 16 / 16f,
                        0f
                    )
                );
            UncoloredBlockMeshes[i].GenerateSidesData();
        }

        StandaloneUncoloredBlockMesh.AppendModelPrimitive(
            stairsNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneUncoloredBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(TextureSlot % 16 / 16f, TextureSlot / 16 / 16f, 0f)
        );
        StandaloneColoredBlockMesh.AppendModelPrimitive(
            stairsNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneColoredBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f)
        );
        base.Initialize();
    }
}

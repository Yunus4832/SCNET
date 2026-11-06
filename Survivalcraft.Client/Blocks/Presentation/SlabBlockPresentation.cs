using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

namespace Game.Blocks.Presentation;

public abstract class SlabBlockPresentation(SlabBlock block, BlockPresentationCatalog catalog, int coloredTextureSlot)
    : BlockPresentation(block, catalog)
{
    protected new SlabBlock Block => (SlabBlock)base.Block;

    public BlockMesh[] ColoredBlockMeshes = new BlockMesh[2];

    public int ColoredTextureSlot = coloredTextureSlot;

    public BlockMesh StandaloneColoredBlockMesh = new();

    public BlockMesh StandaloneUncoloredBlockMesh = new();

    public BlockMesh[] UncoloredBlockMeshes = new BlockMesh[2];

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
        var num = SlabBlock.GetIsTop(data) ? 1 : 0;
        var color = SlabBlock.GetColor(data);
        if (color.HasValue)
        {
            generator.GenerateShadedMeshVertices(
                this,
                x,
                y,
                z,
                ColoredBlockMeshes[num],
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
                UncoloredBlockMeshes[num],
                Color.White,
                null,
                [],
                geometry.SubsetOpaque
            );
        }
    }

    public override BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        var color = SlabBlock.GetColor(Terrain.ExtractData(value));
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
            GetFaceTextureSlot(0, value)
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
        var color2 = SlabBlock.GetColor(Terrain.ExtractData(value));
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
        var color = SlabBlock.GetColor(Terrain.ExtractData(value));
        return PalettePresentation.GetName(color, base.GetDisplayName(subsystemTerrain, value));
    }

    public override string GetCategory(int value)
    {
        return !SlabBlock.GetColor(Terrain.ExtractData(value)).HasValue ? base.GetCategory(value) : "Painted";
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Slab");
        var slabNode = model.FindNode("Slab")!;
        var meshPart = slabNode.RequireMesh().Primitives[0];
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(slabNode);
        for (var i = 0; i < 2; i++)
        {
            var matrix = nodeAbsoluteTransform * Matrix.CreateTranslation(0.5f, i == 0 ? 0f : 0.5f, 0.5f);
            UncoloredBlockMeshes[i] = new BlockMesh();
            UncoloredBlockMeshes[i].AppendModelPrimitive(meshPart, matrix, false, false, false, false, Color.White);
            UncoloredBlockMeshes[i]
                .TransformTextureCoordinates(
                    Matrix.CreateTranslation(
                        TextureSlot % 16 / 16f,
                        TextureSlot / 16 / 16f,
                        0f
                    )
                );
            UncoloredBlockMeshes[i].GenerateSidesData();
            ColoredBlockMeshes[i] = new BlockMesh();
            ColoredBlockMeshes[i].AppendModelPrimitive(meshPart, matrix, false, false, false, false, Color.White);
            ColoredBlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f)
            );
            ColoredBlockMeshes[i].GenerateSidesData();
        }

        StandaloneUncoloredBlockMesh.AppendModelPrimitive(
            meshPart,
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
            meshPart,
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

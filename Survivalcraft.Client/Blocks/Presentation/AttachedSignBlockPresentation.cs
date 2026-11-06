using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

using static Game.Blocks.AttachedSignBlock;

namespace Game.Blocks.Presentation;

public abstract class AttachedSignBlockPresentation(
    AttachedSignBlock block,
    BlockPresentationCatalog catalog,
    string modelName,
    int coloredTextureSlot
) : SignBlockPresentation(block, catalog)
{
    protected new AttachedSignBlock Block => (AttachedSignBlock)base.Block;

    public readonly BlockMesh[] BlockMeshes = new BlockMesh[4];

    public readonly BlockMesh[] ColoredBlockMeshes = new BlockMesh[4];

    public int ColoredTextureSlot = coloredTextureSlot;

    public readonly BlockMesh StandaloneBlockMesh = new();

    public readonly BlockMesh StandaloneColoredBlockMesh = new();

    public readonly BlockMesh[] SurfaceMeshes = new BlockMesh[4];

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
        var face = GetFace(data);
        var color = GetColor(data);
        if (color.HasValue)
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                ColoredBlockMeshes[face],
                PalettePresentation.GetColor(generator.SubsystemPalette, color),
                null,
                geometry.SubsetOpaque
            );
        }
        else
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                BlockMeshes[face],
                Color.White,
                null,
                geometry.SubsetOpaque
            );
        }

        generator.GenerateWireVertices(
            value,
            x,
            y,
            z,
            GetFace(data),
            0.375f,
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

    public override BlockMesh GetSignSurfaceBlockMesh(int data) => SurfaceMeshes[GetFace(data)];


    public string ModelName = modelName;

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var signNode = model.FindNode("Sign")!;
        var surfaceNode = model.FindNode("Surface")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(signNode);
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(surfaceNode);
        for (var i = 0; i < 4; i++)
        {
            var radians = (float)Math.PI / 2f * i;
            var m = Matrix.CreateTranslation(0f, 0f, -15f / 32f) *
                    Matrix.CreateRotationY(radians) *
                    Matrix.CreateTranslation(0.5f, -0.3125f, 0.5f);
            var blockMesh = new BlockMesh();
            blockMesh.AppendModelPrimitive(
                signNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * m,
                false,
                false,
                false,
                false,
                Color.White
            );
            BlockMeshes[i] = new BlockMesh();
            BlockMeshes[i].AppendBlockMesh(blockMesh);
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
            SurfaceMeshes[i] = new BlockMesh();
            SurfaceMeshes[i].AppendModelPrimitive(
                surfaceNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * m,
                false,
                false,
                false,
                false,
                Color.White
            );
        }

        StandaloneBlockMesh.AppendModelPrimitive(
            signNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.6f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
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
}

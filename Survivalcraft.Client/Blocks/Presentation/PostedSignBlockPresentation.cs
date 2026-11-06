using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

using static Game.Blocks.PostedSignBlock;

namespace Game.Blocks.Presentation;

public abstract class PostedSignBlockPresentation(
    PostedSignBlock block,
    BlockPresentationCatalog catalog,
    string modelName,
    int coloredTextureSlot
) : SignBlockPresentation(block, catalog)
{
    protected new PostedSignBlock Block => (PostedSignBlock)base.Block;

    public BlockMesh[] BlockMeshes = new BlockMesh[16];

    public BlockMesh[] ColoredBlockMeshes = new BlockMesh[16];

    public int ColoredTextureSlot = coloredTextureSlot;

    public BlockMesh StandaloneBlockMesh = new();

    public BlockMesh StandaloneColoredBlockMesh = new();

    public BlockMesh[] SurfaceMeshes = new BlockMesh[16];

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var color = GetColor(Terrain.ExtractData(value));
        return PalettePresentation.GetName(color, base.GetDisplayName(subsystemTerrain, value));
    }

    public override string GetCategory(int value)
    {
        if (!GetColor(Terrain.ExtractData(value)).HasValue)
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
                BlockMeshes[variant],
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
            GetHanging(data) ? 5 : 4,
            0.01f,
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
        }
        else
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneBlockMesh,
                color,
                1.25f * size,
                ref matrix,
                environmentData
            );
        }
    }

    public override BlockMesh GetSignSurfaceBlockMesh(int data)
    {
        return SurfaceMeshes[GetVariant(data)];
    }

    public string ModelName = modelName;

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var signNode = model.FindNode("Sign")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(signNode);
        var postNode = model.FindNode("Post")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(postNode);
        var surfaceNode = model.FindNode("Surface")!;
        var nodeAbsoluteTransform3 = BlockMesh.GetNodeAbsoluteTransform(surfaceNode);
        for (var i = 0; i < 16; i++)
        {
            var hanging = GetHanging(i);
            var m = Matrix.CreateRotationY(GetDirection(i) * (float)Math.PI / 4f) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            if (hanging)
            {
                m *= Matrix.CreateScale(1f, -1f, 1f) * Matrix.CreateTranslation(0f, 1f, 0f);
            }

            var blockMesh = new BlockMesh();
            blockMesh.AppendModelPrimitive(
                signNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * m,
                false,
                hanging,
                false,
                false,
                Color.White
            );
            var blockMesh2 = new BlockMesh();
            blockMesh2.AppendModelPrimitive(
                postNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * m,
                false,
                hanging,
                false,
                false,
                Color.White
            );
            BlockMeshes[i] = new BlockMesh();
            BlockMeshes[i].AppendBlockMesh(blockMesh);
            BlockMeshes[i].AppendBlockMesh(blockMesh2);
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
                nodeAbsoluteTransform3 * m,
                false,
                hanging,
                false,
                false,
                Color.White
            );
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

        StandaloneBlockMesh.AppendModelPrimitive(
            signNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.6f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            postNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.6f, 0f),
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

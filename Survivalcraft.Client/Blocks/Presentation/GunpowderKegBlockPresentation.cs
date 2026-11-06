using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class GunpowderKegBlockPresentation(
    GunpowderKegBlock block,
    BlockPresentationCatalog catalog,
    string modelName
) : BlockPresentation(block, catalog)
{
    protected new GunpowderKegBlock Block => (GunpowderKegBlock)base.Block;

    public readonly BlockMesh BlockMesh = new();

    public readonly BlockMesh StandaloneBlockMesh = new();

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        generator.GenerateMeshVertices(this, x, y, z, BlockMesh, Color.White, null, geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, 4, 0.25f, Vector2.Zero, geometry.SubsetOpaque);
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
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            size,
            ref matrix,
            environmentData
        );
    }

    public string ModelName = modelName;

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var kegNode = model.FindNode("Keg")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(kegNode);
        var blockMesh = new BlockMesh();
        blockMesh.AppendModelPrimitive(
            kegNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMesh.AppendBlockMesh(blockMesh);
        if (Block.IsIncendiary)
        {
            BlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(-0.25f, 0f, 0f));
        }

        StandaloneBlockMesh.AppendModelPrimitive(
            kegNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        if (Block.IsIncendiary)
        {
            StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(-0.25f, 0f, 0f));
        }

        base.Initialize();
    }
}

using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class FoodBlockPresentation(
    FoodBlock block,
    BlockPresentationCatalog catalog,
    string modelName,
    Matrix tcTransform,
    Color color
) : BlockPresentation(block, catalog)
{
    protected new FoodBlock Block => (FoodBlock)base.Block;

    public BlockMesh StandaloneBlockMesh = new();

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
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
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(model.MeshNodes[0]);
        StandaloneBlockMesh.AppendModelPrimitive(
            model.MeshNodes[0].RequireMesh().Primitives[0],
            nodeAbsoluteTransform,
            false,
            false,
            false,
            false,
            Color
        );
        StandaloneBlockMesh.TransformTextureCoordinates(TcTransform);
        base.Initialize();
    }

    public string ModelName = modelName;

    public Color Color = color;


    public Matrix TcTransform = tcTransform;
}

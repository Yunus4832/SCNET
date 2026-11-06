using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class ChunkBlockPresentation(
    ChunkBlock block,
    BlockPresentationCatalog catalog,
    Matrix transform,
    Matrix tcTransform,
    Color color,
    bool smooth
) : BlockPresentation(block, catalog)
{
    protected new ChunkBlock Block => (ChunkBlock)base.Block;

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
        var model = Smooth
            ? ContentManager.Get<Model>("Models/ChunkSmooth")
            : ContentManager.Get<Model>("Models/Chunk");
        var matrix = BlockMesh.GetNodeAbsoluteTransform(model.MeshNodes[0]) *
                     Transform;
        StandaloneBlockMesh.AppendModelPrimitive(
            model.MeshNodes[0].RequireMesh().Primitives[0],
            matrix,
            false,
            false,
            false,
            false,
            Color
        );
        StandaloneBlockMesh.TransformTextureCoordinates(TcTransform);
        base.Initialize();
    }

    public Color Color = color;


    public readonly bool Smooth = smooth;


    public Matrix TcTransform = tcTransform;


    public Matrix Transform = transform;
}

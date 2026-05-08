using Engine.Graphics;

namespace Game.Blocks;

public abstract class ChunkBlock(
    Matrix transform,
    Matrix tcTransform,
    Color color,
    bool smooth
) : Block
{
    public Color Color = color;

    public readonly bool Smooth = smooth;

    public readonly BlockMesh StandaloneBlockMesh = new();

    public Matrix TcTransform = tcTransform;

    public Matrix Transform = transform;

    public override void Initialize()
    {
        var model = Smooth
            ? ContentManager.Get<Model>("Models/ChunkSmooth")
            : ContentManager.Get<Model>("Models/Chunk");
        var matrix = BlockMesh.GetBoneAbsoluteTransform(
            model.Meshes[0].ParentBone ??
            throw new InvalidOperationException("Required Model.ParentBone is null")
        ) * Transform;
        StandaloneBlockMesh.AppendModelMeshPart(model.Meshes[0].MeshParts[0], matrix, false, false, false, false,
            Color);
        StandaloneBlockMesh.TransformTextureCoordinates(TcTransform);
        base.Initialize();
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
        BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMesh, color, 2f * size, ref matrix,
            environmentData);
    }
}

using Engine.Graphics;

namespace Game.Blocks;

public class SaddleBlock : Block
{
    public const int Index = 158;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Saddle");
        var saddleMesh = model.FindMesh("Saddle")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            saddleMesh.ParentBone ??
            throw new InvalidOperationException("Required SaddleMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(saddleMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.2f, 0f), false, false, false, false,
            new Color(224, 224, 224));
        StandaloneBlockMesh.AppendModelMeshPart(saddleMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.2f, 0f), false, true, false, false,
            new Color(96, 96, 96));
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
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }
}

using Engine.Graphics;

namespace Game.Blocks;

public class WhistleBlock : Block
{
    public const int Index = 160;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Whistle");
        var whistleMesh = model.FindMesh("Whistle")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            whistleMesh.ParentBone ??
            throw new InvalidOperationException("Required WhistleMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(whistleMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.04f, 0f), false, false, false, false,
            new Color(255, 255, 255));
        StandaloneBlockMesh.AppendModelMeshPart(whistleMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.04f, 0f), false, true, false, false,
            new Color(64, 64, 64));
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
            9f * size,
            ref matrix,
            environmentData
        );
    }
}

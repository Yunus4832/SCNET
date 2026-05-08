using Engine.Graphics;

namespace Game.Blocks;

public class DiamondChunkBlock : Block
{
    public const int Index = 111;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Diamond");
        var diamondMesh = model.FindMesh("Diamond")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            diamondMesh.ParentBone ??
            throw new InvalidOperationException("Require DiamondMesh.ParentBone is null"));
        StandaloneBlockMesh.AppendModelMeshPart(diamondMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
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

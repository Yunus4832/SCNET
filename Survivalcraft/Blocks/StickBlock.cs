using Engine.Graphics;

namespace Game.Blocks;

public class StickBlock : Block
{
    public const int Index = 23;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Stick");
        var stickMesh = model.FindMesh("Stick")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            stickMesh.ParentBone ??
            throw new InvalidOperationException("Required StickMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(stickMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
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

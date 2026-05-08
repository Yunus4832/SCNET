using Engine.Graphics;

namespace Game.Blocks;

public class FurBlock : Block
{
    public const int Index = 207;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Fur");
        var furMesh = model.FindMesh("Fur")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            furMesh.ParentBone
            ?? throw new InvalidOperationException("Required FurMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(furMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(furMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), false, true, false, false,
            new Color(128, 128, 160));
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

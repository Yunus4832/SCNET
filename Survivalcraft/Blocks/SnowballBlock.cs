using Engine.Graphics;

namespace Game.Blocks;

public class SnowballBlock : Block
{
    public const int Index = 85;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Snowball");
        var snowballMesh = model.FindMesh("Snowball")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            snowballMesh.ParentBone ??
            throw new InvalidOperationException("Required SnowballMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(snowballMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
        base.Initialize();
    }

    public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value,
        int x, int y, int z)
    {
    }

    public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size,
        ref Matrix matrix, DrawBlockEnvironmentData environmentData)
    {
        BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMesh, color, 2.5f * size, ref matrix,
            environmentData);
    }
}

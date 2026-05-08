using Engine.Graphics;

namespace Game.Blocks;

public class CactusBlock : Block
{
    public const int Index = 127;

    public BlockMesh BlockMesh = new();

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Cactus");
        var cactusMesh = model.FindMesh("Cactus")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            cactusMesh.ParentBone ??
            throw new InvalidOperationException("Required CactusMesh.ParentBone is null")
        );
        BlockMesh.AppendModelMeshPart(cactusMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(cactusMesh.MeshParts[0],
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
        generator.GenerateMeshVertices(this, x, y, z, BlockMesh, Color.White, null, geometry.SubsetAlphaTest);
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

    public override bool ShouldAvoid(int value)
    {
        return true;
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }
}

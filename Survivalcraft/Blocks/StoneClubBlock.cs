using Engine.Graphics;

namespace Game.Blocks;

public class StoneClubBlock : Block
{
    public const int Index = 123;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var num = 47;
        var num2 = 1;
        var model = ContentManager.Get<Model>("Models/StoneClub");
        var handleMesh = model.FindMesh("Handle")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            handleMesh.ParentBone ??
            throw new InvalidOperationException("Required HandleMesh.ParentBone is null")
        );
        var spikesMesh = model.FindMesh("Spikes")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            spikesMesh.ParentBone ??
            throw new InvalidOperationException("Required SpikesMesh.ParentBone is null")
        );
        var blockMesh = new BlockMesh();
        blockMesh.AppendModelMeshPart(handleMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        blockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(num % 16 / 16f, num / 16 / 16f, 0f));
        var blockMesh2 = new BlockMesh();
        blockMesh2.AppendModelMeshPart(spikesMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        blockMesh2.TransformTextureCoordinates(Matrix.CreateTranslation(num2 % 16 / 16f, num2 / 16 / 16f, 0f));
        StandaloneBlockMesh.AppendBlockMesh(blockMesh);
        StandaloneBlockMesh.AppendBlockMesh(blockMesh2);
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

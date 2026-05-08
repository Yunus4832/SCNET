using Engine.Graphics;

namespace Game.Blocks;

public class BrickBlock : Block
{
    public const int Index = 74;

    public BlockMesh StandaloneBlockMesh = new();

    public override bool FurnitureBuilt { get; set; } = true;

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Brick");
        var brickMesh = model.FindMesh("Brick")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            brickMesh.ParentBone ??
            throw new InvalidOperationException("Required BrickMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(
            brickMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.075f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
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
            2.5f * size,
            ref matrix,
            environmentData
        );
    }
}

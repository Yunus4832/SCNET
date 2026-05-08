using Engine.Graphics;

namespace Game.Blocks;

public class IncendiaryBombBlock : Block
{
    public const int Index = 228;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Bomb");
        var bombMesh = model.FindMesh("Bomb")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            bombMesh.ParentBone ??
            throw new InvalidOperationException("Required BombMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(bombMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.25f, 0f), false, false, false, false,
            new Color(0.4f, 0.2f, 0.2f));
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

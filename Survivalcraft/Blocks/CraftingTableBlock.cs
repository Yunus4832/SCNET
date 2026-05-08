using Engine.Graphics;

namespace Game.Blocks;

public class CraftingTableBlock : Block
{
    public const int Index = 27;

    public BlockMesh BlockMesh = new();

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/CraftingTable");
        var craftingTableMesh = model.FindMesh("CraftingTable")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            craftingTableMesh.ParentBone ??
            throw new InvalidOperationException("Required CraftingTableMesh.ParentBone is null")
        );
        BlockMesh.AppendModelMeshPart(craftingTableMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(craftingTableMesh.MeshParts[0],
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
        generator.GenerateShadedMeshVertices(
            this,
            x,
            y,
            z,
            BlockMesh,
            Color.White,
            null,
            [],
            geometry.SubsetOpaque
        );
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
}

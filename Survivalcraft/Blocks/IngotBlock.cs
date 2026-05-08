using Engine.Graphics;

namespace Game.Blocks;

public abstract class IngotBlock(string meshName) : Block
{
    public string MeshName = meshName;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Ingots");
        var modelMesh = model.FindMesh(MeshName)!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            modelMesh.ParentBone ??
            throw new InvalidOperationException("Required ModelMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(modelMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, false, false, Color.White);
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
        BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMesh, color, 2f * size, ref matrix,
            environmentData);
    }
}

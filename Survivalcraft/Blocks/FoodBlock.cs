using Engine.Graphics;

namespace Game.Blocks;

public abstract class FoodBlock(
    string modelName,
    Matrix tcTransform,
    Color color,
    int rottenValue
) : Block
{
    public static int CompostValue = Terrain.MakeBlockValue(
        168,
        0,
        SoilBlock.SetHydration(SoilBlock.SetNitrogen(0, 1), false)
    );

    public Color Color = color;

    public string ModelName = modelName;

    public int RottenValue = rottenValue;

    public BlockMesh StandaloneBlockMesh = new();

    public Matrix TcTransform = tcTransform;

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            model.Meshes[0].ParentBone ??
            throw new InvalidOperationException("Required ModelMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(
            model.Meshes[0].MeshParts[0],
            boneAbsoluteTransform,
            false,
            false,
            false,
            false,
            Color
        );
        StandaloneBlockMesh.TransformTextureCoordinates(TcTransform);
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

    public override int GetDamageDestructionValue(int value)
    {
        return RottenValue;
    }
}

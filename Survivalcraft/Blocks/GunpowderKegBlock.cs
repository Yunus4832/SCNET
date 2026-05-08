using Engine.Graphics;

namespace Game.Blocks;

public abstract class GunpowderKegBlock(string modelName, bool isIncendiary) : Block, IElectricElementBlock
{
    public readonly BlockMesh BlockMesh = new();

    public BoundingBox[] CollisionBoxes = [];

    public bool IsIncendiary = isIncendiary;

    public string ModelName = modelName;

    public readonly BlockMesh StandaloneBlockMesh = new();

    public Vector3 FuseOffset { get; set; }

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new GunpowderKegElectricElement(subsystemElectricity, new CellFace(x, y, z, 4));
    }

    public ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        if (face == 4)
        {
            return ElectricConnectorType.Input;
        }

        return null;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var kegMesh = model.FindMesh("Keg")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            kegMesh.ParentBone ??
            throw new InvalidOperationException("Required KegMesh.ParentBone is null")
        );
        var fuseMesh = model.FindMesh("Fuse")!;
        FuseOffset = BlockMesh.GetBoneAbsoluteTransform(
            fuseMesh.ParentBone ??
            throw new InvalidOperationException("Required FuseMesh.ParentBone is null")
        ).Translation + new Vector3(0.5f, 0f, 0.5f);
        var blockMesh = new BlockMesh();
        blockMesh.AppendModelMeshPart(kegMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, Color.White);
        BlockMesh.AppendBlockMesh(blockMesh);
        if (IsIncendiary)
        {
            BlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(-0.25f, 0f, 0f));
        }

        CollisionBoxes = [blockMesh.CalculateBoundingBox()];
        StandaloneBlockMesh.AppendModelMeshPart(kegMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        if (IsIncendiary)
        {
            StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(-0.25f, 0f, 0f));
        }

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
        generator.GenerateMeshVertices(this, x, y, z, BlockMesh, Color.White, null, geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, 4, 0.25f, Vector2.Zero, geometry.SubsetOpaque);
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

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
    }
}

using Engine.Graphics;

namespace Game.Blocks;

public class CairnBlock : Block
{
    public const int Index = 258;

    private readonly BlockMesh _blockMesh = new();

    private readonly BlockMesh _standaloneMesh = new();

    private readonly BoundingBox[] _collisionBoxes = new BoundingBox[1];

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Cairn");
        var cairnMesh = model.FindMesh("Cairn")!;
        var woodMesh = model.FindMesh("Wood")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            cairnMesh.ParentBone ??
            throw new InvalidOperationException("Required CairnMesh.ParentBone is null"));
        var white = Color.White;
        var blockMesh = new BlockMesh();
        blockMesh.AppendModelMeshPart(cairnMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateRotationX(-(float)Math.PI / 2f) *
            Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, white);
        var blockMesh2 = new BlockMesh();
        blockMesh2.AppendModelMeshPart(woodMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateRotationX(-(float)Math.PI / 2f) *
            Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, white);
        _blockMesh.AppendBlockMesh(blockMesh);
        _blockMesh.AppendBlockMesh(blockMesh2);
        _standaloneMesh.AppendModelMeshPart(cairnMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateScale(1.3f) * Matrix.CreateRotationX(-(float)Math.PI / 2f) *
            Matrix.CreateTranslation(0f, 0f, 0f), false, false, true, false, white);
        _standaloneMesh.AppendModelMeshPart(woodMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateScale(1.3f) * Matrix.CreateRotationX(-(float)Math.PI / 2f) *
            Matrix.CreateTranslation(0f, 0f, 0f), false, false, true, false, white);
        _collisionBoxes[0] = blockMesh.CalculateBoundingBox();
        base.Initialize();
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return _collisionBoxes;
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
        generator.GenerateMeshVertices(this, x, y, z, _blockMesh, Color.White, null, geometry.SubsetOpaque);
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
            _standaloneMesh,
            color,
            size,
            ref matrix,
            environmentData
        );
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        var num = Terrain.ExtractData(oldValue);
        var num2 = 10 + 4 * num;
        var num3 = num >= 3 ? 1 : 0;
        BlockDropValue item;
        for (var i = 0; i < 3; i++)
        {
            item = new BlockDropValue
            {
                Value = 79,
                Count = 1
            };
            dropValues.Add(item);
        }

        for (var j = 0; j < num2; j++)
        {
            item = new BlockDropValue
            {
                Value = 248,
                Count = 1
            };
            dropValues.Add(item);
        }

        for (var k = 0; k < num3; k++)
        {
            item = new BlockDropValue
            {
                Value = 111,
                Count = 1
            };
            dropValues.Add(item);
        }

        for (var l = 0; l < 2; l++)
        {
            item = new BlockDropValue
            {
                Value = 23,
                Count = 1
            };
            dropValues.Add(item);
        }

        showDebris = false;
    }
}

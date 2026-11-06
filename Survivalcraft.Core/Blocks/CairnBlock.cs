using Game.Blocks.Collision;

namespace Game.Blocks;

public class CairnBlock : Block
{
    private readonly BoundingBox[] _collisionBoxes = new BoundingBox[1];

    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Cairn.glb");

    public override void Initialize()
    {
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var cairnNode = model.GetNode("Cairn")!;
        var nodeAbsoluteTransform = cairnNode.AbsoluteTransform;
        var blockMesh = new CollisionGeometryBuilder();
        blockMesh.Append(
            cairnNode.Primitives[0],
            nodeAbsoluteTransform *
            Matrix.CreateRotationX(-(float)Math.PI / 2f) *
            Matrix.CreateTranslation(0.5f, 0f, 0.5f)
        );
        _collisionBoxes[0] = blockMesh.GetBounds();
        base.Initialize();
    }


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return _collisionBoxes;
    }


    public override bool AllowsDestructionDebris(int oldValue, int newValue) => false;

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

        showDebris = AllowsDestructionDebris(oldValue, newValue);
    }
}

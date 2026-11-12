using Game.Blocks.Collision;

namespace Game.Blocks;

public abstract class FenceBlock(string modelName) : Block, IPaintableBlock
{
    public BoundingBox[][] CollisionBoxes = new BoundingBox[16][];


    public int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        var data = Terrain.ExtractData(value);
        return Terrain.ReplaceData(value, SetColor(data, color));
    }

    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:" + modelName + ".glb");

    public override void Initialize()
    {
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var postNode = model.GetNode("Post")!;
        var nodeAbsoluteTransform = postNode.AbsoluteTransform;
        var planksNode = model.GetNode("Planks")!;
        var nodeAbsoluteTransform2 = planksNode.AbsoluteTransform;
        for (var i = 0; i < 16; i++)
        {
            var num = (i & 1) != 0;
            var flag = (i & 2) != 0;
            var flag2 = (i & 4) != 0;
            var flag3 = (i & 8) != 0;
            var list = new List<BoundingBox>();
            var m = Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            var blockMesh = new CollisionGeometryBuilder();
            blockMesh.Append(postNode.Primitives[0], nodeAbsoluteTransform * m);
            var item = blockMesh.GetBounds();
            var min = item.Min;
            var max = item.Max;
            min.X -= 0.1f;
            min.Z -= 0.1f;
            max.X += 0.1f;
            max.Z += 0.1f;
            item.Min = min;
            item.Max = max;
            list.Add(item);
            if (num)
            {
                var blockMesh3 = new CollisionGeometryBuilder();
                var m2 = Matrix.CreateRotationY(0f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh3.Append(planksNode.Primitives[0], nodeAbsoluteTransform2 * m2);

                var item2 = blockMesh3.GetBounds();
                list.Add(item2);
            }

            if (flag)
            {
                var blockMesh4 = new CollisionGeometryBuilder();
                var m3 = Matrix.CreateRotationY((float)Math.PI) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh4.Append(planksNode.Primitives[0], nodeAbsoluteTransform2 * m3);

                var item3 = blockMesh4.GetBounds();
                list.Add(item3);
            }

            if (flag2)
            {
                var blockMesh5 = new CollisionGeometryBuilder();
                var m4 = Matrix.CreateRotationY(4.712389f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh5.Append(planksNode.Primitives[0], nodeAbsoluteTransform2 * m4);

                var item4 = blockMesh5.GetBounds();
                list.Add(item4);
            }

            if (flag3)
            {
                var blockMesh6 = new CollisionGeometryBuilder();
                var m5 = Matrix.CreateRotationY((float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                blockMesh6.Append(planksNode.Primitives[0], nodeAbsoluteTransform2 * m5);

                var item5 = blockMesh6.GetBounds();
                list.Add(item5);
            }

            CollisionBoxes[i] = list.ToArray();
        }

        base.Initialize();
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, SetColor(0, null));
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
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
        showDebris = true;
        var data = SetVariant(Terrain.ExtractData(oldValue), 0);
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex, data),
                Count = 1
            }
        );
    }


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var variant = GetVariant(Terrain.ExtractData(value));
        return CollisionBoxes[variant];
    }

    public virtual bool ShouldConnectTo(int value)
    {
        var num = Terrain.ExtractContents(value);
        var block = Catalog.Blocks[num];
        if (block is not FenceBlock)
        {
            return block is FenceGateBlock;
        }

        return true;
    }

    public static int GetVariant(int data) => data & 0xF;

    public static int SetVariant(int data, int variant) => (data & -16) | (variant & 0xF);

    public static int? GetColor(int data)
    {
        if ((data & 0x10) != 0)
        {
            return (data >> 5) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -497) | 0x10 | ((color.Value & 0xF) << 5);
        }

        return data & -497;
    }

    public override bool IsFaceNonAttachable(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int attachBlockValue
    )
    {
        var block = Catalog.Blocks[Terrain.ExtractContents(attachBlockValue)];
        return block is not BasePumpkinBlock &&
               base.IsFaceNonAttachable(subsystemTerrain, face, value, attachBlockValue);
    }
}

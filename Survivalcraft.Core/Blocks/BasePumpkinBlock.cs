using Game.Blocks.Collision;

namespace Game.Blocks;

public abstract class BasePumpkinBlock(bool isRotten) : Block
{
    public BoundingBox[][] CollisionBoxesBySize = new BoundingBox[8][];

    public bool IsRotten = isRotten;


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Pumpkins.glb");

    public override void Initialize()
    {
        var blockPositionsBySize = new CollisionGeometryBuilder[8];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var pumpkinNode = model.GetNode("Pumpkin")!;
        var nodeAbsoluteTransform = pumpkinNode.AbsoluteTransform;
        for (var i = 0; i < 8; i++)
        {
            var num = MathUtils.Lerp(0.2f, 1f, i / 7f);
            var num2 = MathUtils.Min(0.3f * num, 0.7f * (1f - num));
            blockPositionsBySize[i] = new CollisionGeometryBuilder();
            if (i >= 1)
            {
                blockPositionsBySize[i].Append(
                    pumpkinNode.Primitives[0],
                    nodeAbsoluteTransform *
                    Matrix.CreateScale(num) *
                    Matrix.CreateTranslation(0.5f + num2, 0f, 0.5f + num2)
                );
            }
        }

        for (var j = 0; j < 8; j++)
        {
            var boundingBox = blockPositionsBySize[j].Count > 0
                ? blockPositionsBySize[j].GetBounds()
                : new BoundingBox(new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f));
            var num3 = boundingBox.Max.X - boundingBox.Min.X;
            if (num3 < 0.8f)
            {
                var num4 = (0.8f - num3) / 2f;
                var min = boundingBox.Min;
                var max = boundingBox.Max;
                min.X -= num4;
                min.Z -= num4;
                max.X += num4;
                max.Y = 0.4f;
                max.Z += num4;
                boundingBox.Min = min;
                boundingBox.Max = max;
            }

            CollisionBoxesBySize[j] = [boundingBox];
        }

        base.Initialize();
    }


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var size = GetSize(Terrain.ExtractData(value));
        return CollisionBoxesBySize[size];
    }


    public override float GetNutritionalValue(int value)
    {
        if (GetSize(Terrain.ExtractData(value)) != 7)
        {
            return 0f;
        }

        return base.GetNutritionalValue(value);
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
        var size = GetSize(Terrain.ExtractData(oldValue));
        if (size >= 1)
        {
            var value = SetDamage(
                Terrain.MakeBlockValue(BlockIndex, SetSize(SetIsDead(0, true), size)),
                GetDamage(oldValue)
            );
            dropValues.Add(
                new BlockDropValue
                {
                    Value = value,
                    Count = 1
                }
            );
        }

        showDebris = true;
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, SetSize(SetIsDead(0, true), 1));
        yield return Terrain.MakeBlockValue(BlockIndex, SetSize(SetIsDead(0, true), 3));
        yield return Terrain.MakeBlockValue(BlockIndex, SetSize(SetIsDead(0, true), 5));
        yield return Terrain.MakeBlockValue(BlockIndex, SetSize(SetIsDead(0, true), 7));
    }

    public static int GetSize(int data)
    {
        return 7 - (data & 7);
    }

    public static int SetSize(int data, int size)
    {
        return (data & -8) | (7 - (size & 7));
    }

    public static bool GetIsDead(int data)
    {
        return (data & 8) != 0;
    }

    public static int SetIsDead(int data, bool isDead)
    {
        if (!isDead)
        {
            return data & -9;
        }

        return data | 8;
    }

    public override int GetDamage(int value)
    {
        return (Terrain.ExtractData(value) & 0x10) >> 4;
    }

    public override int SetDamage(int value, int damage)
    {
        var num = Terrain.ExtractData(value);
        return Terrain.ReplaceData(value, (num & -17) | ((damage & 1) << 4));
    }

    public override int GetDamageDestructionValue(int value)
    {
        if (IsRotten)
        {
            return 0;
        }

        var data = Terrain.ExtractData(value);
        return SetDamage(Terrain.MakeBlockValue(244, data), 0);
    }

    public override int GetRotPeriod(int value)
    {
        return !GetIsDead(Terrain.ExtractData(value)) ? 0 : RotPeriod;
    }
}

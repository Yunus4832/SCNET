using System.Globalization;

using Game.Blocks.Collision;

namespace Game.Blocks;

public class GravestoneBlock : Block
{
    public BoundingBox[][] CollisionBoxes = new BoundingBox[16][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Graves.glb");

    public override void Initialize()
    {
        var blockPositions = new CollisionGeometryBuilder[16];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        for (var i = 0; i < 16; i++)
        {
            var variant = GetVariant(i);
            var radians = GetRotation(i) == 0 ? 0f : (float)Math.PI / 2f;
            var name = "Grave" + (variant % 4 + 1).ToString(CultureInfo.InvariantCulture);
            var num = variant >= 4;
            var modelNode = model.GetNode(name)!;
            var nodeAbsoluteTransform = modelNode.AbsoluteTransform;
            blockPositions[i] = new CollisionGeometryBuilder();
            blockPositions[i].Append(
                modelNode.Primitives[0],
                nodeAbsoluteTransform * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f)
            );
            if (num)
            {
                var nodeAbsoluteTransform2 = model.GetNode("Plinth")!.AbsoluteTransform;
                blockPositions[i].Append(
                    model.GetNode("Plinth")!.Primitives[0],
                    nodeAbsoluteTransform2 * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                );
            }

            CollisionBoxes[i] = [blockPositions[i].GetBounds()];
        }

        base.Initialize();
    }


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        if (num < CollisionBoxes.Length)
        {
            return CollisionBoxes[num];
        }

        return base.GetCustomCollisionBoxes(terrain, value);
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var data = Terrain.ExtractData(value);
        var forward = eyeForward;
        var num = MathUtils.Abs(Vector3.Dot(forward, Vector3.UnitX));
        BlockPlacementData result;
        if (MathUtils.Abs(Vector3.Dot(forward, Vector3.UnitZ)) > num)
        {
            result = default;
            result.Value = Terrain.MakeBlockValue(BlockIndex, 0, SetRotation(data, 0));
            result.CellFace = raycastResult.CellFace;
            return result;
        }

        result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, SetRotation(data, 1));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < 8)
        {
            var data = SetVariant(0, i);
            yield return Terrain.MakeBlockValue(BlockIndex, 0, data);
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
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex, 0, Terrain.ExtractData(oldValue)),
                Count = 1
            }
        );
    }

    public static int GetRotation(int data)
    {
        return (data & 8) >> 3;
    }

    public static int SetRotation(int data, int rotation)
    {
        return (data & -9) | ((rotation << 3) & 8);
    }

    public static int GetVariant(int data)
    {
        return data & 7;
    }

    public static int SetVariant(int data, int variant)
    {
        return (data & -8) | (variant & 7);
    }
}

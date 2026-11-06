using Game.Blocks.Collision;

namespace Game.Blocks;

public abstract class RotatableMountedElectricElementBlock(string modelName, string meshName, float centerBoxSize)
    : MountedElectricElementBlock
{
    public string CollisionNodeName { get; } = meshName;

    public float CenterBoxSize = centerBoxSize;

    public BoundingBox[][] CollisionBoxes = new BoundingBox[24][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:" + modelName + ".glb");

    public override void Initialize()
    {
        var blockPositions = new CollisionGeometryBuilder[24];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var modelNode = model.GetNode(CollisionNodeName)!;
        var nodeAbsoluteTransform = modelNode.AbsoluteTransform;
        for (var i = 0; i < 6; i++)
        {
            float radians;
            bool flag;
            switch (i)
            {
                case < 4:
                    radians = i * (float)Math.PI / 2f;
                    flag = false;
                    break;
                case 4:
                    radians = -(float)Math.PI / 2f;
                    flag = true;
                    break;
                default:
                    radians = (float)Math.PI / 2f;
                    flag = true;
                    break;
            }

            for (var j = 0; j < 4; j++)
            {
                var radians2 = -j * (float)Math.PI / 2f;
                var num = (i << 2) + j;
                var m = Matrix.CreateRotationX((float)Math.PI / 2f) *
                        Matrix.CreateRotationZ(radians2) *
                        Matrix.CreateTranslation(0f, 0f, -0.5f) *
                        (flag ? Matrix.CreateRotationX(radians) : Matrix.CreateRotationY(radians)) *
                        Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                blockPositions[num] = new CollisionGeometryBuilder();
                blockPositions[num].Append(modelNode.Primitives[0], nodeAbsoluteTransform * m);
                CollisionBoxes[num] = [blockPositions[num].GetBounds()];
            }
        }
    }


    public override int GetFace(int value)
    {
        return (Terrain.ExtractData(value) >> 2) & 7;
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var rotation = 0;
        if (raycastResult.CellFace.Face >= 4)
        {
            var forward = eyeForward;
            var num = Vector3.Dot(forward, Vector3.UnitZ);
            var num2 = Vector3.Dot(forward, Vector3.UnitX);
            var num3 = Vector3.Dot(forward, -Vector3.UnitZ);
            var num4 = Vector3.Dot(forward, -Vector3.UnitX);
            if (num.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 2;
            }
            else if (num2.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 1;
            }
            else if (num3.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 0;
            }
            else if (num4.CloseTo(MathUtils.Max(num, num2, num3, num4)))
            {
                rotation = 3;
            }
        }

        var num5 = Terrain.ExtractData(value);
        num5 &= -29;
        num5 |= raycastResult.CellFace.Face << 2;
        BlockPlacementData result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, SetRotation(num5, rotation));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value) & 0x1F;
        return CollisionBoxes[num];
    }

    public static int GetRotation(int data)
    {
        return data & 3;
    }

    public static int SetRotation(int data, int rotation)
    {
        return (data & -4) | (rotation & 3);
    }
}

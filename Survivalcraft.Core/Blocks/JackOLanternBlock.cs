using Game.Blocks.Collision;

namespace Game.Blocks;

public class JackOLanternBlock : Block
{
    public BoundingBox[] CollisionBoxes = new BoundingBox[1];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Pumpkins.glb");

    public override void Initialize()
    {
        var blockPositionsByData = new CollisionGeometryBuilder[4];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var jackOLanternNode = model.GetNode("JackOLantern")!;
        var nodeAbsoluteTransform = jackOLanternNode.AbsoluteTransform;
        for (var i = 0; i < 4; i++)
        {
            var radians = i * (float)Math.PI / 2f;
            var blockMesh = new CollisionGeometryBuilder();
            blockMesh.Append(
                jackOLanternNode.Primitives[0],
                nodeAbsoluteTransform * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f)
            );
            blockMesh.Append(
                jackOLanternNode.Primitives[0],
                nodeAbsoluteTransform * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f)
            );
            blockPositionsByData[i] = blockMesh;
        }

        CollisionBoxes[0] = blockPositionsByData[0].GetBounds();
        base.Initialize();
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var forward = eyeForward;
        var num = Vector3.Dot(forward, Vector3.UnitZ);
        var num2 = Vector3.Dot(forward, Vector3.UnitX);
        var num3 = Vector3.Dot(forward, -Vector3.UnitZ);
        var num4 = Vector3.Dot(forward, -Vector3.UnitX);
        var data = 0;
        if (num.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 0;
        }
        else if (num2.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 1;
        }
        else if (num3.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 2;
        }
        else if (num4.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            data = 3;
        }

        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
    }


    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }
}

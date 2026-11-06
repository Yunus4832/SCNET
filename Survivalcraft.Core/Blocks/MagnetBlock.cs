using Game.Blocks.Collision;

namespace Game.Blocks;

public class MagnetBlock : Block
{
    public readonly BoundingBox[][] CollisionBoxesByData = new BoundingBox[2][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Magnet.glb");

    public override void Initialize()
    {
        var meshesByData = new CollisionGeometryBuilder[2];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var magnetNode = model.GetNode("Magnet")!;
        var nodeAbsoluteTransform = magnetNode.AbsoluteTransform;
        for (var i = 0; i < 2; i++)
        {
            meshesByData[i] = new CollisionGeometryBuilder();
            meshesByData[i].Append(
                magnetNode.Primitives[0],
                nodeAbsoluteTransform *
                Matrix.CreateRotationY((float)Math.PI / 2f * i) *
                Matrix.CreateTranslation(0.5f, 0f, 0.5f)
            );
            CollisionBoxesByData[i] = [meshesByData[i].GetBounds()];
        }

        base.Initialize();
    }


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num < CollisionBoxesByData.Length ? CollisionBoxesByData[num] : [];
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result;
        if (subsystemTerrain.Project.FindSubsystem<SubsystemMagnetBlockBehavior>(true)!.MagnetsCount < 8)
        {
            var forward = eyeForward;
            var data = !(MathUtils.Abs(forward.X) > MathUtils.Abs(forward.Z)) ? 1 : 0;
            result = default;
            result.CellFace = raycastResult.CellFace;
            result.Value = Terrain.ReplaceData(value, data);
            return result;
        }

        result = new BlockPlacementData { Rejection = BlockPlacementRejection.MagnetLimit };
        return result;
    }
}

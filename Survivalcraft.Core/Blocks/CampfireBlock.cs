using Game.Blocks.Collision;

namespace Game.Blocks;

public class CampfireBlock : Block
{
    public readonly BoundingBox[][] CollisionBoxesByData = new BoundingBox[16][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Campfire.glb");

    public override void Initialize()
    {
        var meshesByData = new CollisionGeometryBuilder[16];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var woodNode = model.GetNode("Wood")!;
        var ashesNode = model.GetNode("Ashes")!;
        var nodeAbsoluteTransform = woodNode.AbsoluteTransform;
        var nodeAbsoluteTransform2 = ashesNode.AbsoluteTransform;
        for (var i = 0; i < 16; i++)
        {
            meshesByData[i] = new CollisionGeometryBuilder();
            if (i == 0)
            {
                meshesByData[i].Append(
                    ashesNode.Primitives[0],
                    nodeAbsoluteTransform2 * Matrix.CreateScale(3f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                );
            }
            else
            {
                var scale = MathUtils.Lerp(1.5f, 4f, i / 15f);
                var radians = i * (float)Math.PI / 2f;
                meshesByData[i].Append(
                    woodNode.Primitives[0],
                    nodeAbsoluteTransform *
                    Matrix.CreateScale(scale) *
                    Matrix.CreateRotationY(radians) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                );
                meshesByData[i].Append(
                    ashesNode.Primitives[0],
                    nodeAbsoluteTransform2 *
                    Matrix.CreateScale(scale) *
                    Matrix.CreateRotationY(radians) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                );
            }

            var boundingBox = meshesByData[i].GetBounds();
            var min = boundingBox.Min;
            min.X = MathUtils.Saturate(min.X);
            min.Y = MathUtils.Saturate(min.Y);
            min.Z = MathUtils.Saturate(min.Z);
            boundingBox.Min = min;
            var max = boundingBox.Max;
            max.X = MathUtils.Saturate(max.X);
            max.Y = MathUtils.Saturate(max.Y);
            max.Z = MathUtils.Saturate(max.Z);
            boundingBox.Max = max;
            CollisionBoxesByData[i] =
            [
                boundingBox
            ];
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
        BlockPlacementData result = default;
        result.CellFace = raycastResult.CellFace;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, 3);
        return result;
    }

    public override bool ShouldAvoid(int value)
    {
        return Terrain.ExtractData(value) > 0;
    }

    public override int GetEmittedLightAmount(int value)
    {
        var num = Terrain.ExtractData(value);
        return num > 0 ? MathUtils.Min(8 + num / 2, 15) : 0;
    }

    public override float GetHeat(int value)
    {
        return Terrain.ExtractData(value) <= 0 ? 0f : base.GetHeat(value);
    }
}

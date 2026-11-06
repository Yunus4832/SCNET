using Game.Blocks.Collision;

namespace Game.Blocks;

public abstract class LadderBlock(string modelName, float offset) : Block
{
    public readonly BoundingBox[][] CollisionBoxesByData = new BoundingBox[4][];


    public float Offset = offset;


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:" + modelName + ".glb");

    public override void Initialize()
    {
        var blockPositionsByData = new CollisionGeometryBuilder[4];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var ladderNode = model.GetNode("Ladder")!;
        var nodeAbsoluteTransform = ladderNode.AbsoluteTransform;
        for (var i = 0; i < 4; i++)
        {
            blockPositionsByData[i] = new CollisionGeometryBuilder();
            var m = Matrix.CreateTranslation(0f, 0f, 0f - (0.5f - Offset)) *
                    Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            blockPositionsByData[i].Append(ladderNode.Primitives[0], nodeAbsoluteTransform * m);
            CollisionBoxesByData[i] = [blockPositionsByData[i].GetBounds()];
        }

        base.Initialize();
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        result.Value = raycastResult.CellFace.Face < 4
            ? Terrain.MakeBlockValue(BlockIndex, 0, SetFace(0, raycastResult.CellFace.Face))
            : 0;
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num < CollisionBoxesByData.Length
            ? CollisionBoxesByData[num]
            : base.GetCustomCollisionBoxes(terrain, value);
    }

    public static int GetFace(int data)
    {
        return data & 3;
    }

    public static int SetFace(int data, int face)
    {
        return (data & -4) | (face & 3);
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = true;
        var data = Terrain.ExtractData(value);
        return pistonFace == GetFace(data);
    }
}

using Game.Blocks.Collision;

namespace Game.Blocks;

public class TorchBlock : Block
{
    public BoundingBox[][] CollisionBoxes = new BoundingBox[5][];


    public override bool FurnitureBuilt { get; set; } = true;

    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Torch.glb");

    public override void Initialize()
    {
        var blockPositionsByVariant = new CollisionGeometryBuilder[5];
        for (var i = 0; i < blockPositionsByVariant.Length; i++)
        {
            blockPositionsByVariant[i] = new CollisionGeometryBuilder();
        }

        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var torchNode = model.GetNode("Torch")!;
        var nodeAbsoluteTransform = torchNode.AbsoluteTransform;
        var flameNode = model.GetNode("Flame")!;
        var nodeAbsoluteTransform2 = flameNode.AbsoluteTransform;
        var m = Matrix.CreateRotationX(0.6f) *
                Matrix.CreateRotationY(0f) *
                Matrix.CreateTranslation(0.5f, 0.15f, -0.05f);
        blockPositionsByVariant[0].Append(torchNode.Primitives[0], nodeAbsoluteTransform * m);
        blockPositionsByVariant[0].Append(flameNode.Primitives[0], nodeAbsoluteTransform2 * m);
        m = Matrix.CreateRotationX(0.6f) *
            Matrix.CreateRotationY((float)Math.PI / 2f) *
            Matrix.CreateTranslation(-0.05f, 0.15f, 0.5f);
        blockPositionsByVariant[1].Append(torchNode.Primitives[0], nodeAbsoluteTransform * m);
        blockPositionsByVariant[1].Append(flameNode.Primitives[0], nodeAbsoluteTransform2 * m);
        m = Matrix.CreateRotationX(0.6f) *
            Matrix.CreateRotationY((float)Math.PI) *
            Matrix.CreateTranslation(0.5f, 0.15f, 1.05f);
        blockPositionsByVariant[2].Append(torchNode.Primitives[0], nodeAbsoluteTransform * m);
        blockPositionsByVariant[2].Append(flameNode.Primitives[0], nodeAbsoluteTransform2 * m);
        m = Matrix.CreateRotationX(0.6f) *
            Matrix.CreateRotationY(4.712389f) *
            Matrix.CreateTranslation(1.05f, 0.15f, 0.5f);
        blockPositionsByVariant[3].Append(torchNode.Primitives[0], nodeAbsoluteTransform * m);
        blockPositionsByVariant[3].Append(flameNode.Primitives[0], nodeAbsoluteTransform2 * m);
        m = Matrix.CreateTranslation(0.5f, 0f, 0.5f);
        blockPositionsByVariant[4].Append(torchNode.Primitives[0], nodeAbsoluteTransform * m);
        blockPositionsByVariant[4].Append(flameNode.Primitives[0], nodeAbsoluteTransform2 * m);
        for (var j = 0; j < 5; j++)
        {
            CollisionBoxes[j] = new BoundingBox[1]
            {
                blockPositionsByVariant[j].GetBounds()
            };
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
        var value2 = 0;
        if (raycastResult.CellFace.Face == 0)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 0);
        }

        if (raycastResult.CellFace.Face == 1)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 1);
        }

        if (raycastResult.CellFace.Face == 2)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 2);
        }

        if (raycastResult.CellFace.Face == 3)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 3);
        }

        if (raycastResult.CellFace.Face == 4)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 4);
        }

        BlockPlacementData result = default;
        result.Value = value2;
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num < CollisionBoxes.Length ? CollisionBoxes[num] : [];
    }
}

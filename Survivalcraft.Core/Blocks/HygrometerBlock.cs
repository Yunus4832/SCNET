using Game.Blocks.Collision;

namespace Game.Blocks;

public class HygrometerBlock : Block, IElectricElementBlock
{
    public BoundingBox[][] CollisionBoxesByData = new BoundingBox[4][];

    public Matrix InvPointerMatrix;

    public Matrix[] MatricesByData = new Matrix[4];

    public Matrix PointerMatrix;


    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var num = Terrain.ExtractData(value);
        return new HygrometerElectricElement(subsystemElectricity, new CellFace(x, y, z, num & 3));
    }

    public ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        if ((Terrain.ExtractData(value) & 3) == face)
        {
            return ElectricConnectorType.Output;
        }

        return null;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Hygrometer.glb");

    public override void Initialize()
    {
        CollisionGeometryBuilder casePositions = new();
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var caseNode = model.GetNode("Case")!;
        var nodeAbsoluteTransform = caseNode.AbsoluteTransform;
        InvPointerMatrix = Matrix.Invert(PointerMatrix);
        casePositions.Append(caseNode.Primitives[0], nodeAbsoluteTransform);
        for (var i = 0; i < 4; i++)
        {
            MatricesByData[i] = Matrix.CreateScale(5f) *
                                Matrix.CreateTranslation(0.95f, 0.15f, 0.5f) *
                                Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                                Matrix.CreateRotationY((i + 1) * (float)Math.PI / 2f) *
                                Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            CollisionBoxesByData[i] = new BoundingBox[1]
            {
                casePositions.GetBounds(MatricesByData[i])
            };
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

        BlockPlacementData result = default;
        result.Value = value2;
        result.CellFace = raycastResult.CellFace;
        return result;
    }
}

using Game.Blocks.Collision;

namespace Game.Blocks;

public class ThermometerBlock : Block, IElectricElementBlock
{
    public BoundingBox[][] CollisionBoxesByData = new BoundingBox[4][];


    public Matrix[] MatricesByData = new Matrix[4];

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var num = Terrain.ExtractData(value);
        return new ThermometerElectricElement(subsystemElectricity, new CellFace(x, y, z, num & 3));
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

    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Thermometer.glb");

    public override void Initialize()
    {
        CollisionGeometryBuilder casePositions = new();
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var caseNode = model.GetNode("Case")!;
        var nodeAbsoluteTransform = caseNode.AbsoluteTransform;
        casePositions.Append(caseNode.Primitives[0], nodeAbsoluteTransform);
        for (var i = 0; i < 4; i++)
        {
            MatricesByData[i] = Matrix.CreateScale(1.5f) *
                                Matrix.CreateTranslation(0.95f, 0.15f, 0.5f) *
                                Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                                Matrix.CreateRotationY((i + 1) * (float)Math.PI / 2f) *
                                Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            CollisionBoxesByData[i] = [casePositions.GetBounds(MatricesByData[i])];
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
        var value2 = raycastResult.CellFace.Face switch
        {
            0 => Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 0),
            1 => Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 1),
            2 => Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 2),
            3 => Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 3),
            _ => 0
        };
        BlockPlacementData result = default;
        result.Value = value2;
        result.CellFace = raycastResult.CellFace;
        return result;
    }
}

using Game.Blocks.Collision;

namespace Game.Blocks;

public class PhotodiodeBlock : MountedElectricElementBlock
{
    public BoundingBox[][] CollisionBoxesByData = new BoundingBox[6][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Photodiode.glb");

    public override void Initialize()
    {
        var blockPositionsByData = new CollisionGeometryBuilder[6];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var photodiodeNode = model.GetNode("Photodiode")!;
        var nodeAbsoluteTransform = photodiodeNode.AbsoluteTransform;
        for (var i = 0; i < 6; i++)
        {
            var num = i;
            var m = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            blockPositionsByData[num] = new CollisionGeometryBuilder();
            blockPositionsByData[num].Append(photodiodeNode.Primitives[0], nodeAbsoluteTransform * m);
            CollisionBoxesByData[num] = [blockPositionsByData[num].GetBounds()];
        }
    }


    public override int GetFace(int value)
    {
        return Terrain.ExtractData(value) & 7;
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(value, raycastResult.CellFace.Face);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num >= CollisionBoxesByData.Length ? [] : CollisionBoxesByData[num];
    }


    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new PhotodiodeElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
    }

    public override ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        var face2 = GetFace(value);
        if (face == face2 && SubsystemElectricity.GetConnectorDirection(face2, 0, connectorFace).HasValue)
        {
            return ElectricConnectorType.Output;
        }

        return null;
    }
}

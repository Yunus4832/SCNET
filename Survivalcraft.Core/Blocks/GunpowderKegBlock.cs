using Game.Blocks.Collision;

namespace Game.Blocks;

public abstract class GunpowderKegBlock(string modelName, bool isIncendiary) : Block, IElectricElementBlock
{
    public BoundingBox[] CollisionBoxes = [];

    public bool IsIncendiary = isIncendiary;


    public Vector3 FuseOffset { get; set; }

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new GunpowderKegElectricElement(subsystemElectricity, new CellFace(x, y, z, 4));
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
        if (face == 4)
        {
            return ElectricConnectorType.Input;
        }

        return null;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:" + modelName + ".glb");

    public override void Initialize()
    {
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var kegNode = model.GetNode("Keg")!;
        var nodeAbsoluteTransform = kegNode.AbsoluteTransform;
        var fuseNode = model.GetNode("Fuse")!;
        FuseOffset = fuseNode.AbsoluteTransform.Translation +
                     new Vector3(0.5f, 0f, 0.5f);
        var blockMesh = new CollisionGeometryBuilder();
        blockMesh.Append(kegNode.Primitives[0], nodeAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f));
        CollisionBoxes = [blockMesh.GetBounds()];
        base.Initialize();
    }


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
    }
}

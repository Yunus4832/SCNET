namespace Game.Blocks;

public class FallenLeavesBlock : CubeBlock
{
    private const float _height = 0.0625f;

    private readonly BoundingBox[] _collisionBoxes = [new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.0625f, 1f))];


    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        showDebris = true;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return _collisionBoxes;
    }
}

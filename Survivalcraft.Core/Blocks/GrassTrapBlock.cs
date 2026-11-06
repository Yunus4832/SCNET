namespace Game.Blocks;

public class GrassTrapBlock : Block
{
    public readonly BoundingBox[] CollisionBoxes =
        [new BoundingBox(new Vector3(0f, 0.75f, 0f), new Vector3(1f, 0.95f, 1f))];


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
    }
}

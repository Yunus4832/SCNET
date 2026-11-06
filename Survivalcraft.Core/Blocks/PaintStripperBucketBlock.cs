namespace Game.Blocks;

public class PaintStripperBucketBlock : BucketBlock
{
    public override int GetDamageDestructionValue(int value)
    {
        return Terrain.MakeBlockValue(90);
    }
}

namespace Game.Blocks;

public class MilkBucketBlock : BucketBlock
{
    public override int GetDamageDestructionValue(int value)
    {
        return 245;
    }
}

namespace Game.Blocks;

public class SaplingBlock : CrossBlock
{
    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0);
        yield return Terrain.MakeBlockValue(BlockIndex, 1);
        yield return Terrain.MakeBlockValue(BlockIndex, 2);
        yield return Terrain.MakeBlockValue(BlockIndex, 3);
        yield return Terrain.MakeBlockValue(BlockIndex, 4);
        yield return Terrain.MakeBlockValue(BlockIndex, 5);
    }
}

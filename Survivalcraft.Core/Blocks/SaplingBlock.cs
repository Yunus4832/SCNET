namespace Game.Blocks;

public class SaplingBlock : CrossBlock
{
    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, 0);
        yield return Terrain.MakeBlockValue(BlockIndex, 0, 1);
        yield return Terrain.MakeBlockValue(BlockIndex, 0, 2);
        yield return Terrain.MakeBlockValue(BlockIndex, 0, 3);
        yield return Terrain.MakeBlockValue(BlockIndex, 0, 4);
        yield return Terrain.MakeBlockValue(BlockIndex, 0, 5);
    }
}

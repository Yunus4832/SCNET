namespace Game.Blocks;

public class DirtStairsBlock() : StairsBlock(2)
{
    public const int Index = 260;

    public override int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        return value;
    }

    public override int? GetPaintColor(int value)
    {
        return null;
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SlabBlock.SetColor(0, null));
    }
}

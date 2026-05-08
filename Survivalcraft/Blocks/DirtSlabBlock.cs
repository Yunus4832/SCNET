namespace Game.Blocks;

public class DirtSlabBlock() : SlabBlock(2, 2)
{
    public const int Index = 259;

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
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, null));
    }
}

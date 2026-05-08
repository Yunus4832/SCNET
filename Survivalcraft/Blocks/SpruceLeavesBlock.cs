namespace Game.Blocks;

public class SpruceLeavesBlock : EvergreenLeavesBlock
{
    public const int Index = 14;

    public override Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z)
    {
        return BlockColorsMap.SpruceLeavesColorsMap.Lookup(terrain, x, y, z);
    }

    public override Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environmentData)
    {
        return BlockColorsMap.SpruceLeavesColorsMap.Lookup(environmentData);
    }
}

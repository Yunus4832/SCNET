namespace Game.Blocks;

public class MagmaBlock() : FluidBlock(MagmaMaxLevel)
{
    public const int MagmaMaxLevel = 4;

    public override bool FurnitureBuilt { get; set; } = true;

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        if (GetIsTop(Terrain.ExtractData(value)))
        {
            return face != 5;
        }

        return false;
    }


    public override bool ShouldAvoid(int value)
    {
        return true;
    }
}

namespace Game.Blocks;

public abstract class FoodBlock(int rottenValue) : Block
{
    public static int CompostValue = Terrain.MakeBlockValue(
        168,
        0,
        SoilBlock.SetHydration(SoilBlock.SetNitrogen(0, 1), false)
    );


    public int RottenValue = rottenValue;


    public override int GetDamageDestructionValue(int value)
    {
        return RottenValue;
    }
}

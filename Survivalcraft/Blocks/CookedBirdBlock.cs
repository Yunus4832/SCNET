namespace Game.Blocks;

public class CookedBirdBlock() : FoodBlock(
    "Models/Bird",
    Matrix.Identity,
    new Color(150, 69, 15),
    239
)
{
    public const int Index = 78;
}

namespace Game.Blocks;

public class CookedMeatBlock() : FoodBlock(
    "Models/Meat",
    Matrix.Identity,
    new Color(155, 122, 51),
    240
)
{
    public const int Index = 89;
}

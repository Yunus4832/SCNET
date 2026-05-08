namespace Game.Blocks;

public class RottenMeatBlock() : FoodBlock(
    "Models/Meat",
    Matrix.CreateTranslation(-0.0625f, 0f, 0f),
    Color.White,
    CompostValue
)
{
    public const int Index = 240;
}

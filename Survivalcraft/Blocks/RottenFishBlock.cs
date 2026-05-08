namespace Game.Blocks;

public class RottenFishBlock() : FoodBlock(
    "Models/Fish",
    Matrix.CreateTranslation(-0.125f, 0.125f, 0f),
    Color.White,
    CompostValue
)
{
    public const int Index = 241;
}

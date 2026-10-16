namespace Game.Blocks;

public class RottenBirdBlock() : FoodBlock(
    "Models/Bird",
    Matrix.CreateTranslation(-0.9375f, 0.4375f, 0f),
    Color.White,
    CompostValue
)
{
}

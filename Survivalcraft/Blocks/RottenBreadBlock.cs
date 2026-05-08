namespace Game.Blocks;

public class RottenBreadBlock() : FoodBlock(
    "Models/Bread",
    Matrix.CreateTranslation(-0.375f, -0.25f, 0f),
    Color.White,
    CompostValue
)
{
    public const int Index = 242;
}

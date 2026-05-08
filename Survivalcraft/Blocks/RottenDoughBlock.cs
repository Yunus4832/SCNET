namespace Game.Blocks;

public class RottenDoughBlock() : FoodBlock(
    "Models/Bread",
    Matrix.CreateTranslation(-0.375f, -0.25f, 0f),
    new Color(192, 255, 212),
    CompostValue
)
{
    public const int Index = 247;
}

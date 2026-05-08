namespace Game.Blocks;

public class DoughBlock() : FoodBlock(
    "Models/Bread",
    Matrix.CreateTranslation(0.5625f, -0.875f, 0f),
    new Color(241, 231, 214),
    247
)
{
    public const int Index = 176;
}

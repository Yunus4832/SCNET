namespace Game.Furniture;

public sealed class FurnitureSetDefault : FurnitureSet
{
    public static readonly FurnitureSetDefault Default = new();

    private FurnitureSetDefault()
    {
    }
}

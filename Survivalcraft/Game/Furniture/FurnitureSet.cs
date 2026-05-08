namespace Game;

public class FurnitureSet
{
    public string ImportedFrom = string.Empty;

    public string Name = string.Empty;
}

public sealed class FurnitureSetDefault : FurnitureSet
{
    public static readonly FurnitureSetDefault Default = new();

    private FurnitureSetDefault()
    {
    }
};

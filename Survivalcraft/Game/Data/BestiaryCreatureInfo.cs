namespace Game;

public class BestiaryCreatureInfo
{
    public float AttackPower;

    public float AttackResilience;

    public bool CanBeRidden;

    public string? DescriptionKey;

    public string Description
    {
        get => DescriptionKey is null ? field : LanguageManager.GetDatabase("Description", DescriptionKey);
        set;
    } = string.Empty;

    public string? DisplayNameKey;

    public string DisplayName
    {
        get => DisplayNameKey is null ? field : LanguageManager.GetDatabase("DisplayName", DisplayNameKey);
        set;
    } = string.Empty;

    public bool HasSpawnerEgg;

    public bool IsHerding;

    public float JumpHeight;

    public List<ComponentLoot.Loot> Loot = [];

    public float Mass;

    public string ModelName = string.Empty;

    public float MovementSpeed;

    public int Order;

    public string TextureOverride = string.Empty;
}

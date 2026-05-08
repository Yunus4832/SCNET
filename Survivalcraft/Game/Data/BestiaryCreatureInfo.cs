namespace Game;

public class BestiaryCreatureInfo
{
    public float AttackPower;

    public float AttackResilience;

    public bool CanBeRidden;

    public string Description = string.Empty;

    public string DisplayName = string.Empty;

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

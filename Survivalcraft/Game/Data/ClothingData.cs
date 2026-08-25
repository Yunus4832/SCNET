using Engine.Graphics;

namespace Game;

public class ClothingData
{
    private string _description = string.Empty;

    private string _displayName = string.Empty;

    public float ArmorProtection;

    public bool CanBeDyed;

    public float DensityModifier;

    public string Description
    {
        get => LanguageManager.TryGetBlock($"ClothingBlock:{Index}", nameof(Description), out var value)
            ? value!
            : _description;
        set => _description = value;
    }

    public int DisplayIndex;

    public string DisplayName
    {
        get => LanguageManager.TryGetBlock($"ClothingBlock:{Index}", nameof(DisplayName), out var value)
            ? value!
            : _displayName;
        set => _displayName = value;
    }

    public string ImpactSoundsFolder = string.Empty;

    public int Index;

    public float Insulation;

    public bool IsOuter;

    public int Layer;

    public float MovementSpeedFactor;

    public int PlayerLevelRequired;

    public ClothingSlot Slot;

    public float SteedMovementSpeedFactor;

    public float Sturdiness;

    public required Texture2D Texture;
}

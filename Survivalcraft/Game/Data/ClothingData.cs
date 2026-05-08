using Engine.Graphics;

namespace Game;

public class ClothingData
{
    public float ArmorProtection;

    public bool CanBeDyed;

    public float DensityModifier;

    public string Description = string.Empty;

    public int DisplayIndex;

    public string DisplayName = string.Empty;

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

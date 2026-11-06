namespace Game.Characters;

/// <summary>World control intent after client device, camera and UI filtering.</summary>
public struct CharacterControlInput
{
    public Vector2 Look;

    public Vector3 Move;

    public Vector3 SneakMove;

    public bool Jump;

    public bool AutoJumpEnabled;

    public int ScrollInventory;

    public int ScrollInventoryPage;

    public int? SelectInventoryPage;

    public int? SelectInventorySlot;

    public Ray3? Dig;

    public Ray3? Hit;

    public Ray3? Aim;

    public Ray3? Interact;

    public Ray3? PickBlockType;

    public bool Drop;

    public bool SuppressLookWhileAiming;

    public bool IsAimDirectionAllowed;

    public bool FlipLookY;

    public bool ContinuousDig;

    public bool UseLookDirectedCreativeFlight;
}

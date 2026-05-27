namespace Game.Network.Enums;

[Flags]
public enum InteractType : byte
{
    None = 0,
    Interact = 1,
    Hit = 2,
    Aim = 4,
    Dig = 8,
    EndDig = 16,
    EndAim = 32,
    CancelAim = 64
}

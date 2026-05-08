namespace Game.Network;

public enum NetworkChannel : byte
{
    Control   = 0,
    Input     = 1,
    Entity    = 2,
    Subsystem = 3,
    Terrain   = 4,
    Event     = 5,
    Mod       = 6,
}

namespace Game.World;

/// <summary>Role of the current world runtime, independent of GUI mode or transport connectivity.</summary>
public enum WorldRole
{
    SinglePlayer = 0,

    Server = 1,

    RemoteClient = 2,

    None = 3
}

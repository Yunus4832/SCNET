namespace Game.Network;

/// <summary>Message endpoint activation; independent of transport connectivity and GUI capabilities.</summary>
public enum NodeRole
{
    Authority = 1,

    Client = 2,

    Inactive = 3
}

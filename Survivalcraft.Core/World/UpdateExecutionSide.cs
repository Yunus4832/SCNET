namespace Game.World;

/// <summary>Declares which world owns an updateable's frame work.</summary>
public enum UpdateExecutionSide
{
    Disabled = 0,

    Authority = 1,

    Client = 2,

    Both = 3,

    Presentation = 4
}

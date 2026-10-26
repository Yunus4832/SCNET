namespace Game.Servers;

public enum P2PRoomState
{
    Disabled = 0,

    Registering = 1,

    Active = 2,

    RetryWaiting = 3,

    Stopping = 4
}

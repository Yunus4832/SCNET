namespace Game.Network;

/// <summary>Live connectivity projected by the actual transport connection.</summary>
public interface IConnectionLiveness
{
    bool IsConnected { get; }
}

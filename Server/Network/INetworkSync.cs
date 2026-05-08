namespace Game.Network;

public interface INetworkSync
{
    bool IsDirty { get; }
    NetworkChannel Channel { get; }
    void WriteDirtyState(INetworkWriter writer);
    void ClearDirty();
}

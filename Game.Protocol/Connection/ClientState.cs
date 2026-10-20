namespace Game.Protocol.Connection;

public enum ClientState
{
    NotConnected,
    Connected,
    ProjectLoaded,
    LoadTerrain,
    Playing
}

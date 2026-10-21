namespace Game.Protocol.Connection;

public enum ClientState
{
    NotConnected = 0,

    Connected = 1,

    ProjectLoaded = 2,

    LoadTerrain = 3,

    Playing = 4
}

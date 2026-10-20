namespace Game.Protocol.Connection;

public sealed record ClientIdentity(byte ID, Guid TokenId, Guid GUID, ClientState State = ClientState.NotConnected);

namespace Game.Network;

public interface ITransportProvider
{
    bool IsServer { get; }
    void Start(int port);
    void Stop();
    void Send(byte clientId, byte[] data, NetworkChannel channel);
    void SendToAll(byte[] data, NetworkChannel channel, byte? exceptClientId = null);
    event Action<byte, NetworkChannel, byte[]>? OnChannelDataReceived;
    event Action<byte>? OnClientConnected;
    event Action<byte, string>? OnClientDisconnected;
}

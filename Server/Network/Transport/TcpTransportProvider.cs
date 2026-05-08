using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Game.Network.Transport;

public class TcpTransportProvider : ITransportProvider, IDisposable
{
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<byte, TcpClient> _clients = new();
    private readonly ConcurrentDictionary<TcpClient, byte> _clientIds = new();
    private byte _nextClientId;
    private bool _isRunning;
    private int _listeningPort;

    public bool IsServer { get; private set; }
    public int ListeningPort => _listeningPort;

    public event Action<byte, NetworkChannel, byte[]>? OnChannelDataReceived;
    public event Action<byte>? OnClientConnected;
    public event Action<byte, string>? OnClientDisconnected;

    public void Start(int port)
    {
        IsServer = true;
        _listeningPort = port;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _isRunning = true;
        _ = AcceptClientsAsync();
    }

    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
        foreach (var client in _clients.Values)
        {
            client.Close();
        }
        _clients.Clear();
        _clientIds.Clear();
    }

    public void Send(byte clientId, byte[] data, NetworkChannel channel)
    {
        if (_clients.TryGetValue(clientId, out var client))
        {
            try
            {
                SendFrame(client, channel, data);
            }
            catch
            {
                // Client disconnected
            }
        }
    }

    public void SendToAll(byte[] data, NetworkChannel channel, byte? exceptClientId = null)
    {
        foreach (var (id, client) in _clients)
        {
            if (id == exceptClientId)
            {
                continue;
            }

            try
            {
                SendFrame(client, channel, data);
            }
            catch
            {
                // Client disconnected
            }
        }
    }

    public void Connect(string host, int port)
    {
        IsServer = false;
        _isRunning = true;
        var client = new TcpClient();
        client.Connect(host, port);
        byte clientId = 0;
        _clients.TryAdd(clientId, client);
        _clientIds.TryAdd(client, clientId);
        _ = ReceiveFromClientAsync(client);
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task AcceptClientsAsync()
    {
        while (_isRunning && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                var clientId = _nextClientId++;
                _clients.TryAdd(clientId, client);
                _clientIds.TryAdd(client, clientId);
                OnClientConnected?.Invoke(clientId);
                _ = ReceiveFromClientAsync(client);
            }
            catch
            {
                break;
            }
        }
    }

    private async Task ReceiveFromClientAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var lengthBuffer = new byte[4];

        while (_isRunning && client.Connected)
        {
            try
            {
                await stream.ReadExactlyAsync(lengthBuffer, 0, 4);
                var frameLength = BitConverter.ToInt32(lengthBuffer, 0);

                var frameBuffer = new byte[frameLength];
                await stream.ReadExactlyAsync(frameBuffer, 0, frameLength);

                var channel = (NetworkChannel)frameBuffer[0];
                var data = new byte[frameLength - 1];
                Array.Copy(frameBuffer, 1, data, 0, frameLength - 1);

                if (_clientIds.TryGetValue(client, out var clientId))
                {
                    OnChannelDataReceived?.Invoke(clientId, channel, data);
                }
            }
            catch
            {
                break;
            }
        }

        if (_clientIds.TryRemove(client, out var id))
        {
            _clients.TryRemove(id, out _);
            OnClientDisconnected?.Invoke(id, "Connection lost");
        }
    }

    private static void SendFrame(TcpClient client, NetworkChannel channel, byte[] data)
    {
        var stream = client.GetStream();
        var frameLength = data.Length + 1;
        var lengthBytes = BitConverter.GetBytes(frameLength);
        stream.Write(lengthBytes, 0, 4);
        stream.WriteByte((byte)channel);
        stream.Write(data, 0, data.Length);
        stream.Flush();
    }
}

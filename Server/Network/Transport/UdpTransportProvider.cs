using System.Net;
using System.Net.Sockets;

namespace Game.Network.Transport;

public class UdpTransportProvider : ITransportProvider, IDisposable
{
    private UdpClient? _socket;
    private readonly Dictionary<byte, IPEndPoint> _clients = new();
    private byte _nextClientId;
    private bool _isRunning;

    public bool IsServer { get; private set; }

    public event Action<byte, NetworkChannel, byte[]>? OnChannelDataReceived;
    public event Action<byte>? OnClientConnected;
    public event Action<byte, string>? OnClientDisconnected;

    public void Start(int port)
    {
        IsServer = true;
        _socket = new UdpClient(port);
        _isRunning = true;
        _ = ReceiveLoopAsync();
    }

    public void Stop()
    {
        _isRunning = false;
        _socket?.Close();
        _socket = null;
        _clients.Clear();
    }

    public void Send(byte clientId, byte[] data, NetworkChannel channel)
    {
        if (_socket != null && _clients.TryGetValue(clientId, out var endpoint))
        {
            SendFrameTo(endpoint, channel, data);
        }
    }

    public void SendToAll(byte[] data, NetworkChannel channel, byte? exceptClientId = null)
    {
        if (_socket == null)
        {
            return;
        }

        if (_clients.Count > 0)
        {
            foreach (var (id, endpoint) in _clients)
            {
                if (id == exceptClientId)
                {
                    continue;
                }

                SendFrameTo(endpoint, channel, data);
            }
        }
        else if (!IsServer)
        {
            SendFrameConnected(channel, data);
        }
    }

    public void Connect(string host, int port)
    {
        IsServer = false;
        _socket = new UdpClient();
        _socket.Connect(host, port);
        _isRunning = true;
        _ = ReceiveLoopAsync();
    }

    public void Dispose()
    {
        Stop();
    }

    private void SendFrameTo(IPEndPoint endpoint, NetworkChannel channel, byte[] data)
    {
        if (_socket == null)
        {
            return;
        }

        var frame = BuildFrame(channel, data);
        _socket.Send(frame, frame.Length, endpoint);
    }

    private void SendFrameConnected(NetworkChannel channel, byte[] data)
    {
        if (_socket == null)
        {
            return;
        }

        var frame = BuildFrame(channel, data);
        _socket.Send(frame, frame.Length);
    }

    private static byte[] BuildFrame(NetworkChannel channel, byte[] data)
    {
        var frameLength = data.Length + 1;
        var frame = new byte[4 + frameLength];
        BitConverter.GetBytes(frameLength).CopyTo(frame, 0);
        frame[4] = (byte)channel;
        Array.Copy(data, 0, frame, 5, data.Length);
        return frame;
    }

    private async Task ReceiveLoopAsync()
    {
        while (_isRunning && _socket != null)
        {
            try
            {
                var result = await _socket.ReceiveAsync();
                ProcessReceivedData(result.RemoteEndPoint, result.Buffer);
            }
            catch
            {
                break;
            }
        }
    }

    private void ProcessReceivedData(IPEndPoint remoteEndPoint, byte[] buffer)
    {
        if (buffer.Length < 5)
        {
            return;
        }

        var frameLength = BitConverter.ToInt32(buffer, 0);
        if (frameLength != buffer.Length - 4)
        {
            return;
        }

        var channel = (NetworkChannel)buffer[4];
        var data = new byte[frameLength - 1];
        Array.Copy(buffer, 5, data, 0, frameLength - 1);

        if (!_clients.ContainsValue(remoteEndPoint))
        {
            var clientId = _nextClientId++;
            _clients[clientId] = remoteEndPoint;
            OnClientConnected?.Invoke(clientId);
        }

        var id = _clients.First(kv => kv.Value.Equals(remoteEndPoint)).Key;
        OnChannelDataReceived?.Invoke(id, channel, data);
    }
}

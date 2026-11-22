using System.Net;

namespace Engine.Network;

/// <summary>宿主持有的回复路径，包含接收该数据报的套接字。</summary>
public sealed class NetworkDatagramEndpoint
{
    private readonly NetworkHost _host;

    internal IPEndPoint Endpoint { get; }

    internal NetworkDatagramEndpoint(NetworkHost host, IPEndPoint endpoint, bool isBroadcast = false)
    {
        _host = host;
        Endpoint = endpoint;
        IsBroadcast = isBroadcast;
    }

    public bool IsBroadcast { get; }

    public IPEndPoint TransportEndpoint => new(Endpoint.Address, Endpoint.Port);

    public void Send(ReadOnlySpan<byte> payload) => _host.SendDatagram(Endpoint, payload);
}

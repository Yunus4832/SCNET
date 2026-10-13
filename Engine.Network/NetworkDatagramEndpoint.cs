using System.Net;

namespace Engine.Network;

/// <summary>A reply route owned by a host, including the socket used to receive the datagram.</summary>
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

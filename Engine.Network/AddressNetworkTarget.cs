namespace Engine.Network;

public sealed record AddressNetworkTarget : NetworkTarget
{
    public AddressNetworkTarget(string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        Host = host.Trim();
        Port = port;
    }

    public string Host { get; }

    public int Port { get; }
}

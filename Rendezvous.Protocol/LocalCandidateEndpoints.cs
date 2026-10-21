using System.Net;
using System.Net.Sockets;

namespace Rendezvous.Protocol;

/// <summary>Bounds advertised interface endpoints; reachability still requires a peer challenge.</summary>
public static class LocalCandidateEndpoints
{
    public const int MaxCount = 4;

    public static bool IsPrivateAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => bytes[0] == 10 ||
                                          bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                                          bytes[0] == 192 && bytes[1] == 168,
            AddressFamily.InterNetworkV6 => (bytes[0] & 0xfe) == 0xfc,
            _ => false
        };
    }

    public static bool IsGlobalIPv6Address(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 &&
        address.ScopeId == 0 &&
        (address.GetAddressBytes()[0] & 0xe0) == 0x20;

    public static bool IsCandidateAddress(IPAddress address) =>
        (address.AddressFamily != AddressFamily.InterNetworkV6 || address.ScopeId == 0) &&
        (IsPrivateAddress(address) || IsGlobalIPv6Address(address));

    public static bool TryParse(IReadOnlyList<string>? candidates, AddressFamily? family, out IPEndPoint[] endpoints)
    {
        endpoints = [];
        if (candidates is null)
        {
            return true;
        }

        if (candidates.Count > MaxCount)
        {
            return false;
        }

        var parsed = new List<IPEndPoint>();
        foreach (var candidate in candidates)
        {
            if (candidate is null or { Length: > 128 } ||
                !IPEndPoint.TryParse(candidate, out var endpoint) ||
                endpoint.Port == 0 ||
                !IsCandidateAddress(endpoint.Address) ||
                family is not null && endpoint.AddressFamily != family ||
                parsed.Contains(endpoint))
            {
                return false;
            }

            parsed.Add(endpoint);
        }

        endpoints = parsed.ToArray();
        return true;
    }
}

using System.Globalization;
using System.Net;
using System.Net.Sockets;

using Engine.Network;

namespace NetworkProbe;

internal sealed record ProbeBinding(IPAddress IPv4Address, IPAddress IPv6Address, int Port)
{
    public static ProbeBinding Parse(ref string[] arguments, out string? outputDirectory)
    {
        var positional = new List<string>();
        var ipv4 = IPAddress.Any;
        var ipv6 = IPAddress.IPv6Any;
        var port = 0;
        outputDirectory = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(argument);
                continue;
            }

            if (argument is not "--bind-ipv4" and not "--bind-ipv6" and not "--local-port" and not "--output" ||
                !seen.Add(argument) ||
                ++index >= arguments.Length)
            {
                throw new ArgumentException($"Unknown, repeated or incomplete option: {argument}");
            }

            var value = arguments[index];
            switch (argument)
            {
                case "--output":
                    ArgumentException.ThrowIfNullOrWhiteSpace(value);
                    outputDirectory = value;
                    break;
                case "--bind-ipv4":
                    ipv4 = ParseAddress(value, AddressFamily.InterNetwork);
                    break;
                case "--bind-ipv6":
                    ipv6 = ParseAddress(value, AddressFamily.InterNetworkV6);
                    break;
                case "--local-port":
                    port = int.Parse(value, CultureInfo.InvariantCulture);
                    ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(port, ushort.MaxValue);
                    break;
            }
        }

        arguments = positional.ToArray();
        if (port != 0 && arguments.FirstOrDefault()?.StartsWith("scenario", StringComparison.Ordinal) == true)
        {
            throw new ArgumentException(
                "Scenario children require independent ephemeral ports; use host/client modes to select fixed local ports."
            );
        }

        return new ProbeBinding(ipv4, ipv6, port);
    }

    public NetworkHost CreateHost() => new(
        Port,
        new NetworkHostOptions
        {
            LocalIPv4Address = IPv4Address,
            LocalIPv6Address = IPv6Address
        }
    );

    private static IPAddress ParseAddress(string value, AddressFamily family)
    {
        if (!IPAddress.TryParse(value, out var address) || address.AddressFamily != family)
        {
            throw new ArgumentException($"Invalid {family} binding address: {value}");
        }

        return address;
    }
}

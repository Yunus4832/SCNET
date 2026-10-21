using LiteNetLib;

namespace Engine.Network;

/// <summary>Configures the process-wide transport log output independently of host lifetimes.</summary>
public static class NetworkLogging
{
    private static Action<NetworkLogLevel, string>? _write;

    private static readonly LiteNetLog _logger = new((level, message) =>
        Volatile.Read(ref _write)?.Invoke(level, message)
    );

    public static void Configure(Action<NetworkLogLevel, string> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        Volatile.Write(ref _write, write);
        NetDebug.Logger = _logger;
    }
}

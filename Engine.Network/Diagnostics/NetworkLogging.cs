using LiteNetLib;

namespace Engine.Network.Diagnostics;

/// <summary>配置进程级传输日志输出，其生命周期独立于传输宿主。</summary>
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

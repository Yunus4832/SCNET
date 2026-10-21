using System.Collections.Concurrent;

using LiteNetLib;

namespace Engine.Network;

internal sealed class LiteNetLog(Action<NetworkLogLevel, string> write, TimeProvider? clock = null) : INetLogger
{
    private readonly ConcurrentDictionary<string, long> _lastNetworkWarning = new();

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public void WriteNet(NetLogLevel level, string message, params object[] args)
    {
        var formatted = FormatMessage(message, args);
        // LiteNetLib 0.9.5.2 renders send exceptions before invoking INetLogger.
        var firstLine = formatted.Split('\n', 2)[0].TrimEnd('\r');
        if (level == NetLogLevel.Error &&
            firstLine.StartsWith("[S]System.Net.Sockets.SocketException", StringComparison.Ordinal) &&
            (firstLine.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase) ||
             firstLine.Contains("No route to host", StringComparison.OrdinalIgnoreCase)))
        {
            var now = _clock.GetTimestamp();
            while (true)
            {
                if (!_lastNetworkWarning.TryGetValue(firstLine, out var previous))
                {
                    if (!_lastNetworkWarning.TryAdd(firstLine, now))
                    {
                        continue;
                    }
                }
                else
                {
                    if (_clock.GetElapsedTime(previous, now) < TimeSpan.FromSeconds(30))
                    {
                        return;
                    }

                    if (!_lastNetworkWarning.TryUpdate(firstLine, now, previous))
                    {
                        continue;
                    }
                }

                write(
                    NetworkLogLevel.Warning,
                    $"[LiteNetLib] {firstLine} (repeated warnings limited to once every 30 seconds)"
                );
                return;
            }
        }

        var logLevel = level switch
        {
            NetLogLevel.Trace => NetworkLogLevel.Debug,
            NetLogLevel.Warning => NetworkLogLevel.Warning,
            NetLogLevel.Error => NetworkLogLevel.Error,
            _ => NetworkLogLevel.Information
        };
        write(logLevel, $"[LiteNetLib]{level} {formatted}");
    }

    private static string FormatMessage(string message, object[] args)
    {
        if (args.Length == 0)
        {
            return message;
        }

        try
        {
            return string.Format(message, args);
        }
        catch (FormatException)
        {
            return message + " " + string.Join(" ", args);
        }
    }
}

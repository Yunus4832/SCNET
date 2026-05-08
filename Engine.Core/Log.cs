using System.Diagnostics;

namespace Engine.Core;

public static class Log
{
    private static readonly Lock _logLock = new();

    private static readonly List<ILogSink> _logSinks;

    public static Action<string>? MsgAdded;

    static Log()
    {
        _logSinks = [];
        MinimumLogType = LogType.Debug;
    }

    public static LogType MinimumLogType { get; set; }

    public static void AddLogMsg(string msg)
    {
        MsgAdded?.Invoke(msg);
    }

    private static void Write(LogType type, string? message)
    {
        lock (_logLock)
        {
            if (_logSinks.Count <= 0 || type < MinimumLogType)
            {
                return;
            }

            lock (_logLock)
            {
                foreach (var logSink in _logSinks)
                {
                    try
                    {
                        logSink.Log(type, message ?? "null");
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
    }

    [Conditional("DEBUG")]
    public static void Debug(object? message)
    {
        Write(LogType.Debug, message?.ToString());
    }

    [Conditional("DEBUG")]
    public static void Debug(string message)
    {
        Write(LogType.Debug, message);
    }

    [Conditional("DEBUG")]
    public static void Debug(string format, params object[] parameters)
    {
        Write(LogType.Debug, string.Format(format, parameters));
    }

    public static void Verbose(object? message)
    {
        Write(LogType.Verbose, message?.ToString());
    }

    public static void Verbose(string message)
    {
        Write(LogType.Verbose, message);
    }

    public static void Verbose(string format, params object[] parameters)
    {
        Write(LogType.Verbose, string.Format(format, parameters));
    }

    public static void Information(object? message)
    {
        Write(LogType.Information, message?.ToString());
    }

    public static void Information(string message)
    {
        Write(LogType.Information, message);
    }

    public static void Information(string format, params object[] parameters)
    {
        Write(LogType.Information, string.Format(format, parameters));
    }

    public static void Warning(object? message)
    {
        Write(LogType.Warning, message?.ToString());
    }

    public static void Warning(string message)
    {
        Write(LogType.Warning, message);
    }

    public static void Warning(string format, params object[] parameters)
    {
        Write(LogType.Warning, string.Format(format, parameters));
    }

    public static void Error(object? message)
    {
        Write(LogType.Error, message?.ToString());
    }

    public static void Error(string message)
    {
        Write(LogType.Error, message);
    }

    public static void Error(string format, params object[] parameters)
    {
        Write(LogType.Error, string.Format(format, parameters));
    }

    public static void AddLogSink(ILogSink logSink)
    {
        lock (_logLock)
        {
            if (!_logSinks.Contains(logSink))
            {
                _logSinks.Add(logSink);
            }
        }
    }

    public static void RemoveLogSink(ILogSink logSink)
    {
        lock (_logLock)
        {
            _logSinks.Remove(logSink);
        }
    }

    public static void RemoveAllLogSinks()
    {
        lock (_logLock)
        {
            _logSinks.Clear();
        }
    }
}

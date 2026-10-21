namespace Engine.Core.Logging;

public interface ILogSink
{
    void Log(LogType type, string message);
}

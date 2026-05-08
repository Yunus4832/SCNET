namespace Engine.Core;

public interface ILogSink
{
    void Log(LogType type, string message);
}

namespace Engine.Core;

public class StreamLogSink : ILogSink
{
    private readonly StreamWriter _writer;

    public StreamLogSink(Stream stream)
    {
        _writer = new StreamWriter(stream);
        stream.Position = stream.Length;
    }

    public LogType MinimumLogType { get; set; }

    public void Log(LogType logType, string message)
    {
        if (logType < MinimumLogType)
        {
            return;
        }

        var str = string.Empty;
        switch (logType)
        {
            case LogType.Debug:
                str = "DEBUG: ";
                break;
            case LogType.Verbose:
            case LogType.Information:
                str = "INFO: ";
                break;
            case LogType.Warning:
                str = "WARNING: ";
                break;
            case LogType.Error:
                str = "ERROR: ";
                break;
            default:
                break;
        }

        _writer.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + str + message);
        _writer.Flush();
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}

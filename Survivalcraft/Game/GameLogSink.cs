namespace Game;

/// <summary>
/// 游戏日志接收器，负责将日志写入文件并管理日志文件
/// </summary>
public class GameLogSink : ILogSink
{
    /// <summary>
    /// 触发清理的日志文件数量阈值。当日志文件数量达到此值时，会触发清理操作
    /// </summary>
    private const int _cleanupThreshold = 10;

    /// <summary>
    /// 清理后保留的日志文件数量
    /// </summary>
    private const int _keepLogFiles = 2;

    /// <summary>
    /// 单个日志文件的最大大小限制（2MB）超过此限制将截断文件
    /// </summary>
    private const long _maxFileSize = 2097152;

    /// <summary>
    /// 当前日志文件的流实例
    /// </summary>
    private static Stream? _stream;

    /// <summary>
    /// 用于写入日志的 StreamWriter 实例
    /// </summary>
    private static StreamWriter _writer = null!;

    /// <summary>
    /// 初始化 <see cref="GameLogSink"/> 的新实例
    /// 创建日志目录，清理旧日志文件，并打开或创建当天的日志文件
    /// </summary>
    /// <exception cref="InvalidOperationException">当 GameLogSink 实例已存在时抛出</exception>
    public GameLogSink()
    {
        try
        {
            if (_stream != null)
            {
                throw new InvalidOperationException("GameLogSink already created.");
            }

            Storage.CreateDirectory(GamePaths.Logs);
            CleanupOldLogFiles();
            var path = GetLogFilePath(out var shouldTruncate);
            _stream = Storage.OpenFile(path, shouldTruncate ? OpenFileMode.Create : OpenFileMode.CreateOrOpen);
            if (!shouldTruncate)
            {
                _stream.Position = _stream.Length;
            }

            _writer = new StreamWriter(_stream);
        }
        catch (Exception ex)
        {
            Engine.Core.Log.Error("Error creating GameLogSink. Reason: {0}", ex.Message);
        }
    }

    /// <summary>
    /// 获取当天的日志文件路径，并检查是否需要截断
    /// </summary>
    /// <param name="shouldTruncate">输出参数，指示是否需要截断文件（文件大小超过限制时）</param>
    /// <returns>日志文件的完整路径</returns>
    private static string GetLogFilePath(out bool shouldTruncate)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var path = Storage.CombinePaths(GamePaths.Logs, $"Game{today}.log");

        shouldTruncate = false;
        if (!Storage.FileExists(path))
        {
            return path;
        }

        var fileInfo = Storage.GetFileInfo(path);
        if (fileInfo is { Length: >= _maxFileSize })
        {
            shouldTruncate = true;
        }

        return path;
    }

    /// <summary>
    /// 清理旧的日志文件。当日志文件数量超过 <see cref="_cleanupThreshold"/> 时，
    /// 只保留最近的 <see cref="_keepLogFiles"/> 个日志文件
    /// </summary>
    private static void CleanupOldLogFiles()
    {
        try
        {
            var files = Storage.ListFileNames(GamePaths.Logs);
            var logFiles = new List<(string Path, DateTime Date)>();
            foreach (var file in files)
            {
                var fileName = Storage.GetFileName(file);
                if (!fileName.StartsWith("Game") || !fileName.EndsWith(".log"))
                {
                    continue;
                }

                var nameWithoutExt = fileName.Substring(4, fileName.Length - 8);
                if (DateTime.TryParseExact(nameWithoutExt, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
                {
                    logFiles.Add((file, date));
                }
            }

            if (logFiles.Count < _cleanupThreshold)
            {
                return;
            }

            logFiles.Sort((a, b) => a.Date.CompareTo(b.Date));

            var filesToDelete = logFiles.Count - _keepLogFiles;
            for (var i = 0; i < filesToDelete; i++)
            {
                try
                {
                    Storage.DeleteFile(Storage.CombinePaths(GamePaths.Logs, logFiles[i].Path));
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// 写入一条日志消息到文件
    /// </summary>
    /// <param name="type">日志类型</param>
    /// <param name="message">日志消息内容</param>
    public void Log(LogType type, string message)
    {
        if (_stream == null)
        {
            return;
        }

        lock (_stream)
        {
            var value = type switch
            {
                LogType.Debug => "DEBUG: ",
                LogType.Verbose => "INFO: ",
                LogType.Information => "INFO: ",
                LogType.Warning => "WARNING: ",
                LogType.Error => "ERROR: ",
                _ => string.Empty
            };
            var line = $"{DateTime.Now:HH:mm:ss.fff} {value}{message}";

            _writer.WriteLine(line);
            _writer.Flush();

            try
            {
                Engine.Core.Log.AddLogMsg(line);
            }
            catch
            {
                // ignored
            }
        }
    }

    public static void Shutdown()
    {
        if (_stream == null)
        {
            return;
        }

        lock (_stream)
        {
            try
            {
                _writer.Dispose();
            }
            catch
            {
                // ignored
            }

            try
            {
                _stream.Dispose();
            }
            catch
            {
                // ignored
            }

            _stream = null;
            _writer = null!;
        }
    }

    /// <summary>
    /// 获取最近指定字节数的日志内容
    /// </summary>
    /// <param name="bytesCount">要读取的字节数</param>
    /// <returns>日志内容字符串。如果日志流未初始化则返回空字符串</returns>
    public static string GetRecentLog(int bytesCount)
    {
        if (_stream == null)
        {
            return string.Empty;
        }

        lock (_stream)
        {
            try
            {
                _stream.Position = MathUtils.Max(_stream.Position - bytesCount, 0L);
                return new StreamReader(_stream).ReadToEnd();
            }
            finally
            {
                _stream.Position = _stream.Length;
            }
        }
    }

    /// <summary>
    /// 获取最近指定字节数的日志内容，按行返回
    /// </summary>
    /// <param name="bytesCount">要读取的字节数</param>
    /// <returns>日志行列表。如果日志流未初始化则返回空列表</returns>
    public static List<string> GetRecentLogLines(int bytesCount)
    {
        if (_stream == null)
        {
            return [];
        }

        lock (_stream)
        {
            try
            {
                _stream.Position = MathUtils.Max(_stream.Position - bytesCount, 0L);
                var streamReader = new StreamReader(_stream);
                var list = new List<string>();
                while (true)
                {
                    var text = streamReader.ReadLine();
                    if (text == null)
                    {
                        break;
                    }

                    list.Add(text);
                }

                return list;
            }
            finally
            {
                _stream.Position = _stream.Length;
            }
        }
    }
}

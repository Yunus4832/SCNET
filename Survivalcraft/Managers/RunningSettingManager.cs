using System.Xml.Linq;

namespace Game.Managers;

public static class RunningSettingManager
{
    public static string RunningSettingPath => GamePaths.RunningSettingFile;

    public static RunningSetting Current { get; private set; } = new();

    public static RunningSetting Load()
    {
        var runningSetting = LoadFromFile();
        Normalize(runningSetting);
        EnsureFileExists(runningSetting);
        Current = Clone(runningSetting);
        return runningSetting;
    }

    public static void Save(RunningSetting runningSetting)
    {
        try
        {
            if (!Storage.DirectoryExists(GamePaths.Config))
            {
                Storage.CreateDirectory(GamePaths.Config);
            }

            using var stream = Storage.OpenFile(RunningSettingPath, OpenFileMode.Create);
            var root = new XElement("RunningSetting",
                new XAttribute(nameof(RunningSetting.RunMode), runningSetting.RunMode),
                new XAttribute(nameof(RunningSetting.LogLevel), runningSetting.LogLevel),
                new XAttribute(nameof(RunningSetting.WindowMode), runningSetting.WindowMode),
                new XAttribute(nameof(RunningSetting.WindowWidth), runningSetting.WindowWidth),
                new XAttribute(nameof(RunningSetting.WindowHeight), runningSetting.WindowHeight),
                new XAttribute(nameof(RunningSetting.DefaultSessionId),
                    NormalizeDefaultSessionId(runningSetting.DefaultSessionId)),
                new XAttribute(nameof(RunningSetting.PendingSessionId),
                    NormalizePendingSessionId(runningSetting.PendingSessionId)),
                new XElement(nameof(RunningSetting.RemainingArgs),
                    runningSetting.RemainingArgs.Select(arg => new XElement("Arg", arg))));
            root.Save(stream);
            Current = Clone(runningSetting);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to save {RunningSettingPath}: {ex.Message}");
        }
    }

    public static void SetRunMode(RunModeType runMode)
    {
        SaveCurrent(setting => setting.RunMode = runMode);
    }

    public static void SetPendingSession(string? sessionId)
    {
        SaveCurrent(setting => setting.PendingSessionId = NormalizePendingSessionId(sessionId));
    }

    public static void ClearPendingSession()
    {
        SetPendingSession(null);
    }

    public static void SaveCurrent(Action<RunningSetting> update)
    {
        var runningSetting = LoadFromFile();
        Normalize(runningSetting);
        update(runningSetting);
        Save(runningSetting);
    }

    private static RunningSetting LoadFromFile()
    {
        var runningSetting = new RunningSetting();
        try
        {
            if (!Storage.FileExists(RunningSettingPath))
            {
                return runningSetting;
            }

            using var stream = Storage.OpenFile(RunningSettingPath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            runningSetting.RunMode = ParseRunMode(
                root.Attribute(nameof(RunningSetting.RunMode))?.Value);
            runningSetting.LogLevel = ParseLogLevel(
                root.Attribute(nameof(RunningSetting.LogLevel))?.Value,
                runningSetting.LogLevel);
            runningSetting.WindowMode = ParseWindowMode(
                root.Attribute(nameof(RunningSetting.WindowMode))?.Value,
                runningSetting.WindowMode);
            runningSetting.WindowWidth = ParseWindowSize(
                root.Attribute(nameof(RunningSetting.WindowWidth))?.Value);
            runningSetting.WindowHeight = ParseWindowSize(
                root.Attribute(nameof(RunningSetting.WindowHeight))?.Value);
            runningSetting.DefaultSessionId = NormalizeDefaultSessionId(
                root.Attribute(nameof(RunningSetting.DefaultSessionId))?.Value);
            runningSetting.PendingSessionId = NormalizePendingSessionId(
                root.Attribute(nameof(RunningSetting.PendingSessionId))?.Value);
            runningSetting.RemainingArgs = root.Element(nameof(RunningSetting.RemainingArgs))?
                .Elements("Arg")
                .Select(element => element.Value)
                .ToArray() ?? [];
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {RunningSettingPath}: {ex.Message}");
        }

        return runningSetting;
    }

    private static void EnsureFileExists(RunningSetting runningSetting)
    {
        if (!Storage.FileExists(RunningSettingPath))
        {
            Save(runningSetting);
        }
    }

    private static void Normalize(RunningSetting runningSetting)
    {
        runningSetting.WindowWidth = Math.Max(runningSetting.WindowWidth, 0);
        runningSetting.WindowHeight = Math.Max(runningSetting.WindowHeight, 0);
        runningSetting.DefaultSessionId = NormalizePersistedSessionReference(
            runningSetting.DefaultSessionId);
        runningSetting.PendingSessionId = NormalizePersistedSessionReference(
            runningSetting.PendingSessionId);
    }

    private static RunningSetting Clone(RunningSetting runningSetting)
    {
        return new RunningSetting
        {
            RunMode = runningSetting.RunMode,
            LogLevel = runningSetting.LogLevel,
            WindowMode = runningSetting.WindowMode,
            WindowWidth = runningSetting.WindowWidth,
            WindowHeight = runningSetting.WindowHeight,
            DefaultSessionId = runningSetting.DefaultSessionId,
            PendingSessionId = runningSetting.PendingSessionId,
            RemainingArgs = runningSetting.RemainingArgs.ToArray()
        };
    }

    private static RunModeType ParseRunMode(string? value) =>
        Enum.TryParse(value, true, out RunModeType runMode) ? runMode : RunModeType.Gui;

    private static LogType ParseLogLevel(string? value, LogType fallback) =>
        Enum.TryParse(value, true, out LogType logLevel) ? logLevel : fallback;

    private static WindowMode ParseWindowMode(string? value, WindowMode fallback) =>
        Enum.TryParse(value, true, out WindowMode windowMode) ? windowMode : fallback;

    private static int ParseWindowSize(string? value) =>
        int.TryParse(value, out var size) ? Math.Max(size, 0) : 0;

    private static string NormalizePendingSessionId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeDefaultSessionId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizePersistedSessionReference(string? value)
    {
        var sessionId = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return SessionInfoManager.SessionExists(sessionId) ? sessionId : string.Empty;
    }
}

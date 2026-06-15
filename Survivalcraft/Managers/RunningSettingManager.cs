using System.Xml.Linq;

namespace Game.Managers;

public static class RunningSettingManager
{
    public const string RunningSettingPath = "config:RunningSetting.xml";

    public static RunningSetting Current { get; private set; } = new();

    public static RunningSetting Load(string[] args)
    {
        var runningSetting = LoadFromFile();
        Normalize(runningSetting);
        EnsureFileExists(runningSetting);
        runningSetting.RemainingArgs = MergeCommandLine(
            runningSetting,
            args,
            out var saveRequested,
            out var worldOverride,
            out var seedOverride);
        if (!string.IsNullOrWhiteSpace(worldOverride) || seedOverride != null)
        {
            SessionInfoManager.UpdateWorldSelection(runningSetting.SessionId, worldOverride, seedOverride);
            saveRequested = true;
        }

        Current = Clone(runningSetting);
        if (saveRequested)
        {
            Save(runningSetting);
        }

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
                new XAttribute(nameof(RunningSetting.RunMode), runningSetting.RunMode.ToString()),
                new XAttribute(nameof(RunningSetting.LogLevel), runningSetting.LogLevel.ToString()),
                new XAttribute(nameof(RunningSetting.SessionId), NormalizeSessionId(runningSetting.SessionId)),
                new XAttribute(nameof(RunningSetting.Restore), runningSetting.Restore),
                new XElement(nameof(RunningSetting.RemainingArgs),
                    runningSetting.RemainingArgs.Select(arg => new XElement("Arg", arg)))
            );
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
        var runningSetting = LoadCurrent();
        runningSetting.RunMode = runMode;
        Save(runningSetting);
    }

    public static void SetRestore(bool restore)
    {
        var runningSetting = LoadCurrent();
        runningSetting.Restore = restore;
        Save(runningSetting);
    }

    public static void SaveCurrent(Action<RunningSetting> update)
    {
        var runningSetting = LoadCurrent();
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
            runningSetting.RunMode = ParseRunMode(root.Attribute(nameof(RunningSetting.RunMode))?.Value);
            runningSetting.LogLevel = ParseLogLevel(
                root.Attribute(nameof(RunningSetting.LogLevel))?.Value,
                runningSetting.LogLevel);
            runningSetting.SessionId = NormalizeSessionId(root.Attribute(nameof(RunningSetting.SessionId))?.Value);
            runningSetting.Restore = bool.TryParse(root.Attribute(nameof(RunningSetting.Restore))?.Value, out var restore) &&
                                     restore;
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

    private static string[] MergeCommandLine(
        RunningSetting runningSetting,
        string[] args,
        out bool saveRequested,
        out string? worldOverride,
        out string? seedOverride)
    {
        saveRequested = false;
        worldOverride = null;
        seedOverride = null;
        var remainingArgs = new List<string>(runningSetting.RemainingArgs);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--save", StringComparison.OrdinalIgnoreCase))
            {
                saveRequested = true;
                continue;
            }

            if (string.Equals(arg, "-d", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--server", StringComparison.OrdinalIgnoreCase))
            {
                runningSetting.RunMode = RunModeType.HeadlessServer;
                continue;
            }

            if (string.Equals(arg, "--gui", StringComparison.OrdinalIgnoreCase))
            {
                runningSetting.RunMode = RunModeType.Gui;
                continue;
            }

            if (string.Equals(arg, "--restore", StringComparison.OrdinalIgnoreCase))
            {
                runningSetting.Restore = true;
                continue;
            }

            if (string.Equals(arg, "--world", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    worldOverride = NormalizeWorld(args[++i]);
                }

                continue;
            }

            if (string.Equals(arg, "--log-level", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    runningSetting.LogLevel = ParseLogLevel(args[++i], runningSetting.LogLevel);
                }

                continue;
            }

            if (string.Equals(arg, "--seed", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    seedOverride = args[++i];
                }

                continue;
            }

            remainingArgs.Add(arg);
        }

        return remainingArgs.ToArray();
    }

    private static RunningSetting LoadCurrent()
    {
        var runningSetting = Clone(Current);
        if (string.IsNullOrWhiteSpace(runningSetting.SessionId))
        {
            runningSetting = LoadFromFile();
        }

        Normalize(runningSetting);
        return runningSetting;
    }

    private static void EnsureFileExists(RunningSetting runningSetting)
    {
        try
        {
            if (Storage.FileExists(RunningSettingPath))
            {
                return;
            }

            if (!Storage.DirectoryExists(GamePaths.Config))
            {
                Storage.CreateDirectory(GamePaths.Config);
            }

            Save(runningSetting);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to create default {RunningSettingPath}: {ex.Message}");
        }
    }

    private static RunModeType ParseRunMode(string? value)
    {
        return Enum.TryParse(value, true, out RunModeType runMode) ? runMode : RunModeType.Gui;
    }

    private static LogType ParseLogLevel(string? value, LogType fallback)
    {
        return Enum.TryParse(value, true, out LogType logLevel) ? logLevel : fallback;
    }

    private static void Normalize(RunningSetting runningSetting)
    {
        runningSetting.SessionId = NormalizeSessionId(runningSetting.SessionId);
        runningSetting.RemainingArgs ??= [];
    }

    private static RunningSetting Clone(RunningSetting runningSetting)
    {
        return new RunningSetting
        {
            RunMode = runningSetting.RunMode,
            LogLevel = runningSetting.LogLevel,
            SessionId = runningSetting.SessionId,
            Restore = runningSetting.Restore,
            RemainingArgs = runningSetting.RemainingArgs.ToArray()
        };
    }

    private static string NormalizeSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "default" : value;
    }

    private static string NormalizeWorld(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "World" : value;
    }
}

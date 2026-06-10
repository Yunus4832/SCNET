using System.Xml.Linq;

namespace Game.Managers;

public static class RunningSettingManager
{
    public const string RunningSettingPath = "config:RunningSetting.xml";

    public static RunningSetting Load(string[] args)
    {
        var runningSetting = LoadFromFile();
        EnsureFileExists(runningSetting);
        runningSetting.RemainingArgs = MergeCommandLine(runningSetting, args, out var saveRequested);
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
                new XAttribute(nameof(RunningSetting.World), runningSetting.World),
                new XAttribute(nameof(RunningSetting.Seed), runningSetting.Seed),
                new XElement(nameof(RunningSetting.RemainingArgs),
                    runningSetting.RemainingArgs.Select(arg => new XElement("Arg", arg)))
            );
            root.Save(stream);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to save {RunningSettingPath}: {ex.Message}");
        }
    }

    public static void SetRunMode(RunModeType runMode)
    {
        var runningSetting = Load([]);
        runningSetting.RunMode = runMode;
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
            runningSetting.World = NormalizeWorld(root.Attribute(nameof(RunningSetting.World))?.Value);
            runningSetting.Seed = root.Attribute(nameof(RunningSetting.Seed))?.Value ?? string.Empty;
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
        out bool saveRequested)
    {
        saveRequested = false;
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

            if (string.Equals(arg, "--world", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    runningSetting.World = NormalizeWorld(args[++i]);
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
                    runningSetting.Seed = args[++i];
                }

                continue;
            }

            remainingArgs.Add(arg);
        }

        return remainingArgs.ToArray();
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

    private static string NormalizeWorld(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "World" : value;
    }
}

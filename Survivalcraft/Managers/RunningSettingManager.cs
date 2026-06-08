using System.Xml.Linq;

namespace Game.Managers;

public static class RunningSettingManager
{
    public const string RunningSettingPath = "config:RunningSetting.xml";

    public static RunningSetting Load(string[] args)
    {
        var runningSetting = LoadFromFile();
        EnsureFileExists(runningSetting);
        runningSetting.RemainingArgs = MergeCommandLine(runningSetting, args);
        return runningSetting;
    }

    public static void Save(RunningSetting runningSetting)
    {
        try
        {
            if (!Storage.DirectoryExists(ModsManager.ConfigPath))
            {
                Storage.CreateDirectory(ModsManager.ConfigPath);
            }

            using var stream = Storage.OpenFile(RunningSettingPath, OpenFileMode.Create);
            var root = new XElement("RunningSetting",
                new XAttribute(nameof(RunningSetting.RunMode), runningSetting.RunMode.ToString()),
                new XAttribute(nameof(RunningSetting.World), runningSetting.World),
                new XAttribute(nameof(RunningSetting.Seed), runningSetting.Seed)
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
            runningSetting.World = NormalizeWorld(root.Attribute(nameof(RunningSetting.World))?.Value);
            runningSetting.Seed = root.Attribute(nameof(RunningSetting.Seed))?.Value ?? string.Empty;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {RunningSettingPath}: {ex.Message}");
        }

        return runningSetting;
    }

    private static string[] MergeCommandLine(RunningSetting runningSetting, string[] args)
    {
        var remainingArgs = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
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

            if (!Storage.DirectoryExists(ModsManager.ConfigPath))
            {
                Storage.CreateDirectory(ModsManager.ConfigPath);
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
        if (Enum.TryParse(value, true, out RunModeType runMode))
        {
            return runMode;
        }

        return RunModeType.Gui;
    }

    private static string NormalizeWorld(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "World" : value;
    }
}

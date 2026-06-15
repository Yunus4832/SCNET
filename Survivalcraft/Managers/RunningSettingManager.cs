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
        runningSetting.RemainingArgs = MergeCommandLine(runningSetting, args, out var saveRequested);
        FinalizeStartupState(runningSetting, saveRequested);

        Current = Clone(runningSetting);
        if (saveRequested)
        {
            Save(runningSetting);
            if (runningSetting.HasExplicitSessionRequest)
            {
                SessionInfoManager.Save(SessionInfoManager.ResolveStartupSession(runningSetting));
            }
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
                new XAttribute(nameof(RunningSetting.DefaultSessionId), NormalizeSessionId(runningSetting.DefaultSessionId)),
                new XAttribute(nameof(RunningSetting.PendingSessionId), NormalizePendingSessionId(runningSetting.PendingSessionId)),
                new XAttribute(nameof(RunningSetting.DefaultGuiStartupBehavior), runningSetting.DefaultGuiStartupBehavior),
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

    public static void SetPendingSession(string? sessionId)
    {
        var runningSetting = LoadCurrent();
        runningSetting.PendingSessionId = NormalizePendingSessionId(sessionId);
        Save(runningSetting);
    }

    public static void ClearPendingSession()
    {
        SetPendingSession(null);
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
            runningSetting.DefaultSessionId = NormalizeSessionId(
                root.Attribute(nameof(RunningSetting.DefaultSessionId))?.Value ??
                root.Attribute("PersistedSessionId")?.Value ??
                root.Attribute("SessionId")?.Value);
            runningSetting.PendingSessionId = NormalizePendingSessionId(
                root.Attribute(nameof(RunningSetting.PendingSessionId))?.Value);
            runningSetting.DefaultGuiStartupBehavior = ParseGuiStartupBehavior(
                root.Attribute(nameof(RunningSetting.DefaultGuiStartupBehavior))?.Value ??
                root.Attribute("StartupBehavior")?.Value,
                runningSetting.DefaultGuiStartupBehavior);

            if (string.IsNullOrWhiteSpace(runningSetting.PendingSessionId) &&
                bool.TryParse(root.Attribute("Restore")?.Value, out var restore) &&
                restore)
            {
                runningSetting.PendingSessionId = NormalizeSessionId(root.Attribute("SessionId")?.Value);
            }

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
        string? worldOverride = null;
        string? seedOverride = null;
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
                if (!string.IsNullOrWhiteSpace(runningSetting.PendingSessionId))
                {
                    runningSetting.ShouldEnterSession = true;
                }

                continue;
            }

            if (string.Equals(arg, "--session", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    runningSetting.ActiveSessionId = NormalizeSessionId(args[++i]);
                    runningSetting.HasExplicitSessionRequest = true;
                    runningSetting.ShouldEnterSession = true;
                }

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

        if (!runningSetting.HasExplicitSessionRequest)
        {
            if (!string.IsNullOrWhiteSpace(worldOverride) || seedOverride != null)
            {
                Log.Warning("Ignoring --world/--seed because --session was not specified.");
            }

            worldOverride = null;
            seedOverride = null;
        }

        runningSetting.SessionWorldOverride = worldOverride;
        runningSetting.SessionSeedOverride = seedOverride;
        return remainingArgs.ToArray();
    }

    private static void FinalizeStartupState(RunningSetting runningSetting, bool saveRequested)
    {
        if (runningSetting.HasExplicitSessionRequest)
        {
            runningSetting.ActiveSessionId = NormalizeSessionId(runningSetting.ActiveSessionId);
            runningSetting.ShouldEnterSession = true;
            runningSetting.SessionIsTransient = !saveRequested;
            if (saveRequested)
            {
                runningSetting.DefaultSessionId = runningSetting.ActiveSessionId;
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(runningSetting.PendingSessionId))
        {
            runningSetting.ActiveSessionId = NormalizeSessionId(runningSetting.PendingSessionId);
            runningSetting.ShouldEnterSession = true;
            runningSetting.SessionIsTransient = true;
            return;
        }

        runningSetting.ActiveSessionId = NormalizeSessionId(runningSetting.DefaultSessionId);
        runningSetting.SessionIsTransient = false;
        runningSetting.ShouldEnterSession =
            runningSetting.RunMode is RunModeType.HeadlessServer ||
            runningSetting.DefaultGuiStartupBehavior is GuiStartupBehavior.EnterDefaultSession;
    }

    private static RunningSetting LoadCurrent()
    {
        var runningSetting = LoadFromFile();
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

    private static GuiStartupBehavior ParseGuiStartupBehavior(string? value, GuiStartupBehavior fallback)
    {
        return Enum.TryParse(value, true, out GuiStartupBehavior behavior) ? behavior : fallback;
    }

    private static void Normalize(RunningSetting runningSetting)
    {
        runningSetting.DefaultSessionId = NormalizeSessionId(runningSetting.DefaultSessionId);
        runningSetting.PendingSessionId = NormalizePendingSessionId(runningSetting.PendingSessionId);
        runningSetting.ActiveSessionId = NormalizeSessionId(
            string.IsNullOrWhiteSpace(runningSetting.ActiveSessionId)
                ? runningSetting.DefaultSessionId
                : runningSetting.ActiveSessionId);
        runningSetting.RemainingArgs ??= [];
    }

    private static RunningSetting Clone(RunningSetting runningSetting)
    {
        return new RunningSetting
        {
            RunMode = runningSetting.RunMode,
            LogLevel = runningSetting.LogLevel,
            DefaultSessionId = runningSetting.DefaultSessionId,
            PendingSessionId = runningSetting.PendingSessionId,
            DefaultGuiStartupBehavior = runningSetting.DefaultGuiStartupBehavior,
            RemainingArgs = runningSetting.RemainingArgs.ToArray(),
            ActiveSessionId = runningSetting.ActiveSessionId,
            HasExplicitSessionRequest = runningSetting.HasExplicitSessionRequest,
            SessionIsTransient = runningSetting.SessionIsTransient,
            ShouldEnterSession = runningSetting.ShouldEnterSession,
            SessionWorldOverride = runningSetting.SessionWorldOverride,
            SessionSeedOverride = runningSetting.SessionSeedOverride
        };
    }

    private static string NormalizeSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "default" : value;
    }

    private static string NormalizePendingSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeSessionId(value);
    }

    private static string NormalizeWorld(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "World" : value;
    }
}

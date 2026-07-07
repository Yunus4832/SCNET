using System.Xml.Linq;

namespace Game.Managers;

public static class RunningSettingManager
{
    public static string RunningSettingPath => GamePaths.RunningSettingFile;

    public static RunningSetting Current { get; private set; } = new();

    public static RunningSetting Load(string[] args)
    {
        var runningSetting = LoadFromFile();
        Normalize(runningSetting);
        EnsureFileExists(runningSetting);
        runningSetting.RemainingArgs = MergeCommandLine(runningSetting, args, out var saveRequested);
        FinalizeStartupState(runningSetting);

        Current = Clone(runningSetting);
        if (!saveRequested)
        {
            return runningSetting;
        }

        Save(runningSetting);
        if (!string.IsNullOrWhiteSpace(runningSetting.ActiveSessionId))
        {
            SessionInfoManager.Save(SessionInfoManager.ResolveStartupSession(runningSetting));
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
                new XAttribute(nameof(RunningSetting.DefaultSessionId), NormalizeDefaultSessionId(runningSetting.DefaultSessionId)),
                new XAttribute(nameof(RunningSetting.PendingSessionId), NormalizePendingSessionId(runningSetting.PendingSessionId)),
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

            if (string.Equals(arg, "--session", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    var sessionName = NormalizeSessionName(args[++i]);
                    if (!string.IsNullOrWhiteSpace(sessionName))
                    {
                        runningSetting.RequestedSessionName = sessionName;
                        runningSetting.HasExplicitSessionRequest = true;
                    }
                    else
                    {
                        Log.Warning("Ignoring --session because the session name is missing.");
                    }
                }
                else
                {
                    Log.Warning("Ignoring --session because the session name is missing.");
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

    private static void FinalizeStartupState(RunningSetting runningSetting)
    {
        if (runningSetting.HasExplicitSessionRequest)
        {
            runningSetting.RequestedSessionName = NormalizeSessionName(runningSetting.RequestedSessionName);
            runningSetting.ActiveSessionId = SessionInfoManager.ResolveSessionIdForName(runningSetting.RequestedSessionName);
            return;
        }

        if (!string.IsNullOrWhiteSpace(runningSetting.PendingSessionId))
        {
            runningSetting.ActiveSessionId = NormalizeSessionId(runningSetting.PendingSessionId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(runningSetting.DefaultSessionId))
        {
            runningSetting.ActiveSessionId = NormalizeSessionId(runningSetting.DefaultSessionId);
            return;
        }

        runningSetting.ActiveSessionId = Guid.NewGuid().ToString("N");
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

    private static void Normalize(RunningSetting runningSetting)
    {
        runningSetting.DefaultSessionId = NormalizeDefaultSessionId(runningSetting.DefaultSessionId);
        runningSetting.PendingSessionId = NormalizePendingSessionId(runningSetting.PendingSessionId);
        runningSetting.ActiveSessionId = NormalizeActiveSessionId(runningSetting.ActiveSessionId);
        runningSetting.DefaultSessionId = NormalizePersistedSessionReference(runningSetting.DefaultSessionId);
        runningSetting.PendingSessionId = NormalizePersistedSessionReference(runningSetting.PendingSessionId);
        runningSetting.ActiveSessionId = NormalizeActiveSessionReference(runningSetting.ActiveSessionId);
    }

    private static RunningSetting Clone(RunningSetting runningSetting)
    {
        return new RunningSetting
        {
            RunMode = runningSetting.RunMode,
            LogLevel = runningSetting.LogLevel,
            DefaultSessionId = runningSetting.DefaultSessionId,
            PendingSessionId = runningSetting.PendingSessionId,
            RemainingArgs = runningSetting.RemainingArgs.ToArray(),
            ActiveSessionId = runningSetting.ActiveSessionId,
            HasExplicitSessionRequest = runningSetting.HasExplicitSessionRequest,
            RequestedSessionName = runningSetting.RequestedSessionName,
            SessionWorldOverride = runningSetting.SessionWorldOverride,
            SessionSeedOverride = runningSetting.SessionSeedOverride
        };
    }

    private static string NormalizeSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizePendingSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeSessionId(value);
    }

    private static string NormalizeDefaultSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeActiveSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeSessionId(value);
    }

    private static string NormalizePersistedSessionReference(string? value)
    {
        var sessionId = string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeSessionId(value);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return string.Empty;
        }

        return SessionInfoManager.SessionExists(sessionId) ? sessionId : string.Empty;
    }

    private static string NormalizeActiveSessionReference(string? value)
    {
        var sessionId = string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeSessionId(value);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return string.Empty;
        }

        return SessionInfoManager.IsValidSessionId(sessionId) ? sessionId : string.Empty;
    }

    private static string NormalizeSessionName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeWorld(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "World" : value;
    }
}

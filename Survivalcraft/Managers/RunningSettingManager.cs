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
                new XAttribute(nameof(RunningSetting.WindowMode), runningSetting.WindowMode.ToString()),
                new XAttribute(nameof(RunningSetting.WindowWidth), runningSetting.WindowWidth),
                new XAttribute(nameof(RunningSetting.WindowHeight), runningSetting.WindowHeight),
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

    private static string[] MergeCommandLine(
        RunningSetting runningSetting,
        string[] args,
        out bool saveRequested)
    {
        saveRequested = false;
        var remainingArgs = new List<string>(runningSetting.RemainingArgs);
        string? worldOverride = null;
        string? seedOverride = null;
        string? windowModeOverride = null;
        string? windowSizeOverride = null;
        string? connectOverride = null;
        string? playerOverride = null;
        var forceWorldRunServer = false;
        int? serverPortOverride = null;
        int? broadcastPortOverride = null;
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

            if (string.Equals(arg, "--connect", StringComparison.OrdinalIgnoreCase))
            {
                connectOverride = ReadOptionValue(args, ref i, "--connect");
                continue;
            }

            if (string.Equals(arg, "--player", StringComparison.OrdinalIgnoreCase))
            {
                playerOverride = ReadOptionValue(args, ref i, "--player")?.Trim();
                continue;
            }

            if (string.Equals(arg, "--host", StringComparison.OrdinalIgnoreCase))
            {
                forceWorldRunServer = true;
                continue;
            }

            if (string.Equals(arg, "--server-port", StringComparison.OrdinalIgnoreCase))
            {
                serverPortOverride = ParsePort(ReadOptionValue(args, ref i, "--server-port"), "--server-port");
                continue;
            }

            if (string.Equals(arg, "--broadcast-port", StringComparison.OrdinalIgnoreCase))
            {
                broadcastPortOverride = ParsePort(ReadOptionValue(args, ref i, "--broadcast-port"), "--broadcast-port");
                continue;
            }

            if (string.Equals(arg, "--window-mode", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    windowModeOverride = args[++i];
                }

                continue;
            }

            if (string.Equals(arg, "--window-size", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    windowSizeOverride = args[++i];
                }

                continue;
            }

            remainingArgs.Add(arg);
        }

        // 窗口参数只在 GUI 模式生效：headless 无窗口，覆盖值既不应应用也不应随 --save 持久化。
        if (runningSetting.RunMode == RunModeType.HeadlessServer)
        {
            if (windowModeOverride != null || windowSizeOverride != null)
            {
                Log.Warning("Ignoring --window-mode/--window-size because the run mode is headless server.");
            }
        }
        else
        {
            if (windowModeOverride != null)
            {
                if (Enum.TryParse<WindowMode>(windowModeOverride, true, out var windowMode))
                {
                    runningSetting.WindowMode = windowMode;
                }
                else
                {
                    Log.Warning($"Ignoring --window-mode because the value '{windowModeOverride}' is invalid.");
                }
            }

            if (windowSizeOverride != null)
            {
                if (TryParseWindowSize(windowSizeOverride, out var width, out var height))
                {
                    runningSetting.WindowWidth = width;
                    runningSetting.WindowHeight = height;
                }
                else
                {
                    Log.Warning($"Ignoring --window-size because the value '{windowSizeOverride}' is invalid.");
                }
            }
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
        if (TryParseEndpoint(connectOverride, out var connectHost, out var connectPort))
        {
            runningSetting.SessionConnectHostOverride = connectHost;
            runningSetting.SessionConnectPortOverride = connectPort;
        }
        if (forceWorldRunServer && !string.IsNullOrWhiteSpace(runningSetting.SessionConnectHostOverride))
        {
            Log.Warning("Ignoring --host because --connect selects a remote server session.");
            forceWorldRunServer = false;
        }
        runningSetting.PlayerOverride = string.IsNullOrWhiteSpace(playerOverride) ? null : playerOverride;
        runningSetting.ForceWorldRunServer = forceWorldRunServer;
        runningSetting.SessionServerPortOverride = serverPortOverride;
        runningSetting.SessionBroadcastPortOverride = broadcastPortOverride;
        runningSetting.SaveRequested = saveRequested;
        return remainingArgs.ToArray();
    }

    private static string? ReadOptionValue(string[] args, ref int index, string option)
    {
        if (index + 1 < args.Length)
        {
            return args[++index];
        }

        Log.Warning($"Ignoring {option} because its value is missing.");
        return null;
    }

    private static int? ParsePort(string? value, string option)
    {
        if (int.TryParse(value, out var port) && port is > 0 and <= 65535)
        {
            return port;
        }

        if (value != null)
        {
            Log.Warning($"Ignoring {option} because '{value}' is not a valid port.");
        }
        return null;
    }

    private static bool TryParseEndpoint(string? value, out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var separator = value.LastIndexOf(':');
        if (separator <= 0 || separator == value.Length - 1 ||
            !int.TryParse(value[(separator + 1)..], out port) || port is <= 0 or > 65535)
        {
            Log.Warning($"Ignoring --connect because '{value}' is not in HOST:PORT format.");
            port = 0;
            return false;
        }

        host = value[..separator].Trim().Trim('[', ']');
        return !string.IsNullOrWhiteSpace(host);
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

    private static WindowMode ParseWindowMode(string? value, WindowMode fallback)
    {
        return Enum.TryParse(value, true, out WindowMode windowMode) ? windowMode : fallback;
    }

    private static int ParseWindowSize(string? value)
    {
        return int.TryParse(value, out var size) ? Math.Max(size, 0) : 0;
    }

    private static bool TryParseWindowSize(string value, out int width, out int height)
    {
        width = 0;
        height = 0;
        var separatorIndex = value.IndexOfAny(['x', 'X']);
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            return false;
        }

        return int.TryParse(value[..separatorIndex], out width) &&
               int.TryParse(value[(separatorIndex + 1)..], out height) &&
               width > 0 &&
               height > 0;
    }

    private static void Normalize(RunningSetting runningSetting)
    {
        runningSetting.WindowWidth = Math.Max(runningSetting.WindowWidth, 0);
        runningSetting.WindowHeight = Math.Max(runningSetting.WindowHeight, 0);
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
            WindowMode = runningSetting.WindowMode,
            WindowWidth = runningSetting.WindowWidth,
            WindowHeight = runningSetting.WindowHeight,
            DefaultSessionId = runningSetting.DefaultSessionId,
            PendingSessionId = runningSetting.PendingSessionId,
            RemainingArgs = runningSetting.RemainingArgs.ToArray(),
            ActiveSessionId = runningSetting.ActiveSessionId,
            HasExplicitSessionRequest = runningSetting.HasExplicitSessionRequest,
            RequestedSessionName = runningSetting.RequestedSessionName,
            SessionWorldOverride = runningSetting.SessionWorldOverride,
            SessionSeedOverride = runningSetting.SessionSeedOverride,
            SessionConnectHostOverride = runningSetting.SessionConnectHostOverride,
            SessionConnectPortOverride = runningSetting.SessionConnectPortOverride,
            PlayerOverride = runningSetting.PlayerOverride,
            ForceWorldRunServer = runningSetting.ForceWorldRunServer,
            SessionServerPortOverride = runningSetting.SessionServerPortOverride,
            SessionBroadcastPortOverride = runningSetting.SessionBroadcastPortOverride,
            SaveRequested = runningSetting.SaveRequested
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

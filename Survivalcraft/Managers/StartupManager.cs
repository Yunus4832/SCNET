namespace Game.Managers;

public static class StartupManager
{
    public static StartupContext Current { get; private set; } = new(
        new RunningSetting(),
        new StartupRequest(),
        new SessionInfo());

    public static StartupContext Load(string[] args)
    {
        var settings = RunningSettingManager.Load();
        var request = ParseArguments(settings, args);
        var activeSessionId = ResolveActiveSessionId(settings, request);
        var session = SessionInfoManager.ResolveStartupSession(
            settings,
            request,
            activeSessionId);
        var context = new StartupContext(settings, request, session);
        Current = context;

        if (!request.Save)
        {
            return context;
        }

        RunningSettingManager.Save(settings);
        SessionInfoManager.Save(session);

        return context;
    }

    private static StartupRequest ParseArguments(RunningSetting settings, string[] args)
    {
        var request = new StartupRequest();
        var remainingArgs = new List<string>(settings.RemainingArgs);
        string? windowModeOverride = null;
        string? windowSizeOverride = null;
        string? connectOverride = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--save", StringComparison.OrdinalIgnoreCase))
            {
                request.Save = true;
                continue;
            }

            if (string.Equals(arg, "-d", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--server", StringComparison.OrdinalIgnoreCase))
            {
                settings.RunMode = RunModeType.HeadlessServer;
                continue;
            }

            if (string.Equals(arg, "--gui", StringComparison.OrdinalIgnoreCase))
            {
                settings.RunMode = RunModeType.Gui;
                continue;
            }

            if (string.Equals(arg, "--session", StringComparison.OrdinalIgnoreCase))
            {
                var sessionName = NormalizeSessionName(
                    ReadOptionValue(args, ref i, "--session"));
                if (!string.IsNullOrWhiteSpace(sessionName))
                {
                    request.SessionName = sessionName;
                    request.HasExplicitSession = true;
                }

                continue;
            }

            if (string.Equals(arg, "--world", StringComparison.OrdinalIgnoreCase))
            {
                request.World = ReadOptionValue(args, ref i, "--world");
                continue;
            }

            if (string.Equals(arg, "--seed", StringComparison.OrdinalIgnoreCase))
            {
                request.Seed = ReadOptionValue(args, ref i, "--seed");
                continue;
            }

            if (string.Equals(arg, "--game-mode", StringComparison.OrdinalIgnoreCase))
            {
                request.GameMode = ParseGameMode(
                    ReadOptionValue(args, ref i, "--game-mode"));
                continue;
            }

            if (string.Equals(arg, "--connect", StringComparison.OrdinalIgnoreCase))
            {
                connectOverride = ReadOptionValue(args, ref i, "--connect");
                continue;
            }

            if (string.Equals(arg, "--player", StringComparison.OrdinalIgnoreCase))
            {
                request.PlayerName = ReadOptionValue(args, ref i, "--player")?.Trim();
                continue;
            }

            if (string.Equals(arg, "--host", StringComparison.OrdinalIgnoreCase))
            {
                request.ForceWorldRunServer = true;
                continue;
            }

            if (string.Equals(arg, "--server-port", StringComparison.OrdinalIgnoreCase))
            {
                request.ServerPort = ParsePort(
                    ReadOptionValue(args, ref i, "--server-port"),
                    "--server-port");
                continue;
            }

            if (string.Equals(arg, "--broadcast-port", StringComparison.OrdinalIgnoreCase))
            {
                request.BroadcastPort = ParsePort(
                    ReadOptionValue(args, ref i, "--broadcast-port"),
                    "--broadcast-port");
                continue;
            }

            if (string.Equals(arg, "--http-command", StringComparison.OrdinalIgnoreCase))
            {
                request.HttpCommandEnabled = true;
                continue;
            }

            if (string.Equals(arg, "--no-http-command", StringComparison.OrdinalIgnoreCase))
            {
                request.HttpCommandEnabled = false;
                continue;
            }

            if (string.Equals(arg, "--http-command-port", StringComparison.OrdinalIgnoreCase))
            {
                request.HttpCommandPort = ParseHttpCommandPort(
                    ReadOptionValue(args, ref i, "--http-command-port"));
                continue;
            }

            if (string.Equals(arg, "--http-command-access-token", StringComparison.OrdinalIgnoreCase))
            {
                request.HttpCommandAccessToken = ReadOptionValue(
                    args,
                    ref i,
                    "--http-command-access-token")?.Trim();
                continue;
            }

            if (string.Equals(arg, "--log-level", StringComparison.OrdinalIgnoreCase))
            {
                settings.LogLevel = ParseLogLevel(
                    ReadOptionValue(args, ref i, "--log-level"),
                    settings.LogLevel);
                continue;
            }

            if (string.Equals(arg, "--window-mode", StringComparison.OrdinalIgnoreCase))
            {
                windowModeOverride = ReadOptionValue(args, ref i, "--window-mode");
                continue;
            }

            if (string.Equals(arg, "--window-size", StringComparison.OrdinalIgnoreCase))
            {
                windowSizeOverride = ReadOptionValue(args, ref i, "--window-size");
                continue;
            }

            remainingArgs.Add(arg);
        }

        ApplyWindowOverrides(settings, windowModeOverride, windowSizeOverride);
        ApplySessionConstraints(request);
        if (TryParseEndpoint(connectOverride, out var connectHost, out var connectPort))
        {
            request.ConnectHost = connectHost;
            request.ConnectPort = connectPort;
        }

        if (request.ForceWorldRunServer && !string.IsNullOrWhiteSpace(request.ConnectHost))
        {
            Log.Warning("Ignoring --host because --connect selects a remote server session.");
            request.ForceWorldRunServer = false;
        }

        request.PlayerName = string.IsNullOrWhiteSpace(request.PlayerName)
            ? null
            : request.PlayerName;
        settings.RemainingArgs = remainingArgs.ToArray();
        return request;
    }

    private static void ApplyWindowOverrides(
        RunningSetting settings,
        string? windowModeOverride,
        string? windowSizeOverride)
    {
        if (settings.RunMode is RunModeType.HeadlessServer)
        {
            if (windowModeOverride != null || windowSizeOverride != null)
            {
                Log.Warning("Ignoring --window-mode/--window-size because the run mode is headless server.");
            }

            return;
        }

        if (windowModeOverride != null)
        {
            if (Enum.TryParse<WindowMode>(windowModeOverride, true, out var windowMode))
            {
                settings.WindowMode = windowMode;
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
                settings.WindowWidth = width;
                settings.WindowHeight = height;
            }
            else
            {
                Log.Warning($"Ignoring --window-size because the value '{windowSizeOverride}' is invalid.");
            }
        }
    }

    private static void ApplySessionConstraints(StartupRequest request)
    {
        if (request.HasExplicitSession)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.World) ||
            request.Seed != null ||
            request.GameMode != null)
        {
            Log.Warning("Ignoring --world/--seed/--game-mode because --session was not specified.");
        }

        request.World = null;
        request.Seed = null;
        request.GameMode = null;
    }

    private static string ResolveActiveSessionId(
        RunningSetting settings,
        StartupRequest request)
    {
        if (request.HasExplicitSession)
        {
            return SessionInfoManager.ResolveSessionIdForName(request.SessionName);
        }

        if (!string.IsNullOrWhiteSpace(settings.PendingSessionId))
        {
            return settings.PendingSessionId;
        }

        if (!string.IsNullOrWhiteSpace(settings.DefaultSessionId))
        {
            return settings.DefaultSessionId;
        }

        return Guid.NewGuid().ToString("N");
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

    private static GameMode? ParseGameMode(string? value)
    {
        if (Enum.TryParse<GameMode>(value, true, out var gameMode) && Enum.IsDefined(gameMode))
        {
            return gameMode;
        }

        if (value != null)
        {
            Log.Warning(
                $"Ignoring --game-mode because '{value}' is invalid. " +
                $"Expected one of: {string.Join(", ", Enum.GetNames<GameMode>())}.");
        }

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

    private static int ParseHttpCommandPort(string? value)
    {
        if (int.TryParse(value, out var port) && port is > 0 and <= 65535)
        {
            return port;
        }

        Log.Error(
            $"HTTP command host is disabled because --http-command-port " +
            $"value '{value ?? "<missing>"}' is invalid.");
        return 0;
    }

    private static LogType ParseLogLevel(string? value, LogType fallback) =>
        Enum.TryParse(value, true, out LogType logLevel) ? logLevel : fallback;

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

    private static bool TryParseWindowSize(string value, out int width, out int height)
    {
        width = 0;
        height = 0;
        var separatorIndex = value.IndexOfAny(['x', 'X']);
        return separatorIndex > 0 &&
               separatorIndex < value.Length - 1 &&
               int.TryParse(value[..separatorIndex], out width) &&
               int.TryParse(value[(separatorIndex + 1)..], out height) &&
               width > 0 &&
               height > 0;
    }

    private static string NormalizeSessionName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

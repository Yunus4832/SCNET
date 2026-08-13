using System.Net;
using System.Security.Cryptography;
using System.Xml.Linq;

using Game.Network;
using Game.Network.Enums;

namespace Game.Managers;

public static class SessionInfoManager
{
    public static string SessionInfoPath => GamePaths.SessionInfoFile;

    private static readonly HashSet<string> _worldListScreens =
    [
        "Play",
        "NewWorld",
        "ModifyWorld",
        "WorldOptions"
    ];

    public static SessionInfo Load(string? sessionId)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            return CreateDefault(string.Empty);
        }

        var sessionInfo = CreateDefault(normalizedSessionId);
        try
        {
            if (!Storage.FileExists(SessionInfoPath))
            {
                return sessionInfo;
            }

            using var stream = Storage.OpenFile(SessionInfoPath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            var sessionElement = FindSessionElement(root, normalizedSessionId);
            if (sessionElement == null)
            {
                return sessionInfo;
            }

            PopulateFromElement(sessionInfo, sessionElement);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {SessionInfoPath}: {ex.Message}");
        }

        return sessionInfo;
    }

    public static SessionInfo? LoadByName(string? sessionName)
    {
        var normalizedSessionName = NormalizeSessionName(sessionName);
        if (string.IsNullOrWhiteSpace(normalizedSessionName))
        {
            return null;
        }

        try
        {
            if (!Storage.FileExists(SessionInfoPath))
            {
                return null;
            }

            using var stream = Storage.OpenFile(SessionInfoPath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            var sessionElement = FindSessionElementByName(root, normalizedSessionName);
            if (sessionElement == null)
            {
                return null;
            }

            var sessionInfo = CreateDefault(NormalizeSessionId(sessionElement.Attribute(nameof(SessionInfo.SessionId))?.Value));
            PopulateFromElement(sessionInfo, sessionElement);
            return sessionInfo;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {SessionInfoPath}: {ex.Message}");
            return null;
        }
    }

    public static string ResolveSessionIdForName(string? sessionName)
    {
        var normalizedSessionName = NormalizeSessionName(sessionName);
        if (string.IsNullOrWhiteSpace(normalizedSessionName))
        {
            return Guid.NewGuid().ToString("N");
        }

        return LoadByName(normalizedSessionName)?.SessionId ?? Guid.NewGuid().ToString("N");
    }

    public static bool IsValidSessionId(string? sessionId)
    {
        return !string.IsNullOrWhiteSpace(sessionId) && Guid.TryParse(sessionId, out _);
    }

    public static bool SessionExists(string? sessionId)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        return IsValidSessionId(normalizedSessionId) && TryLoadExisting(normalizedSessionId, out _);
    }

    public static void Save(SessionInfo sessionInfo)
    {
        try
        {
            if (!Storage.DirectoryExists(GamePaths.Config))
            {
                Storage.CreateDirectory(GamePaths.Config);
            }

            Normalize(sessionInfo);

            var root = LoadSessionRoot();
            var existing = FindSessionElement(root, sessionInfo.SessionId);
            existing?.Remove();
            root.Add(CreateSessionElement(sessionInfo));

            using var stream = Storage.OpenFile(SessionInfoPath, OpenFileMode.Create);
            root.Save(stream);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to save {SessionInfoPath}: {ex.Message}");
        }
    }

    public static SessionInfo CaptureCurrentSession()
    {
        var sessionInfo = Load(RunningSettingManager.Current.ActiveSessionId);
        sessionInfo.SessionId = NormalizeSessionId(RunningSettingManager.Current.ActiveSessionId);
        PopulateFromCurrentState(sessionInfo);
        Normalize(sessionInfo);
        return sessionInfo;
    }

    public static SessionInfo PrepareRestartSession(SessionInfo? sessionInfo = null)
    {
        var effective = sessionInfo ?? CaptureCurrentSession();
        effective.SessionId = CreateRestartSessionId();
        effective.Name = string.Empty;
        Normalize(effective);
        Save(effective);
        return effective;
    }

    public static SessionInfo CreateRemoteClientSession(IPEndPoint endPoint, string password)
    {
        var sessionInfo = Load(RunningSettingManager.Current.ActiveSessionId);
        sessionInfo.SessionId = NormalizeSessionId(RunningSettingManager.Current.ActiveSessionId);
        sessionInfo.Target = SessionTarget.RemoteServer;
        sessionInfo.ServerHost = endPoint.Address.ToString();
        sessionInfo.ServerPort = endPoint.Port;
        sessionInfo.Password = password;
        return sessionInfo;
    }

    public static bool TryRestoreGuiSession()
    {
        var runningSetting = RunningSettingManager.Current;
        var sessionInfo = ResolveStartupSession(runningSetting);
        ConsumePendingSessionIfNeeded(runningSetting, sessionInfo);

        return sessionInfo.Target switch
        {
            SessionTarget.MainMenu => SwitchTo("MainMenu"),
            SessionTarget.WorldList => SwitchTo("Play"),
            SessionTarget.ServerBrowser => SwitchTo("NetPlay"),
            SessionTarget.World => RestoreWorldSession(
                sessionInfo,
                createIfMissing: runningSetting.HasExplicitSessionRequest),
            SessionTarget.RemoteServer => RestoreRemoteClientSession(sessionInfo),
            _ => false
        };
    }

    public static SessionInfo ResolveStartupSession()
    {
        return ResolveStartupSession(RunningSettingManager.Current);
    }

    public static SessionInfo ResolveStartupSession(RunningSetting runningSetting)
    {
        var sessionId = NormalizeSessionId(runningSetting.ActiveSessionId);
        if (!TryLoadExisting(sessionId, out var sessionInfo))
        {
            sessionInfo = CreateSessionForStartup(runningSetting, sessionId);
        }

        ApplySessionOverrides(sessionInfo, runningSetting);
        Normalize(sessionInfo);
        return sessionInfo;
    }

    public static WorldInfo ResolveHeadlessWorld(RunningSetting runningSetting)
    {
        var sessionInfo = ResolveStartupSession(runningSetting);
        ConsumePendingSessionIfNeeded(runningSetting, sessionInfo);

        var worldArg = sessionInfo.World;
        var seedArg = sessionInfo.Seed;

        WorldsManager.UpdateWorldsList();
        var worlds = WorldsManager.WorldInfos.ToList();
        Log.Information($"Worlds directory: {GamePaths.Worlds}");
        Log.Information($"Detected worlds: {string.Join(", ", worlds.Select(w => w.WorldSettings.Name))}");

        var worldInfo = worlds.FirstOrDefault(w =>
            string.Equals(w.DirectoryName, worldArg, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.WorldSettings.Name, worldArg, StringComparison.OrdinalIgnoreCase));
        if (worldInfo == null)
        {
            var worldPath = Storage.CombinePaths(GamePaths.Worlds, worldArg);
            if (Storage.DirectoryExists(worldPath))
            {
                worldInfo = WorldsManager.GetWorldInfo(worldPath);
            }
        }

        if (worldInfo == null)
        {
            var worldSettings = new WorldSettings
            {
                Name = worldArg,
                Seed = string.IsNullOrWhiteSpace(seedArg) ? GenerateRandomSeed() : seedArg,
                RunServer = true,
                IsNeedCommunityLogin = false
            };
            var customWorldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, worldArg);
            Log.Information($"Creating new world with seed: {worldSettings.Seed}");
            worldInfo = WorldsManager.CreateWorld(worldSettings, customWorldDirectoryName);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(seedArg))
            {
                Log.Warning($"World already exists; ignoring provided seed \"{seedArg}\".");
            }

            if (!worldInfo.WorldSettings.RunServer)
            {
                Log.Warning(
                    $"World \"{worldInfo.WorldSettings.Name}\" is not marked as a server world. " +
                    "HeadlessServer will enable RunServer automatically.");
                worldInfo.WorldSettings.RunServer = true;
                WorldsManager.ChangeWorld(worldInfo.DirectoryName, worldInfo.WorldSettings);
            }

            Log.Information($"Using existing world seed: {worldInfo.WorldSettings.Seed}");
        }

        return worldInfo;
    }

    private static void PopulateFromCurrentState(SessionInfo sessionInfo)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            PopulateWorldContext(sessionInfo);
            return;
        }

        if (TryCapturePendingLoadingSession(sessionInfo))
        {
            return;
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            if (TryGetCurrentRemoteConnectionContext(out var host, out var port))
            {
                sessionInfo.Target = SessionTarget.RemoteServer;
                sessionInfo.ServerHost = host;
                sessionInfo.ServerPort = port;
            }
            else
            {
                sessionInfo.Target = SessionTarget.ServerBrowser;
            }

            return;
        }

        if (PopulateWorldContext(sessionInfo))
        {
            return;
        }

        var screenName = ScreensManager.GetCurrentScreenName();
        if (_worldListScreens.Contains(screenName))
        {
            sessionInfo.Target = SessionTarget.WorldList;
            return;
        }

        if (string.Equals(screenName, "NetPlay", StringComparison.Ordinal))
        {
            sessionInfo.Target = SessionTarget.ServerBrowser;
            return;
        }

        sessionInfo.Target = SessionTarget.MainMenu;
    }

    private static bool PopulateWorldContext(SessionInfo sessionInfo)
    {
        if (GameManager.WorldInfo == null)
        {
            return false;
        }

        sessionInfo.Target = SessionTarget.World;
        sessionInfo.World = NormalizeWorld(GameManager.WorldInfo.WorldSettings.Name);
        sessionInfo.Seed = GameManager.WorldInfo.WorldSettings.Seed;
        return true;
    }

    private static bool TryCapturePendingLoadingSession(SessionInfo sessionInfo)
    {
        if (ScreensManager.CurrentScreen is not GameLoadingScreen loadingScreen)
        {
            return false;
        }

        if (loadingScreen.CurrentServerEndPoint != null)
        {
            sessionInfo.Target = SessionTarget.RemoteServer;
            sessionInfo.ServerHost = loadingScreen.CurrentServerEndPoint.Address.ToString();
            sessionInfo.ServerPort = loadingScreen.CurrentServerEndPoint.Port;
            sessionInfo.Password = loadingScreen.CurrentPassword;
            return true;
        }

        if (loadingScreen.CurrentWorldInfo == null)
        {
            return false;
        }

        sessionInfo.Target = SessionTarget.World;
        sessionInfo.World = NormalizeWorld(loadingScreen.CurrentWorldInfo.WorldSettings.Name);
        sessionInfo.Seed = loadingScreen.CurrentWorldInfo.WorldSettings.Seed;

        return true;
    }

    private static bool TryGetCurrentRemoteConnectionContext(out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        var endPoint = CommonLib.Net.Server?.IPPoint;
        if (endPoint == null)
        {
            return false;
        }

        host = endPoint.Address.ToString();
        port = endPoint.Port;
        return true;
    }

    private static bool RestoreWorldSession(SessionInfo sessionInfo, bool createIfMissing)
    {
        if (!TryResolveExistingWorld(sessionInfo.World, out var worldInfo))
        {
            if (!createIfMissing)
            {
                Log.Warning($"Cannot restore session \"{sessionInfo.SessionId}\": world \"{sessionInfo.World}\" not found.");
                return false;
            }

            worldInfo = CreateWorld(sessionInfo.World, sessionInfo.Seed, runServer: false);
        }

        var startServer = CommonLib.WorkType == WorkType.Server || worldInfo!.WorldSettings.RunServer;
        if (startServer)
        {
            if (!CommonLib.StartServer(sessionInfo))
            {
                Log.Warning("Cannot restore hosted world: server port is already in use.");
                DialogsManager.Alert("恢复联机世界失败：端口被占用");
                ScreensManager.SwitchScreen("Play");
                return true;
            }
        }

        ScreensManager.SwitchScreen("GameLoading", worldInfo!, string.Empty);
        return true;
    }

    private static bool RestoreRemoteClientSession(SessionInfo sessionInfo)
    {
        if (!TryCreateEndpoint(sessionInfo, out var endPoint))
        {
            Log.Warning($"Cannot restore remote session \"{sessionInfo.SessionId}\": invalid server endpoint.");
            ScreensManager.SwitchScreen("NetPlay");
            return true;
        }

        ScreensManager.SwitchScreen("GameLoading", string.Empty, string.Empty, endPoint, sessionInfo.Password);
        return true;
    }

    private static bool TryCreateEndpoint(SessionInfo sessionInfo, out IPEndPoint endPoint)
    {
        endPoint = null!;
        if (string.IsNullOrWhiteSpace(sessionInfo.ServerHost) || sessionInfo.ServerPort <= 0)
        {
            return false;
        }

        if (IPAddress.TryParse(sessionInfo.ServerHost, out var address))
        {
            endPoint = new IPEndPoint(address, sessionInfo.ServerPort);
            return true;
        }

        if (!CommonLib.Resolve($"{sessionInfo.ServerHost}:{sessionInfo.ServerPort}", out var resolvedEndPoint) ||
            resolvedEndPoint == null)
        {
            return false;
        }

        endPoint = resolvedEndPoint;
        return true;
    }

    private static bool TryResolveExistingWorld(string? worldName, out WorldInfo? worldInfo)
    {
        WorldsManager.UpdateWorldsList();
        var normalizedWorld = NormalizeWorld(worldName);
        worldInfo = WorldsManager.WorldInfos.FirstOrDefault(w =>
                        string.Equals(w.DirectoryName, normalizedWorld, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(w.WorldSettings.Name, normalizedWorld, StringComparison.OrdinalIgnoreCase))
                    ?? null;
        if (worldInfo is not null)
        {
            return true;
        }

        var worldPath = Storage.CombinePaths(GamePaths.Worlds, normalizedWorld);
        if (!Storage.DirectoryExists(worldPath))
        {
            return false;
        }

        var resolvedWorldInfo = WorldsManager.GetWorldInfo(worldPath);
        if (resolvedWorldInfo == null)
        {
            return false;
        }

        worldInfo = resolvedWorldInfo;
        return true;
    }

    private static bool TryLoadExisting(string? sessionId, out SessionInfo sessionInfo)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        sessionInfo = CreateDefault(normalizedSessionId);
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            return false;
        }

        try
        {
            if (!Storage.FileExists(SessionInfoPath))
            {
                return false;
            }

            using var stream = Storage.OpenFile(SessionInfoPath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            var sessionElement = FindSessionElement(root, normalizedSessionId);
            if (sessionElement == null)
            {
                return false;
            }

            PopulateFromElement(sessionInfo, sessionElement);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {SessionInfoPath}: {ex.Message}");
            return false;
        }
    }

    private static string CreateRestartSessionId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static XElement LoadSessionRoot()
    {
        if (!Storage.FileExists(SessionInfoPath))
        {
            return new XElement("Sessions");
        }

        using var stream = Storage.OpenFile(SessionInfoPath, OpenFileMode.Read);
        var root = XElement.Load(stream);
        if (!string.Equals(root.Name.LocalName, "SessionInfo", StringComparison.Ordinal))
        {
            return root;
        }

        var sessions = new XElement("Sessions");
        sessions.Add(new XElement(root));
        return sessions;

    }

    private static XElement? FindSessionElement(XElement root, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        if (string.Equals(root.Name.LocalName, "SessionInfo", StringComparison.Ordinal))
        {
            var storedSessionId = NormalizeSessionId(root.Attribute(nameof(SessionInfo.SessionId))?.Value);
            return string.Equals(storedSessionId, sessionId, StringComparison.Ordinal) ? root : null;
        }

        return root.Elements("SessionInfo").FirstOrDefault(element =>
            string.Equals(
                NormalizeSessionId(element.Attribute(nameof(SessionInfo.SessionId))?.Value),
                sessionId,
                StringComparison.Ordinal));
    }

    private static XElement? FindSessionElementByName(XElement root, string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            return null;
        }

        if (string.Equals(root.Name.LocalName, "SessionInfo", StringComparison.Ordinal))
        {
            var storedSessionName = NormalizeSessionName(root.Attribute(nameof(SessionInfo.Name))?.Value);
            return string.Equals(storedSessionName, sessionName, StringComparison.Ordinal) ? root : null;
        }

        return root.Elements("SessionInfo").FirstOrDefault(element =>
            string.Equals(
                NormalizeSessionName(element.Attribute(nameof(SessionInfo.Name))?.Value),
                sessionName,
                StringComparison.Ordinal));
    }

    private static XElement CreateSessionElement(SessionInfo sessionInfo)
    {
        return new XElement("SessionInfo",
            new XAttribute(nameof(SessionInfo.SessionId), sessionInfo.SessionId),
            new XAttribute(nameof(SessionInfo.Name), sessionInfo.Name),
            new XAttribute(nameof(SessionInfo.Target), sessionInfo.Target),
            new XAttribute(nameof(SessionInfo.World), sessionInfo.World),
            new XAttribute(nameof(SessionInfo.Seed), sessionInfo.Seed),
            new XAttribute(nameof(SessionInfo.ServerHost), sessionInfo.ServerHost),
            new XAttribute(nameof(SessionInfo.ServerPort), sessionInfo.ServerPort),
            new XAttribute(nameof(SessionInfo.BroadcastPort), sessionInfo.BroadcastPort),
            new XAttribute(nameof(SessionInfo.Password), sessionInfo.Password));
    }

    private static void PopulateFromElement(SessionInfo sessionInfo, XElement element)
    {
        sessionInfo.SessionId = NormalizeSessionId(element.Attribute(nameof(SessionInfo.SessionId))?.Value);
        sessionInfo.Name = NormalizeSessionName(element.Attribute(nameof(SessionInfo.Name))?.Value);
        sessionInfo.Target = ParseTarget(element.Attribute(nameof(SessionInfo.Target))?.Value);
        sessionInfo.World = NormalizeWorld(element.Attribute(nameof(SessionInfo.World))?.Value);
        sessionInfo.Seed = element.Attribute(nameof(SessionInfo.Seed))?.Value ?? string.Empty;
        sessionInfo.ServerHost = element.Attribute(nameof(SessionInfo.ServerHost))?.Value ?? string.Empty;
        sessionInfo.ServerPort = ParseServerPort(element.Attribute(nameof(SessionInfo.ServerPort))?.Value);
        sessionInfo.BroadcastPort = ParseServerPort(element.Attribute(nameof(SessionInfo.BroadcastPort))?.Value);
        sessionInfo.Password = element.Attribute(nameof(SessionInfo.Password))?.Value ?? string.Empty;
    }

    private static SessionInfo CreateDefault(string sessionId)
    {
        return new SessionInfo
        {
            SessionId = sessionId,
            Name = string.Empty,
            Target = SessionTarget.MainMenu,
            World = "World",
            Seed = string.Empty,
            ServerHost = string.Empty,
            Password = string.Empty
        };
    }

    private static SessionInfo CreateSessionForStartup(RunningSetting runningSetting, string sessionId)
    {
        var explicitNamedSession = runningSetting.HasExplicitSessionRequest &&
                                   !string.IsNullOrWhiteSpace(runningSetting.RequestedSessionName);
        var createWorldSession =
            runningSetting.RunMode is RunModeType.HeadlessServer ||
            explicitNamedSession;
        var startupWorldName = !string.IsNullOrWhiteSpace(runningSetting.SessionWorldOverride)
            ? NormalizeWorld(runningSetting.SessionWorldOverride)
            : explicitNamedSession
                ? NormalizeWorld(runningSetting.RequestedSessionName)
                : "World";

        return new SessionInfo
        {
            SessionId = sessionId,
            Name = runningSetting.HasExplicitSessionRequest
                ? NormalizeSessionName(runningSetting.RequestedSessionName)
                : string.Empty,
            Target = createWorldSession ? SessionTarget.World : SessionTarget.MainMenu,
            World = startupWorldName,
            Seed = string.Empty
        };
    }

    private static void ApplySessionOverrides(SessionInfo sessionInfo, RunningSetting runningSetting)
    {
        if (!string.IsNullOrWhiteSpace(runningSetting.SessionConnectHostOverride) &&
            runningSetting.SessionConnectPortOverride is > 0)
        {
            sessionInfo.Target = SessionTarget.RemoteServer;
            sessionInfo.ServerHost = runningSetting.SessionConnectHostOverride;
            sessionInfo.ServerPort = runningSetting.SessionConnectPortOverride.Value;
        }
        else if (runningSetting.HasExplicitSessionRequest)
        {
            sessionInfo.Target = SessionTarget.World;
        }

        if (!string.IsNullOrWhiteSpace(runningSetting.SessionWorldOverride))
        {
            sessionInfo.World = NormalizeWorld(runningSetting.SessionWorldOverride);
        }

        if (runningSetting.SessionSeedOverride != null)
        {
            sessionInfo.Seed = runningSetting.SessionSeedOverride;
        }

        if (runningSetting.SessionServerPortOverride is { } serverPort)
        {
            sessionInfo.ServerPort = serverPort;
        }

        if (runningSetting.SessionBroadcastPortOverride is { } broadcastPort)
        {
            sessionInfo.BroadcastPort = broadcastPort;
        }
    }

    private static WorldInfo CreateWorld(string worldName, string? seed, bool runServer)
    {
        var worldSettings = new WorldSettings
        {
            Name = NormalizeWorld(worldName),
            Seed = string.IsNullOrWhiteSpace(seed) ? GenerateRandomSeed() : seed,
            RunServer = runServer,
            IsNeedCommunityLogin = false
        };
        var customWorldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, worldSettings.Name);
        Log.Information($"Creating new world with seed: {worldSettings.Seed}");
        return WorldsManager.CreateWorld(worldSettings, customWorldDirectoryName);
    }

    private static void Normalize(SessionInfo sessionInfo)
    {
        sessionInfo.SessionId = !IsValidSessionId(sessionInfo.SessionId)
            ? Guid.NewGuid().ToString("N")
            : NormalizeSessionId(sessionInfo.SessionId);
        sessionInfo.Name = NormalizeSessionName(sessionInfo.Name);
        sessionInfo.World = NormalizeWorld(sessionInfo.World);
        if (sessionInfo.ServerPort < 0)
        {
            sessionInfo.ServerPort = 0;
        }
    }

    private static bool SwitchTo(string screenName)
    {
        ScreensManager.SwitchScreen(screenName);
        return true;
    }

    private static SessionTarget ParseTarget(string? value)
    {
        return Enum.TryParse(value, true, out SessionTarget target)
            ? target
            : SessionTarget.MainMenu;
    }

    private static int ParseServerPort(string? value)
    {
        return int.TryParse(value, out var port) ? port : 0;
    }

    private static string NormalizeSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeWorld(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "World" : value;
    }

    private static string NormalizeSessionName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static void ConsumePendingSessionIfNeeded(RunningSetting runningSetting, SessionInfo sessionInfo)
    {
        if (runningSetting.HasExplicitSessionRequest ||
            string.IsNullOrWhiteSpace(runningSetting.PendingSessionId))
        {
            return;
        }

        var pendingSessionId = NormalizeSessionId(runningSetting.PendingSessionId);
        RunningSettingManager.ClearPendingSession();
        if (string.Equals(sessionInfo.SessionId, pendingSessionId, StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(sessionInfo.Name))
        {
            ModProfileManager.DeleteSessionProfile(pendingSessionId);
            Delete(pendingSessionId);
        }
    }

    public static void Delete(string? sessionId)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            return;
        }

        try
        {
            if (!Storage.FileExists(SessionInfoPath))
            {
                return;
            }

            var root = LoadSessionRoot();
            var existing = FindSessionElement(root, normalizedSessionId);
            if (existing == null)
            {
                return;
            }

            existing.Remove();
            using var stream = Storage.OpenFile(SessionInfoPath, OpenFileMode.Create);
            root.Save(stream);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to delete session \"{normalizedSessionId}\": {ex.Message}");
        }
    }

    private static string GenerateRandomSeed()
    {
        var seed = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        return seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

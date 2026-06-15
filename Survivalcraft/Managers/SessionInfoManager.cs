using System.Net;
using System.Security.Cryptography;
using System.Xml.Linq;

using Game.Network;
using Game.Network.Enums;
using Game.Screens;

namespace Game.Managers;

public static class SessionInfoManager
{
    public const string SessionInfoPath = "config:SessionInfo.xml";

    private static readonly HashSet<string> WorldListScreens =
    [
        "Play",
        "NewWorld",
        "ModifyWorld",
        "WorldOptions"
    ];

    public static SessionInfo Load(string? sessionId)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        var sessionInfo = CreateDefault(normalizedSessionId);
        try
        {
            if (!Storage.FileExists(SessionInfoPath))
            {
                return sessionInfo;
            }

            using var stream = Storage.OpenFile(SessionInfoPath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            var storedSessionId = NormalizeSessionId(root.Attribute(nameof(SessionInfo.SessionId))?.Value);
            if (!string.Equals(storedSessionId, normalizedSessionId, StringComparison.Ordinal))
            {
                return sessionInfo;
            }

            sessionInfo.SessionId = storedSessionId;
            sessionInfo.Kind = ParseKind(root.Attribute(nameof(SessionInfo.Kind))?.Value);
            sessionInfo.Action = ParseAction(root.Attribute(nameof(SessionInfo.Action))?.Value);
            sessionInfo.World = NormalizeWorld(root.Attribute(nameof(SessionInfo.World))?.Value);
            sessionInfo.Seed = root.Attribute(nameof(SessionInfo.Seed))?.Value ?? string.Empty;
            sessionInfo.ServerHost = root.Attribute(nameof(SessionInfo.ServerHost))?.Value ?? string.Empty;
            sessionInfo.ServerPort = ParseServerPort(root.Attribute(nameof(SessionInfo.ServerPort))?.Value);
            sessionInfo.Password = root.Attribute(nameof(SessionInfo.Password))?.Value ?? string.Empty;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {SessionInfoPath}: {ex.Message}");
        }

        return sessionInfo;
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

            using var stream = Storage.OpenFile(SessionInfoPath, OpenFileMode.Create);
            var root = new XElement("SessionInfo",
                new XAttribute(nameof(SessionInfo.SessionId), sessionInfo.SessionId),
                new XAttribute(nameof(SessionInfo.Kind), sessionInfo.Kind),
                new XAttribute(nameof(SessionInfo.Action), sessionInfo.Action),
                new XAttribute(nameof(SessionInfo.World), sessionInfo.World),
                new XAttribute(nameof(SessionInfo.Seed), sessionInfo.Seed),
                new XAttribute(nameof(SessionInfo.ServerHost), sessionInfo.ServerHost),
                new XAttribute(nameof(SessionInfo.ServerPort), sessionInfo.ServerPort),
                new XAttribute(nameof(SessionInfo.Password), sessionInfo.Password)
            );
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
        Normalize(effective);
        Save(effective);
        return effective;
    }

    public static SessionInfo CreateRemoteClientSession(IPEndPoint endPoint, string password)
    {
        var sessionInfo = Load(RunningSettingManager.Current.ActiveSessionId);
        sessionInfo.SessionId = NormalizeSessionId(RunningSettingManager.Current.ActiveSessionId);
        sessionInfo.Kind = SessionKind.RemoteClient;
        sessionInfo.Action = SessionRestoreAction.ConnectRemoteServer;
        sessionInfo.ServerHost = endPoint.Address.ToString();
        sessionInfo.ServerPort = endPoint.Port;
        sessionInfo.Password = password ?? string.Empty;
        return sessionInfo;
    }

    public static bool TryRestoreGuiSession()
    {
        var runningSetting = RunningSettingManager.Current;
        if (!runningSetting.ShouldEnterSession)
        {
            return false;
        }

        if (!runningSetting.HasExplicitSessionRequest &&
            !string.IsNullOrWhiteSpace(runningSetting.PendingSessionId))
        {
            RunningSettingManager.ClearPendingSession();
        }

        var sessionInfo = ResolveStartupSession(runningSetting);

        return sessionInfo.Action switch
        {
            SessionRestoreAction.OpenMainMenu => SwitchTo("MainMenu"),
            SessionRestoreAction.OpenWorldList => SwitchTo("Play"),
            SessionRestoreAction.OpenServerBrowser => SwitchTo("NetPlay"),
            SessionRestoreAction.LoadSingleplayerWorld => RestoreWorldSession(
                sessionInfo,
                startServer: false,
                createIfMissing: runningSetting.HasExplicitSessionRequest),
            SessionRestoreAction.LoadLocalServerWorld => RestoreWorldSession(
                sessionInfo,
                startServer: true,
                createIfMissing: runningSetting.HasExplicitSessionRequest),
            SessionRestoreAction.ConnectRemoteServer => RestoreRemoteClientSession(sessionInfo),
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
        if (!runningSetting.HasExplicitSessionRequest &&
            string.IsNullOrWhiteSpace(runningSetting.PendingSessionId) &&
            runningSetting.DefaultGuiStartupBehavior is not GuiStartupBehavior.EnterDefaultSession &&
            runningSetting.RunMode is not RunModeType.HeadlessServer)
        {
            return Load(sessionId);
        }

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
        if (!runningSetting.HasExplicitSessionRequest &&
            !string.IsNullOrWhiteSpace(runningSetting.PendingSessionId))
        {
            RunningSettingManager.ClearPendingSession();
        }

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
                OriginalSerializationVersion = VersionsManager.SerializationVersion,
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

            Log.Information($"Using existing world seed: {worldInfo.WorldSettings.Seed}");
        }

        return worldInfo;
    }

    private static void PopulateFromCurrentState(SessionInfo sessionInfo)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            sessionInfo.Kind = SessionKind.HeadlessServer;
            sessionInfo.Action = SessionRestoreAction.StartHeadlessServer;
            PopulateWorldContext(sessionInfo, forceServer: true);
            return;
        }

        if (TryCapturePendingLoadingSession(sessionInfo))
        {
            return;
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            sessionInfo.Kind = SessionKind.RemoteClient;
            if (TryGetCurrentRemoteConnectionContext(out var host, out var port))
            {
                sessionInfo.Action = SessionRestoreAction.ConnectRemoteServer;
                sessionInfo.ServerHost = host;
                sessionInfo.ServerPort = port;
            }
            else
            {
                sessionInfo.Action = SessionRestoreAction.OpenServerBrowser;
            }

            return;
        }

        if (PopulateWorldContext(sessionInfo, forceServer: false))
        {
            return;
        }

        var screenName = ScreensManager.GetCurrentScreenName();
        if (WorldListScreens.Contains(screenName))
        {
            sessionInfo.Kind = SessionKind.Singleplayer;
            sessionInfo.Action = SessionRestoreAction.OpenWorldList;
            return;
        }

        if (string.Equals(screenName, "NetPlay", StringComparison.Ordinal))
        {
            sessionInfo.Kind = SessionKind.RemoteClient;
            sessionInfo.Action = SessionRestoreAction.OpenServerBrowser;
            return;
        }

        sessionInfo.Kind = SessionKind.Gui;
        sessionInfo.Action = SessionRestoreAction.OpenMainMenu;
    }

    private static bool PopulateWorldContext(SessionInfo sessionInfo, bool forceServer)
    {
        if (GameManager.WorldInfo == null)
        {
            return false;
        }

        var startServer = forceServer ||
                          CommonLib.WorkType == WorkType.Server ||
                          GameManager.WorldInfo.WorldSettings.RunServer;
        sessionInfo.Kind = startServer ? SessionKind.LocalServer : SessionKind.Singleplayer;
        sessionInfo.Action = startServer
            ? SessionRestoreAction.LoadLocalServerWorld
            : SessionRestoreAction.LoadSingleplayerWorld;
        sessionInfo.World = NormalizeWorld(GameManager.WorldInfo.WorldSettings.Name);
        sessionInfo.Seed = GameManager.WorldInfo.WorldSettings.Seed ?? string.Empty;
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
            sessionInfo.Kind = SessionKind.RemoteClient;
            sessionInfo.Action = SessionRestoreAction.ConnectRemoteServer;
            sessionInfo.ServerHost = loadingScreen.CurrentServerEndPoint.Address.ToString();
            sessionInfo.ServerPort = loadingScreen.CurrentServerEndPoint.Port;
            sessionInfo.Password = loadingScreen.CurrentPassword;
            return true;
        }

        if (loadingScreen.CurrentWorldInfo == null)
        {
            return false;
        }

        var startServer = loadingScreen.CurrentWorldInfo.WorldSettings.RunServer || CommonLib.WorkType == WorkType.Server;
        sessionInfo.Kind = startServer ? SessionKind.LocalServer : SessionKind.Singleplayer;
        sessionInfo.Action = startServer
            ? SessionRestoreAction.LoadLocalServerWorld
            : SessionRestoreAction.LoadSingleplayerWorld;
        sessionInfo.World = NormalizeWorld(loadingScreen.CurrentWorldInfo.WorldSettings.Name);
        sessionInfo.Seed = loadingScreen.CurrentWorldInfo.WorldSettings.Seed ?? string.Empty;
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

    private static bool RestoreWorldSession(SessionInfo sessionInfo, bool startServer, bool createIfMissing)
    {
        if (!TryResolveExistingWorld(sessionInfo.World, out var worldInfo))
        {
            if (!createIfMissing)
            {
                Log.Warning($"Cannot restore session \"{sessionInfo.SessionId}\": world \"{sessionInfo.World}\" not found.");
                return false;
            }

            worldInfo = CreateWorld(sessionInfo.World, sessionInfo.Seed, runServer: startServer);
        }

        if (startServer)
        {
            if (!CommonLib.StartServer())
            {
                Log.Warning("Cannot restore hosted world: server port is already in use.");
                DialogsManager.Alert("恢复联机世界失败：端口被占用");
                ScreensManager.SwitchScreen("Play");
                return true;
            }
        }

        ScreensManager.SwitchScreen("GameLoading", worldInfo, string.Empty);
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

    private static bool TryResolveExistingWorld(string? worldName, out WorldInfo worldInfo)
    {
        WorldsManager.UpdateWorldsList();
        var normalizedWorld = NormalizeWorld(worldName);
        worldInfo = WorldsManager.WorldInfos.FirstOrDefault(w =>
                        string.Equals(w.DirectoryName, normalizedWorld, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(w.WorldSettings.Name, normalizedWorld, StringComparison.OrdinalIgnoreCase))
                    ?? null!;
        if (worldInfo != null)
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
        sessionInfo = CreateDefault(NormalizeSessionId(sessionId));
        try
        {
            if (!Storage.FileExists(SessionInfoPath))
            {
                return false;
            }

            using var stream = Storage.OpenFile(SessionInfoPath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            var storedSessionId = NormalizeSessionId(root.Attribute(nameof(SessionInfo.SessionId))?.Value);
            if (!string.Equals(storedSessionId, NormalizeSessionId(sessionId), StringComparison.Ordinal))
            {
                return false;
            }

            sessionInfo.SessionId = storedSessionId;
            sessionInfo.Kind = ParseKind(root.Attribute(nameof(SessionInfo.Kind))?.Value);
            sessionInfo.Action = ParseAction(root.Attribute(nameof(SessionInfo.Action))?.Value);
            sessionInfo.World = NormalizeWorld(root.Attribute(nameof(SessionInfo.World))?.Value);
            sessionInfo.Seed = root.Attribute(nameof(SessionInfo.Seed))?.Value ?? string.Empty;
            sessionInfo.ServerHost = root.Attribute(nameof(SessionInfo.ServerHost))?.Value ?? string.Empty;
            sessionInfo.ServerPort = ParseServerPort(root.Attribute(nameof(SessionInfo.ServerPort))?.Value);
            sessionInfo.Password = root.Attribute(nameof(SessionInfo.Password))?.Value ?? string.Empty;
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
        return $"restart-{Guid.NewGuid():N}";
    }

    private static SessionInfo CreateDefault(string sessionId)
    {
        return new SessionInfo
        {
            SessionId = sessionId,
            Kind = SessionKind.Gui,
            Action = SessionRestoreAction.OpenMainMenu,
            World = DefaultWorldForSession(sessionId),
            Seed = string.Empty,
            ServerHost = string.Empty,
            Password = string.Empty
        };
    }

    private static SessionInfo CreateSessionForStartup(RunningSetting runningSetting, string sessionId)
    {
        return new SessionInfo
        {
            SessionId = sessionId,
            Kind = runningSetting.RunMode is RunModeType.HeadlessServer
                ? SessionKind.HeadlessServer
                : SessionKind.Singleplayer,
            Action = runningSetting.RunMode is RunModeType.HeadlessServer
                ? SessionRestoreAction.StartHeadlessServer
                : SessionRestoreAction.LoadSingleplayerWorld,
            World = DefaultWorldForSession(sessionId),
            Seed = string.Empty
        };
    }

    private static void ApplySessionOverrides(SessionInfo sessionInfo, RunningSetting runningSetting)
    {
        if (!string.IsNullOrWhiteSpace(runningSetting.SessionWorldOverride))
        {
            sessionInfo.World = NormalizeWorld(runningSetting.SessionWorldOverride);
        }

        if (runningSetting.SessionSeedOverride != null)
        {
            sessionInfo.Seed = runningSetting.SessionSeedOverride;
        }
    }

    private static WorldInfo CreateWorld(string worldName, string? seed, bool runServer)
    {
        var worldSettings = new WorldSettings
        {
            Name = NormalizeWorld(worldName),
            Seed = string.IsNullOrWhiteSpace(seed) ? GenerateRandomSeed() : seed,
            OriginalSerializationVersion = VersionsManager.SerializationVersion,
            RunServer = runServer,
            IsNeedCommunityLogin = false
        };
        var customWorldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, worldSettings.Name);
        Log.Information($"Creating new world with seed: {worldSettings.Seed}");
        return WorldsManager.CreateWorld(worldSettings, customWorldDirectoryName);
    }

    private static void Normalize(SessionInfo sessionInfo)
    {
        sessionInfo.SessionId = NormalizeSessionId(sessionInfo.SessionId);
        sessionInfo.World = NormalizeWorld(sessionInfo.World);
        sessionInfo.Seed ??= string.Empty;
        sessionInfo.ServerHost ??= string.Empty;
        sessionInfo.Password ??= string.Empty;
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

    private static SessionKind ParseKind(string? value)
    {
        return Enum.TryParse(value, true, out SessionKind kind) ? kind : SessionKind.Gui;
    }

    private static SessionRestoreAction ParseAction(string? value)
    {
        return Enum.TryParse(value, true, out SessionRestoreAction action)
            ? action
            : SessionRestoreAction.OpenMainMenu;
    }

    private static int ParseServerPort(string? value)
    {
        return int.TryParse(value, out var port) ? port : 0;
    }

    private static string NormalizeSessionId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "default" : value;
    }

    private static string NormalizeWorld(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "World" : value;
    }

    private static string DefaultWorldForSession(string sessionId)
    {
        return string.Equals(sessionId, "default", StringComparison.Ordinal) ? "World" : sessionId;
    }

    private static string GenerateRandomSeed()
    {
        var seed = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        return seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

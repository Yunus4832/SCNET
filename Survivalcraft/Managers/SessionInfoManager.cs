using System.Xml.Linq;

using Engine.Network;

using Game.Network;

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

            var sessionInfo =
                CreateDefault(NormalizeSessionId(sessionElement.Attribute(nameof(SessionInfo.SessionId))?.Value));
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
        var sessionInfo = Load(StartupManager.Current.Session.SessionId);
        sessionInfo.SessionId = NormalizeSessionId(StartupManager.Current.Session.SessionId);
        sessionInfo.P2POverrides = StartupManager.Current.Session.P2POverrides;
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

    public static SessionInfo CreateRemoteClientSession(NetworkTarget target)
    {
        var sessionInfo = Clone(StartupManager.Current.Session);
        sessionInfo.Target = SessionTarget.RemoteServer;
        sessionInfo.RemoteTarget = target;
        return sessionInfo;
    }

    public static bool TryRestoreGuiSession()
    {
        var context = StartupManager.Current;
        var sessionInfo = context.Session;
        ConsumePendingSessionIfNeeded(context, sessionInfo);

        return sessionInfo.Target switch
        {
            SessionTarget.MainMenu => SwitchTo("MainMenu"),
            SessionTarget.WorldList => SwitchTo("Play"),
            SessionTarget.ServerBrowser => SwitchTo("NetPlay"),
            SessionTarget.World => RestoreWorldSession(
                sessionInfo,
                createIfMissing: context.Request.HasExplicitSession
            ),
            SessionTarget.RemoteServer => RestoreRemoteClientSession(sessionInfo),
            _ => false
        };
    }

    public static SessionInfo ResolveStartupSession(
        RunningSetting settings,
        StartupRequest request,
        string activeSessionId
    )
    {
        var sessionId = NormalizeSessionId(activeSessionId);
        if (!TryLoadExisting(sessionId, out var sessionInfo))
        {
            sessionInfo = CreateSessionForStartup(settings, request, sessionId);
        }

        ApplySessionOverrides(sessionInfo, request);
        Normalize(sessionInfo);
        return sessionInfo;
    }

    public static WorldInfo ResolveHeadlessWorld(StartupContext context)
    {
        var sessionInfo = context.Session;
        ConsumePendingSessionIfNeeded(context, sessionInfo);

        var worldArg = sessionInfo.World;
        var seedArg = sessionInfo.Seed;

        WorldsManager.UpdateWorldsList();
        var worlds = WorldsManager.WorldInfos.ToList();
        Log.Information($"Worlds directory: {GamePaths.Worlds}");
        Log.Information($"Detected worlds: {string.Join(", ", worlds.Select(w => w.WorldSettings.Name))}");

        var worldInfo = worlds.FirstOrDefault(w =>
            string.Equals(w.DirectoryName, worldArg, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.WorldSettings.Name, worldArg, StringComparison.OrdinalIgnoreCase)
        );
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
                GameMode = sessionInfo.GameMode ?? GameMode.Survival,
                TerrainGenerationMode = sessionInfo.TerrainGenerationMode ?? TerrainGenerationMode.Continent,
                TerrainLevel = sessionInfo.TerrainLevel ?? 64,
                RunServer = true
            };
            var customWorldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, worldArg);
            Log.Information($"Creating new world with seed: {worldSettings.Seed}");
            worldInfo = WorldsManager.CreateWorld(worldSettings, customWorldDirectoryName);
        }
        else
        {
            WarnIgnoredTerrainOptions(sessionInfo);
            if (!string.IsNullOrWhiteSpace(seedArg))
            {
                Log.Warning($"World already exists; ignoring provided seed \"{seedArg}\".");
            }

            if (!worldInfo.WorldSettings.RunServer)
            {
                Log.Warning(
                    $"World \"{worldInfo.WorldSettings.Name}\" is not marked as a server world. " +
                    "HeadlessServer will enable RunServer automatically."
                );
                worldInfo.WorldSettings.RunServer = true;
                WorldsManager.ChangeWorld(worldInfo.DirectoryName, worldInfo.WorldSettings);
            }

            worldInfo.GameModeOverride = sessionInfo.GameMode;

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
            if (CommonLib.Net.RemoteTarget is { } remoteTarget)
            {
                sessionInfo.Target = SessionTarget.RemoteServer;
                sessionInfo.RemoteTarget = remoteTarget;
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
        sessionInfo.GameMode = GameManager.WorldInfo.WorldSettings.GameMode;
        return true;
    }

    private static bool TryCapturePendingLoadingSession(SessionInfo sessionInfo)
    {
        if (ScreensManager.CurrentScreen is not GameLoadingScreen loadingScreen)
        {
            return false;
        }

        if (loadingScreen.CurrentRemoteTarget != null)
        {
            sessionInfo.Target = SessionTarget.RemoteServer;
            sessionInfo.RemoteTarget = loadingScreen.CurrentRemoteTarget;
            return true;
        }

        if (loadingScreen.CurrentWorldInfo == null)
        {
            return false;
        }

        sessionInfo.Target = SessionTarget.World;
        sessionInfo.World = NormalizeWorld(loadingScreen.CurrentWorldInfo.WorldSettings.Name);
        sessionInfo.Seed = loadingScreen.CurrentWorldInfo.WorldSettings.Seed;
        sessionInfo.GameMode = loadingScreen.CurrentWorldInfo.WorldSettings.GameMode;

        return true;
    }

    private static bool RestoreWorldSession(SessionInfo sessionInfo, bool createIfMissing)
    {
        if (!TryResolveExistingWorld(sessionInfo.World, out var worldInfo))
        {
            if (!createIfMissing)
            {
                Log.Warning(
                    $"Cannot restore session \"{sessionInfo.SessionId}\": world \"{sessionInfo.World}\" not found."
                );
                return false;
            }

            worldInfo = CreateWorld(
                sessionInfo.World,
                sessionInfo.Seed,
                sessionInfo.GameMode,
                sessionInfo.TerrainGenerationMode,
                sessionInfo.TerrainLevel,
                runServer: StartupManager.Current.Request.ForceWorldRunServer
            );
        }
        else
        {
            WarnIgnoredTerrainOptions(sessionInfo);
            worldInfo!.GameModeOverride = sessionInfo.GameMode;
            if (StartupManager.Current.Request.ForceWorldRunServer &&
                !worldInfo.WorldSettings.RunServer)
            {
                Log.Information(
                    $"Enabling RunServer for world \"{worldInfo.WorldSettings.Name}\" because --host was specified."
                );
                worldInfo.WorldSettings.RunServer = true;
                WorldsManager.ChangeWorld(worldInfo.DirectoryName, worldInfo.WorldSettings);
            }
        }

        var startServer = CommonLib.WorkType == WorkType.Server || worldInfo!.WorldSettings.RunServer;
        if (startServer)
        {
            if (!CommonLib.StartServer(sessionInfo, worldInfo!.WorldSettings))
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
        if (sessionInfo.RemoteTarget is not { } target)
        {
            Log.Warning($"Cannot restore remote session \"{sessionInfo.SessionId}\": invalid server endpoint.");
            ScreensManager.SwitchScreen("NetPlay");
            return true;
        }

        var netPlayScreen = ScreensManager.FindScreen<NetPlayScreen>("NetPlay", true)!;
        ScreensManager.SwitchScreen("NetPlay");
        netPlayScreen.ConnectToRemoteSession(target);
        return true;
    }

    private static bool TryResolveExistingWorld(string? worldName, out WorldInfo? worldInfo)
    {
        WorldsManager.UpdateWorldsList();
        var normalizedWorld = NormalizeWorld(worldName);
        worldInfo = WorldsManager.WorldInfos.FirstOrDefault(w =>
                        string.Equals(w.DirectoryName, normalizedWorld, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(w.WorldSettings.Name, normalizedWorld, StringComparison.OrdinalIgnoreCase)
                    ) ??
                    null;
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
                StringComparison.Ordinal
            )
        );
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
                StringComparison.Ordinal
            )
        );
    }

    private static XElement CreateSessionElement(SessionInfo sessionInfo)
    {
        var element = new XElement(
            "SessionInfo",
            new XAttribute(nameof(SessionInfo.SessionId), sessionInfo.SessionId),
            new XAttribute(nameof(SessionInfo.Name), sessionInfo.Name),
            new XAttribute(nameof(SessionInfo.Target), sessionInfo.Target),
            new XAttribute(nameof(SessionInfo.World), sessionInfo.World),
            new XAttribute(nameof(SessionInfo.Seed), sessionInfo.Seed),
            new XAttribute(nameof(SessionInfo.ServerPort), sessionInfo.ServerPort),
            new XAttribute(nameof(SessionInfo.BroadcastPort), sessionInfo.BroadcastPort)
        );
        if (sessionInfo.RemoteTarget is not null)
        {
            element.Add(CreateRemoteTargetElement(sessionInfo.RemoteTarget));
        }

        if (sessionInfo.GameMode is { } gameMode)
        {
            element.Add(new XAttribute(nameof(SessionInfo.GameMode), gameMode));
        }

        if (sessionInfo.TerrainGenerationMode is { } terrain)
        {
            element.Add(new XAttribute(nameof(SessionInfo.TerrainGenerationMode), terrain));
        }

        if (sessionInfo.TerrainLevel is { } level)
        {
            element.Add(new XAttribute(nameof(SessionInfo.TerrainLevel), level));
        }

        var p2p = new XElement(nameof(SessionInfo.P2POverrides));
        if (sessionInfo.P2POverrides.Enabled is { } enabled)
        {
            p2p.Add(new XAttribute(nameof(P2PRoomOverrides.Enabled), enabled));
        }

        if (sessionInfo.P2POverrides.ControlUrl is { } controlUrl)
        {
            p2p.Add(new XAttribute(nameof(P2PRoomOverrides.ControlUrl), controlUrl));
        }

        if (sessionInfo.P2POverrides.Name is { } name)
        {
            p2p.Add(new XAttribute(nameof(P2PRoomOverrides.Name), name));
        }

        if (sessionInfo.P2POverrides.Description is { } description)
        {
            p2p.Add(new XAttribute(nameof(P2PRoomOverrides.Description), description));
        }

        if (sessionInfo.P2POverrides.IsPublic is { } isPublic)
        {
            p2p.Add(new XAttribute(nameof(P2PRoomOverrides.IsPublic), isPublic));
        }

        if (p2p.HasAttributes)
        {
            element.Add(p2p);
        }

        if (sessionInfo.HttpCommandEnabled is { } httpCommandEnabled)
        {
            element.Add(new XAttribute(nameof(SessionInfo.HttpCommandEnabled), httpCommandEnabled));
        }

        if (sessionInfo.HttpCommandPort is { } httpCommandPort)
        {
            element.Add(new XAttribute(nameof(SessionInfo.HttpCommandPort), httpCommandPort));
        }

        if (!string.IsNullOrWhiteSpace(sessionInfo.AutoJoinPlayerName))
        {
            element.Add(
                new XAttribute(
                    nameof(SessionInfo.AutoJoinPlayerName),
                    sessionInfo.AutoJoinPlayerName
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(sessionInfo.HttpCommandAccessToken))
        {
            element.Add(
                new XAttribute(
                    nameof(SessionInfo.HttpCommandAccessToken),
                    sessionInfo.HttpCommandAccessToken
                )
            );
        }

        return element;
    }

    private static void PopulateFromElement(SessionInfo sessionInfo, XElement element)
    {
        sessionInfo.SessionId = NormalizeSessionId(element.Attribute(nameof(SessionInfo.SessionId))?.Value);
        sessionInfo.Name = NormalizeSessionName(element.Attribute(nameof(SessionInfo.Name))?.Value);
        sessionInfo.Target = ParseTarget(element.Attribute(nameof(SessionInfo.Target))?.Value);
        sessionInfo.World = NormalizeWorld(element.Attribute(nameof(SessionInfo.World))?.Value);
        sessionInfo.Seed = element.Attribute(nameof(SessionInfo.Seed))?.Value ?? string.Empty;
        sessionInfo.GameMode = ParseGameMode(element.Attribute(nameof(SessionInfo.GameMode))?.Value);
        sessionInfo.TerrainGenerationMode = element.Attribute(nameof(SessionInfo.TerrainGenerationMode)) is { } terrain
            ? Enum.Parse<TerrainGenerationMode>(terrain.Value, true)
            : null;
        sessionInfo.TerrainLevel = element.Attribute(nameof(SessionInfo.TerrainLevel)) is { } level
            ? int.Parse(level.Value)
            : null;
        var p2p = element.Element(nameof(SessionInfo.P2POverrides));
        sessionInfo.P2POverrides = new P2PRoomOverrides
        {
            Enabled = (bool?)p2p?.Attribute(nameof(P2PRoomOverrides.Enabled)),
            ControlUrl = (string?)p2p?.Attribute(nameof(P2PRoomOverrides.ControlUrl)),
            Name = (string?)p2p?.Attribute(nameof(P2PRoomOverrides.Name)),
            Description = (string?)p2p?.Attribute(nameof(P2PRoomOverrides.Description)),
            IsPublic = (bool?)p2p?.Attribute(nameof(P2PRoomOverrides.IsPublic))
        };
        sessionInfo.RemoteTarget = element.Element("RemoteTarget") is { } remote ? ReadRemoteTarget(remote) : null;
        sessionInfo.ServerPort = ParseServerPort(element.Attribute(nameof(SessionInfo.ServerPort))?.Value);
        sessionInfo.BroadcastPort = ParseServerPort(element.Attribute(nameof(SessionInfo.BroadcastPort))?.Value);
        sessionInfo.HttpCommandEnabled = ParseOptionalBoolean(
            element.Attribute(nameof(SessionInfo.HttpCommandEnabled))?.Value
        );
        sessionInfo.HttpCommandPort = ParseOptionalPort(element.Attribute(nameof(SessionInfo.HttpCommandPort))?.Value);
        sessionInfo.AutoJoinPlayerName = NormalizeOptionalText(
            element.Attribute(nameof(SessionInfo.AutoJoinPlayerName))?.Value
        );
        sessionInfo.HttpCommandAccessToken =
            element.Attribute(nameof(SessionInfo.HttpCommandAccessToken))?.Value;
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
            RemoteTarget = null
        };
    }

    private static SessionInfo CreateSessionForStartup(
        RunningSetting settings,
        StartupRequest request,
        string sessionId
    )
    {
        var explicitNamedSession = request.HasExplicitSession &&
                                   !string.IsNullOrWhiteSpace(request.SessionName);
        var createWorldSession =
            settings.RunMode is RunModeType.HeadlessServer ||
            explicitNamedSession;
        var startupWorldName = !string.IsNullOrWhiteSpace(request.World)
            ? NormalizeWorld(request.World)
            : explicitNamedSession
                ? NormalizeWorld(request.SessionName)
                : "World";

        return new SessionInfo
        {
            SessionId = sessionId,
            Name = request.HasExplicitSession
                ? NormalizeSessionName(request.SessionName)
                : string.Empty,
            Target = createWorldSession ? SessionTarget.World : SessionTarget.MainMenu,
            World = startupWorldName,
            Seed = string.Empty
        };
    }

    private static void ApplySessionOverrides(SessionInfo sessionInfo, StartupRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ConnectHost) &&
            request.ConnectPort is > 0)
        {
            sessionInfo.Target = SessionTarget.RemoteServer;
            sessionInfo.RemoteTarget = new AddressNetworkTarget(request.ConnectHost, request.ConnectPort.Value);
        }
        else if (request.HasExplicitSession)
        {
            sessionInfo.Target = SessionTarget.World;
            sessionInfo.RemoteTarget = null;
        }

        if (!string.IsNullOrWhiteSpace(request.World))
        {
            sessionInfo.World = NormalizeWorld(request.World);
        }

        if (request.Seed != null)
        {
            sessionInfo.Seed = request.Seed;
        }

        if (request.GameMode is { } gameMode)
        {
            sessionInfo.GameMode = gameMode;
        }

        if (request.TerrainGenerationMode is { } terrain)
        {
            sessionInfo.TerrainGenerationMode = terrain;
        }

        if (request.TerrainLevel is { } level)
        {
            sessionInfo.TerrainLevel = level;
        }

        if (request.ServerPort is { } serverPort)
        {
            sessionInfo.ServerPort = serverPort;
        }

        if (request.BroadcastPort is { } broadcastPort)
        {
            sessionInfo.BroadcastPort = broadcastPort;
        }

        if (request.AutoJoinPlayerName is not null)
        {
            sessionInfo.AutoJoinPlayerName = request.AutoJoinPlayerName;
        }

        sessionInfo.P2POverrides = sessionInfo.P2POverrides.Merge(request.P2POverrides);

        if (request.HttpCommandEnabled is { } httpCommandEnabled)
        {
            sessionInfo.HttpCommandEnabled = httpCommandEnabled;
        }

        if (request.HttpCommandPort is { } httpCommandPort)
        {
            sessionInfo.HttpCommandPort = httpCommandPort;
        }

        if (request.HttpCommandAccessToken is not null)
        {
            sessionInfo.HttpCommandAccessToken = request.HttpCommandAccessToken;
        }
    }

    private static WorldInfo CreateWorld(
        string worldName,
        string? seed,
        GameMode? gameMode,
        TerrainGenerationMode? terrainGenerationMode,
        int? terrainLevel,
        bool runServer
    )
    {
        var worldSettings = new WorldSettings
        {
            Name = NormalizeWorld(worldName),
            Seed = string.IsNullOrWhiteSpace(seed) ? GenerateRandomSeed() : seed,
            GameMode = gameMode ?? GameMode.Survival,
            TerrainGenerationMode = terrainGenerationMode ?? TerrainGenerationMode.Continent,
            TerrainLevel = terrainLevel ?? 64,
            RunServer = runServer
        };
        var customWorldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, worldSettings.Name);
        Log.Information($"Creating new world with seed: {worldSettings.Seed}");
        return WorldsManager.CreateWorld(worldSettings, customWorldDirectoryName);
    }

    private static void Normalize(SessionInfo sessionInfo)
    {
        if (sessionInfo.Target != SessionTarget.RemoteServer)
        {
            sessionInfo.RemoteTarget = null;
        }

        if (sessionInfo.TerrainGenerationMode is { } terrain &&
            (!Enum.IsDefined(terrain) || TerrainGenerationModes.IsLegacy(terrain)))
        {
            throw new ArgumentException("Session terrain must use a current generation mode.");
        }

        if (sessionInfo.TerrainLevel is < 2 or > 252)
        {
            throw new ArgumentException("Session terrain level must be between 2 and 252.");
        }

        sessionInfo.SessionId = !IsValidSessionId(sessionInfo.SessionId)
            ? Guid.NewGuid().ToString("N")
            : NormalizeSessionId(sessionInfo.SessionId);
        sessionInfo.Name = NormalizeSessionName(sessionInfo.Name);
        sessionInfo.World = NormalizeWorld(sessionInfo.World);
        sessionInfo.AutoJoinPlayerName = NormalizeOptionalText(sessionInfo.AutoJoinPlayerName);
        if (sessionInfo.ServerPort < 0)
        {
            sessionInfo.ServerPort = 0;
        }
    }

    private static void WarnIgnoredTerrainOptions(SessionInfo sessionInfo)
    {
        if (sessionInfo.TerrainGenerationMode != null || sessionInfo.TerrainLevel != null)
        {
            Log.Warning(
                "World already exists; ignoring terrain creation options without changing saved terrain settings."
            );
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

    private static int? ParseOptionalPort(string? value)
    {
        return int.TryParse(value, out var port) ? port : null;
    }

    private static bool? ParseOptionalBoolean(string? value)
    {
        return bool.TryParse(value, out var result) ? result : null;
    }

    private static GameMode? ParseGameMode(string? value)
    {
        return Enum.TryParse<GameMode>(value, true, out var gameMode) &&
               Enum.IsDefined(gameMode)
            ? gameMode
            : null;
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

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static XElement CreateRemoteTargetElement(NetworkTarget target) => target switch
    {
        AddressNetworkTarget address => new XElement(
            "RemoteTarget",
            new XAttribute("Kind", "Address"),
            new XAttribute("Host", address.Host),
            new XAttribute("Port", address.Port)
        ),
        InvitationCodeNetworkTarget code => new XElement(
            "RemoteTarget",
            new XAttribute("Kind", "InvitationCode"),
            new XAttribute("ControlAddress", code.ControlAddress),
            new XAttribute("Code", code.JoinCode),
            new XAttribute("RelayPolicy", code.RelayPolicy)
        ),
        InvitationSessionNetworkTarget session => new XElement(
            "RemoteTarget",
            new XAttribute("Kind", "InvitationSession"),
            new XAttribute("ControlAddress", session.ControlAddress),
            new XAttribute("SessionId", session.SessionId),
            new XAttribute("RelayPolicy", session.RelayPolicy)
        ),
        _ => throw new InvalidOperationException("Unknown remote target.")
    };

    private static NetworkTarget ReadRemoteTarget(XElement element)
    {
        string Required(string name) => element.Attribute(name)?.Value ??
                                        throw new FormatException($"Missing remote target {name}.");

        return Required("Kind") switch
        {
            "Address" => new AddressNetworkTarget(Required("Host"), int.Parse(Required("Port"))),
            "InvitationCode" => new InvitationCodeNetworkTarget(
                new Uri(Required("ControlAddress")),
                Required("Code"),
                Enum.Parse<RelayPolicy>(Required("RelayPolicy"))
            ),
            "InvitationSession" => new InvitationSessionNetworkTarget(
                new Uri(Required("ControlAddress")),
                Guid.Parse(Required("SessionId")),
                Enum.Parse<RelayPolicy>(Required("RelayPolicy"))
            ),
            _ => throw new FormatException("Unknown remote target kind.")
        };
    }

    private static SessionInfo Clone(SessionInfo source)
    {
        return new SessionInfo
        {
            SessionId = NormalizeSessionId(source.SessionId),
            Name = source.Name,
            Target = source.Target,
            World = source.World,
            Seed = source.Seed,
            GameMode = source.GameMode,
            TerrainGenerationMode = source.TerrainGenerationMode,
            TerrainLevel = source.TerrainLevel,
            RemoteTarget = source.RemoteTarget,
            P2POverrides = source.P2POverrides,
            ServerPort = source.ServerPort,
            BroadcastPort = source.BroadcastPort,
            AutoJoinPlayerName = source.AutoJoinPlayerName,
            HttpCommandEnabled = source.HttpCommandEnabled,
            HttpCommandPort = source.HttpCommandPort,
            HttpCommandAccessToken = source.HttpCommandAccessToken
        };
    }

    private static void ConsumePendingSessionIfNeeded(
        StartupContext context,
        SessionInfo sessionInfo
    )
    {
        if (context.Request.HasExplicitSession ||
            string.IsNullOrWhiteSpace(context.Settings.PendingSessionId))
        {
            return;
        }

        var pendingSessionId = NormalizeSessionId(context.Settings.PendingSessionId);
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
        return WorldSeedConverter.CreateRandomTextSeed();
    }
}

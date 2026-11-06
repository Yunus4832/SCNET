using Engine.Network;

using Game.Application;
using Game.FileStorage;
using Game.Network;

using static Game.Startup.SessionInfoSerializer;

namespace Game.Managers;

public static class SessionInfoManager
{
    private static readonly HashSet<string> _worldListScreens =
    [
        "Play",
        "NewWorld",
        "ModifyWorld",
        "WorldOptions"
    ];

    public static SessionInfo CaptureCurrentSession()
    {
        var loading = (ScreensManager.CurrentScreen as GameLoadingScreen)?.CurrentWorldSession;
        var sessionInfo = Clone(loading ?? GameManager.WorldSession ?? StartupManager.Current.Session);
        PopulateFromCurrentState(sessionInfo);
        Normalize(sessionInfo);
        return sessionInfo;
    }

    public static SessionInfo PrepareRestartSession(SessionInfo? sessionInfo = null)
    {
        return StartupSessionLifecycle.PrepareRestart(sessionInfo ?? CaptureCurrentSession(), StartupManager.Profiles);
    }

    public static SessionInfo CreateWorldSession(WorldInfo world, SessionTarget target = SessionTarget.World)
    {
        return new SessionInfo { Target = target, World = world.DirectoryName };
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
        StartupSessionLifecycle.ConsumePending(context, StartupManager.Profiles);

        return sessionInfo.Target switch
        {
            SessionTarget.MainMenu => SwitchTo("MainMenu"),
            SessionTarget.WorldList => SwitchTo("Play"),
            SessionTarget.ServerBrowser => SwitchTo("NetPlay"),
            SessionTarget.World or SessionTarget.HostedWorld => RestoreWorldSession(
                sessionInfo,
                createIfMissing: context.Request.HasExplicitSession
            ),
            SessionTarget.RemoteServer => RestoreRemoteClientSession(sessionInfo),
            _ => false
        };
    }


    private static void PopulateFromCurrentState(SessionInfo sessionInfo)
    {
        if (TryCapturePendingLoadingSession(sessionInfo))
        {
            return;
        }

        if (CommonLib.Net.Role == NodeRole.Client)
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

        sessionInfo.Target = GameManager.WorldSession?.Target ?? SessionTarget.World;
        sessionInfo.World = GameManager.WorldInfo.DirectoryName;
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

        sessionInfo.Target = loadingScreen.CurrentWorldSession?.Target ?? SessionTarget.World;
        sessionInfo.World = loadingScreen.CurrentWorldInfo.DirectoryName;
        sessionInfo.Seed = loadingScreen.CurrentWorldInfo.WorldSettings.Seed;
        sessionInfo.GameMode = loadingScreen.CurrentWorldInfo.WorldSettings.GameMode;

        return true;
    }

    private static bool RestoreWorldSession(SessionInfo sessionInfo, bool createIfMissing)
    {
        var worldInfo = ResolveWorldSession(
            sessionInfo,
            createIfMissing,
            StartupManager.Current.Request.ForceWorldRunServer
        );
        if (worldInfo is null) { return false; }

        ScreensManager.SwitchScreen("GameLoading", worldInfo, string.Empty, sessionInfo);
        return true;
    }

    internal static WorldInfo? ResolveWorldSession(
        SessionInfo sessionInfo,
        bool createIfMissing,
        bool forceWorldRunServer
    )
    {
        createIfMissing |= sessionInfo.CreateWorldIfMissing;
        forceWorldRunServer |= sessionInfo.ForceWorldRunServer;
        if (!TryResolveExistingWorld(sessionInfo.World, out var worldInfo))
        {
            if (!createIfMissing)
            {
                Log.Warning(
                    $"Cannot restore session \"{sessionInfo.SessionId}\": world \"{sessionInfo.World}\" not found."
                );
                return null;
            }

            worldInfo = CreateWorld(
                sessionInfo.World,
                sessionInfo.Seed,
                sessionInfo.GameMode,
                sessionInfo.TerrainGenerationMode,
                sessionInfo.TerrainLevel,
                runServer: forceWorldRunServer
            );
        }
        else
        {
            WarnIgnoredTerrainOptions(sessionInfo);
            worldInfo!.GameModeOverride = sessionInfo.GameMode;
            if (forceWorldRunServer &&
                !worldInfo.Hosting.RunServer)
            {
                Log.Information(
                    $"Enabling RunServer for world \"{worldInfo.WorldSettings.Name}\" because --host was specified."
                );
                worldInfo.Hosting.RunServer = true;
                ClientApplication.Worlds.ChangeWorld(
                    worldInfo.DirectoryName,
                    worldInfo.WorldSettings,
                    worldInfo.Hosting
                );
            }
        }

        if (sessionInfo.Target == SessionTarget.HostedWorld) { worldInfo!.Hosting.RunServer = true; }

        sessionInfo.CreateWorldIfMissing = false;
        sessionInfo.ForceWorldRunServer = false;
        return worldInfo;
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
        ClientApplication.Worlds.UpdateWorldsList();
        var normalizedWorld = NormalizeWorld(worldName);
        worldInfo = ClientApplication.Worlds.WorldInfos.FirstOrDefault(w =>
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

        var resolvedWorldInfo = ClientApplication.Worlds.GetWorldInfo(worldPath);
        if (resolvedWorldInfo == null)
        {
            return false;
        }

        worldInfo = resolvedWorldInfo;
        return true;
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
            TerrainLevel = terrainLevel ?? 64
        };
        var customWorldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, worldSettings.Name);
        Log.Information($"Creating new world with seed: {worldSettings.Seed}");
        return ClientApplication.Worlds.CreateWorld(
            DatabaseManager.GameDatabase,
            worldSettings,
            new WorldHostingSettings { RunServer = runServer },
            customWorldDirectoryName
        );
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


    private static string GenerateRandomSeed()
    {
        return WorldSeedConverter.CreateRandomTextSeed();
    }
}

namespace Game.Managers;

public static class GameExitManager
{
    public static GameExitAction ExitAction { get; private set; }

    public static event Action<GameExitAction>? ExitRequested;

    public static string SwitchInstanceId { get; private set; } = string.Empty;

    public static string UpdatePackagePath { get; private set; } = string.Empty;

    public static string UpdateDownloadUrl { get; private set; } = string.Empty;

    public static void RequestUpdate(string packagePath, string downloadUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadUrl);
        SaveWorldBeforeExit();
        UpdatePackagePath = packagePath;
        UpdateDownloadUrl = downloadUrl;
        ExitAction = GameExitAction.Update;
        ExitRequested?.Invoke(ExitAction);
        RequestApplicationExit();
    }

    public static void RequestRestart(SessionInfo? sessionInfo = null)
    {
        RequestRestartInternal(sessionInfo, null);
    }

    public static void RequestRestart(SessionInfo sessionInfo, ModProfile sessionProfile)
    {
        RequestRestartInternal(sessionInfo, sessionProfile);
    }

    public static void RequestInstanceSwitch(string instanceId)
    {
        SaveWorldBeforeExit();
        StarterInstanceManager.RequestSwitch(instanceId);
        SwitchInstanceId = instanceId;
        ExitAction = GameExitAction.SwitchInstance;
        ExitRequested?.Invoke(ExitAction);
        RequestApplicationExit();
    }

    public static void RequestExit()
    {
        SaveWorldBeforeExit();
        ExitAction = GameExitAction.Exit;
        ExitRequested?.Invoke(ExitAction);
        RequestApplicationExit();
    }

    internal static void BeginSession()
    {
        ExitAction = GameExitAction.Exit;
        SwitchInstanceId = string.Empty;
        UpdatePackagePath = string.Empty;
        UpdateDownloadUrl = string.Empty;
    }

    private static void RequestRestartInternal(SessionInfo? sessionInfo, ModProfile? sessionProfile)
    {
        SaveWorldBeforeExit();
        var pendingSessionId = string.Empty;
        if (sessionInfo != null)
        {
            var pendingSession = SessionInfoManager.PrepareRestartSession(sessionInfo);
            pendingSessionId = pendingSession.SessionId;
            if (sessionProfile != null)
            {
                sessionProfile.Id = pendingSessionId;
                ModProfileManager.SaveSessionProfile(sessionProfile);
            }
        }

        StartupManager.Profiles.UpdateRunningSettings(runningSetting =>
            {
                runningSetting.PendingSessionId = pendingSessionId;
            }
        );
        ExitAction = GameExitAction.Restart;
        ExitRequested?.Invoke(ExitAction);
        RequestApplicationExit();
    }

    private static void SaveWorldBeforeExit()
    {
        GameManager.SaveProject(waitForCompletion: true, showErrorDialog: true);
    }

    private static void RequestApplicationExit()
    {
        if (Window.IsCreated) { Window.Close(); }
    }
}

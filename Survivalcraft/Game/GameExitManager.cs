namespace Game;

public enum GameExitAction
{
    Exit,
    Restart
}

public static class GameExitManager
{
    public static GameExitAction ExitAction { get; private set; }

    public static event Action<GameExitAction>? ExitRequested;

    public static void RequestRestart(SessionInfo? sessionInfo = null)
    {
        RequestRestartInternal(sessionInfo, null);
    }

    public static void RequestRestart(SessionInfo sessionInfo, ModProfile sessionProfile)
    {
        RequestRestartInternal(sessionInfo, sessionProfile);
    }

    internal static void BeginSession()
    {
        ExitAction = GameExitAction.Exit;
    }

    private static void RequestRestartInternal(SessionInfo? sessionInfo, ModProfile? sessionProfile)
    {
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

        RunningSettingManager.SaveCurrent(runningSetting =>
        {
            runningSetting.PendingSessionId = pendingSessionId;
        });
        ExitAction = GameExitAction.Restart;
        ExitRequested?.Invoke(ExitAction);
        Window.Close();
    }
}

namespace Game;

using Game.Managers;

public enum GameExitAction
{
    Exit,
    Restart,
    SwitchInstance
}

public static class GameExitManager
{
    public static GameExitAction ExitAction { get; private set; }

    public static event Action<GameExitAction>? ExitRequested;

    public static string SwitchInstanceId { get; private set; } = string.Empty;

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
        StarterInstanceManager.RequestSwitch(instanceId);
        SwitchInstanceId = instanceId;
        ExitAction = GameExitAction.SwitchInstance;
        ExitRequested?.Invoke(ExitAction);
        RequestApplicationExit();
    }

    internal static void BeginSession()
    {
        ExitAction = GameExitAction.Exit;
        SwitchInstanceId = string.Empty;
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
        RequestApplicationExit();
    }

    private static void RequestApplicationExit()
    {
        if (RunMode.Value is RunModeType.Gui)
        {
            Window.Close();
        }
        else
        {
            HeadlessEntry.RequestStop();
        }
    }
}

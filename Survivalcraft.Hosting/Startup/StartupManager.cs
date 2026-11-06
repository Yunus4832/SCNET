using Game.FileStorage;
using Game.Startup;

namespace Game.Managers;

public static class StartupManager
{
    public static StartupProfileStore Profiles { get; } = new(GamePaths.RunningSettingFile, GamePaths.SessionInfoFile);

    public static StartupContext Current { get; private set; } = new(
        new RunningSetting(),
        new StartupRequest(),
        new SessionInfo()
    );

    public static StartupContext Load(string[] args, SessionTarget defaultTarget = SessionTarget.MainMenu)
    {
        Current = Profiles.LoadStartup(args, defaultTarget: defaultTarget);
        return Current;
    }
}

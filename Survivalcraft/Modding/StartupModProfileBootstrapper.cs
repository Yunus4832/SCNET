namespace Game.Modding;

/// <summary>在 GUI 模组运行时初始化前准备启动目标的精确包组合。</summary>
public static class StartupModProfileBootstrapper
{
    public static ModLoadPlan PrepareLoadPlan(
        SessionInfo startupSession,
        string localRepositoryPath,
        Action<string>? log = null
    )
    {
        ArgumentNullException.ThrowIfNull(startupSession);
        var profile = ModProfileManager.ResolveStartupProfile(startupSession);
        if (startupSession.Target is SessionTarget.World or SessionTarget.HostedWorld && profile.Packages.Count > 0)
        {
            ModProfileResolver.EnsurePackagesAvailable(profile, localRepositoryPath, log);
        }

        return startupSession.Target == SessionTarget.RemoteServer
            ? ModLoadPlan.ReadClientProfile(profile, localRepositoryPath, log)
            : ModLoadPlan.Read(profile, localRepositoryPath, log);
    }
}

using static Game.Startup.SessionInfoSerializer;

namespace Game.Startup;

public static class StartupSessionLifecycle
{
    public static SessionInfo PrepareRestart(SessionInfo session, StartupProfileStore profiles)
    {
        var effective = Clone(session);
        effective.SessionId = Guid.NewGuid().ToString("N");
        effective.Name = string.Empty;
        Normalize(effective);
        profiles.SaveSession(effective);
        return effective;
    }

    public static void ConsumePending(StartupContext context, StartupProfileStore profiles)
    {
        var sessionInfo = context.Session;
        if (context.Request.HasExplicitSession ||
            string.IsNullOrWhiteSpace(context.Settings.PendingSessionId))
        {
            return;
        }

        var pendingSessionId = NormalizeSessionId(context.Settings.PendingSessionId);
        profiles.ClearPendingSession();
        if (string.Equals(sessionInfo.SessionId, pendingSessionId, StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(sessionInfo.Name))
        {
            ModProfileManager.DeleteSessionProfile(pendingSessionId);
            profiles.DeleteSession(pendingSessionId);
        }
    }
}

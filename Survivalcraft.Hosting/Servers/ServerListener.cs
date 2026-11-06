using Game.Admission;
using Game.Startup;

namespace Game.Servers;

public static class ServerListener
{
    public static bool Start(
        NetNode node,
        Configuration.HostPreferences preferences,
        LocalIdentityManager identities,
        SessionInfo sessionInfo,
        WorldSettings worldSettings,
        WorldHostingSettings hosting,
        ServerAdmissionPolicy? policyOverride = null
    )
    {
        var serverPort = sessionInfo.ServerPort > 0
            ? sessionInfo.ServerPort
            : preferences.ServerPort;
        int? broadcastPort = sessionInfo.BroadcastPort switch
        {
            < 0 => null,
            > 0 => sessionInfo.BroadcastPort,
            _ => preferences.BroadcastPort
        };
        var effectivePolicy = policyOverride ??
                              ServerAdmissionManager.Resolve(
                                  sessionInfo.AdmissionOverrides,
                                  ServerAdmissionPolicy.Load(
                                      (node.Runtime.Project ??
                                       throw new InvalidOperationException(
                                           "Load the authority world before starting its listener."
                                       ))
                                      .ExtensionData.Get("game", "admission")
                                  )
                              );
        var started = node.StartServer(
            serverPort,
            broadcastPort,
            worldSettings.MaxOnlinePlayerCount,
            hosting.P2P.ApplyOverrides(sessionInfo.P2POverrides, worldSettings.Name),
            effectivePolicy,
            ServerIdentitySelection.Resolve(identities, sessionInfo, hosting)
        );

        return started;
    }
}

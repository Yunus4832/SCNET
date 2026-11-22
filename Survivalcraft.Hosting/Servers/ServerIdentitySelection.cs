using Game.Startup;

namespace Game.Servers;

/// <summary>实例身份与世界设置可用后确定宿主所用身份。</summary>
public static class ServerIdentitySelection
{
    public static string Resolve(
        LocalIdentityManager identities,
        SessionInfo? session,
        WorldHostingSettings world
    )
    {
        using var identity =
            identities.OpenServerIdentity(session?.ServerIdentityAddress ?? world.ServerIdentityAddress);
        return identity.PublicIdentity.Address;
    }
}

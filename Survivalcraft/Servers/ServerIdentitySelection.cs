namespace Game.Servers;

/// <summary>Resolves host identity selection after instance identities and world settings are available.</summary>
public static class ServerIdentitySelection
{
    public static string Resolve(
        LocalIdentityManager identities,
        SessionInfo? session,
        WorldSettings world
    )
    {
        using var identity =
            identities.OpenServerIdentity(session?.ServerIdentityAddress ?? world.ServerIdentityAddress);
        return identity.PublicIdentity.Address;
    }
}

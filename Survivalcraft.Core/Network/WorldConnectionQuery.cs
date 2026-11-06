using EntitySystem.Core;

using Game.Protocol.Identity;

namespace Game.Network;

/// <summary>Joins runtime sessions with world players and roles without owning persistent state.</summary>
public static class WorldConnectionQuery
{
    public static WorldConnection? Find(Project project, PlayerAddress player)
    {
        return ActiveConnections(project)?.Members.Values.FirstOrDefault(session => session.PlayerAddress == player);
    }

    public static PlayerAddress? GetController(Project project, Guid characterId)
    {
        return characterId == Guid.Empty
            ? null
            : ActiveConnections(project)?.Members.Values
                .FirstOrDefault(session => session.CharacterId == characterId && session.PlayerAddress is not null)
                ?.PlayerAddress;
    }

    public static IReadOnlySet<Guid> GetControlledCharacterIds(Project project, IReadOnlySet<PlayerAddress> players)
    {
        return ActiveConnections(project)?.Members.Values.Where(session =>
                       session.PlayerAddress is { } player &&
                       players.Contains(player) &&
                       session.CharacterId != Guid.Empty
                   )
                   .Select(session => session.CharacterId).ToHashSet() ??
               new HashSet<Guid>();
    }

    public static bool IsLocalCharacter(Project project, Guid characterId)
    {
        return characterId != Guid.Empty && ActiveConnections(project)?.Self?.CharacterId == characterId;
    }

    public static bool IsPlayerOnline(Project project, WorldConnection connection)
    {
        return connection.State != ClientState.NotConnected &&
               connection.IsConnected &&
               connection.PlayerAddress is not null &&
               ActiveConnections(project)?.Members.TryGetValue(connection.ID, out var registered) == true &&
               ReferenceEquals(registered, connection);
    }

    private static WorldConnections? ActiveConnections(Project project)
    {
        if (project.IsDisposed) { return null; }

        var runtime = WorldRuntimeContext.For(project);
        return runtime.IsClosed ? null : WorldConnections.For(runtime);
    }
}

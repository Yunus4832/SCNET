using EntitySystem.Core;

using Game.Protocol.Identity;

namespace Game.Network;

/// <summary>Joins live transport sessions with world players and roles without owning persistent state.</summary>
public static class PlayerSessionQuery
{
    public static Client? Find(PlayerAddress player)
    {
        return CommonLib.Net.Clients.Values.FirstOrDefault(session => session.PlayerAddress == player);
    }

    public static PlayerAddress? GetController(Guid characterId)
    {
        return characterId == Guid.Empty
            ? null
            : CommonLib.Net.Clients.Values
                .FirstOrDefault(session => session.CharacterId == characterId && session.PlayerAddress is not null)
                ?.PlayerAddress;
    }

    public static IReadOnlySet<Guid> GetControlledCharacterIds(IReadOnlySet<PlayerAddress> players)
    {
        return CommonLib.Net.Clients.Values.Where(session =>
                session.PlayerAddress is { } player && players.Contains(player) && session.CharacterId != Guid.Empty
            )
            .Select(session => session.CharacterId).ToHashSet();
    }

    public static string GetDisplayName(Project project, PlayerAddress address)
    {
        var player = project.FindSubsystem<SubsystemPlayers>(true)!.Store.Find(address);
        return player is { DisplayName.Length: > 0 }
            ? player.DisplayName
            : Find(address)?.CharacterData?.Name ?? address.Value;
    }

    public static bool IsLocalCharacter(Guid characterId)
    {
        return characterId != Guid.Empty && CommonLib.Net.Self?.CharacterId == characterId;
    }
}

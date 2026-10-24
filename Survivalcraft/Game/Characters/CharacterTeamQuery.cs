using EntitySystem.Core;

using Game.Network;
using Game.Protocol.Identity;

namespace Game;

/// <summary>A live presentation query; teams never own or cache characters.</summary>
public static class CharacterTeamQuery
{
    public static Guid GetTeamId(Project project, Guid characterId)
    {
        var controller = PlayerSessionQuery.GetController(characterId);
        return controller is { } player
            ? project.FindSubsystem<SubsystemTeams>(true)!.Store.FindForPlayer(player)?.Id ?? Guid.Empty
            : Guid.Empty;
    }

    public static IEnumerable<CharacterListEntry> GetControlledTeamCharacters(Project project, PlayerAddress player)
    {
        var team = project.FindSubsystem<SubsystemTeams>(true)!.Store.FindForPlayer(player);
        if (team is null) { return []; }

        var controlled = PlayerSessionQuery.GetControlledCharacterIds(team.Members);
        return project.FindSubsystem<SubsystemCharacters>(true)!.CharacterList.Values.Where(character =>
            controlled.Contains(character.CharacterId)
        );
    }
}

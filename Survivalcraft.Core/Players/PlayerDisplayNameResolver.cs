using EntitySystem.Core;

using Game.Protocol.Identity;

namespace Game.Players;

public static class PlayerDisplayNameResolver
{
    public static string GetDisplayName(Project project, PlayerAddress address)
    {
        var player = project.FindSubsystem<SubsystemPlayers>(true)!.Store.Find(address);
        return player is { DisplayName.Length: > 0 }
            ? player.DisplayName
            : (WorldConnectionQuery.Find(project, address) is { } connection
                  ? project.FindSubsystem<SubsystemCharacters>(true)!
                      .FindCharacterData(character => character.CharacterId == connection.CharacterId)?.Name
                  : null) ??
              address.Value;
    }
}

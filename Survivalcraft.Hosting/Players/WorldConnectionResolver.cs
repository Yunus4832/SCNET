namespace Game.Players;

/// <summary>Application joins from a logical connection to world player and character data.</summary>
public static class WorldConnectionResolver
{
    public static WorldPlayer? FindPlayer(WorldConnection connection)
    {
        return connection.World is { IsClosed: false, Project: { } project } &&
               connection.PlayerAddress is { } address
            ? project.FindSubsystem<SubsystemPlayers>(false)?.Store.Find(address)
            : null;
    }

    public static CharacterData? FindCharacter(WorldConnection connection)
    {
        return connection.CharacterId != Guid.Empty && connection.World is { IsClosed: false, Project: { } project }
            ? project.FindSubsystem<SubsystemCharacters>(false)
                ?.FindCharacterData(character => character.CharacterId == connection.CharacterId)
            : null;
    }
}

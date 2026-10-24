namespace Game.Protocol.Characters;

public enum CharacterCreationError : byte
{
    Unavailable = 0,

    ExistingCharacter = 1,

    InvalidProfile = 2,

    NameUnavailable = 3
}

namespace Game.Commands;

public sealed record UpdateOwnPlayerProfileCommand(
    string Name,
    string SkinName,
    CharacterSex CharacterSex
) : IGameCommand;

public sealed record UpdatePlayerProfileCommand(
    Guid PlayerId,
    string Name,
    string SkinName,
    CharacterSex CharacterSex
) : IGameCommand;

public sealed record SendChatMessageCommand(
    GameMessageChannel Channel,
    string Content
) : IGameCommand;

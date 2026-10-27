using MessagePack;

namespace Game.Commands;

[MessagePackObject]
public sealed record UpdateOwnPlayerProfileCommand(
    [property: Key(0)] string Name,
    [property: Key(1)] string SkinName,
    [property: Key(2)] CharacterSex CharacterSex
) : IGameCommand;

[MessagePackObject]
public sealed record UpdatePlayerProfileCommand(
    [property: Key(0)] Guid PlayerId,
    [property: Key(1)] string Name,
    [property: Key(2)] string SkinName,
    [property: Key(3)] CharacterSex CharacterSex
) : IGameCommand;

[MessagePackObject]
public sealed record SendChatMessageCommand(
    [property: Key(0)] GameMessageChannel Channel,
    [property: Key(1)] string Content
) : IGameCommand;

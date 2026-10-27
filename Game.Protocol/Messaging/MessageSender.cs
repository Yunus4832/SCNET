using Game.Protocol.Identity;

using MessagePack;

namespace Game.Protocol.Messaging;

[MessagePackObject]
/// <summary>Server-authored subject identity and immutable character/name context at send time.</summary>
public sealed record MessageSender(
    [property: Key(0)] PlayerAddress SubjectAddress,
    [property: Key(1)] string SubjectName,
    [property: Key(2)] Guid? CharacterId = null,
    [property: Key(3)] string CharacterName = ""
);

using Game.Protocol.Identity;

namespace Game.Protocol.Messaging;

/// <summary>Server-authored subject identity and immutable character/name context at send time.</summary>
public sealed record MessageSender(
    PlayerAddress SubjectAddress,
    string SubjectName,
    Guid? CharacterId = null,
    string CharacterName = ""
);

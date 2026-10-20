namespace Game.Protocol.Players;

public sealed record PlayerListEntry(
    Guid PlayerGuid,
    string Name,
    bool IsOnline
);

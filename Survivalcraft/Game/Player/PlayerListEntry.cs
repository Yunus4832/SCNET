namespace Game;

public sealed record PlayerListEntry(
    Guid PlayerGuid,
    string Name,
    bool IsOnline
);

namespace Rendezvous.Protocol;

public sealed record RoomPage(IReadOnlyList<RoomSummary> Rooms, string? NextCursor);

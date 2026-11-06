using Game.Protocol.Identity;

namespace Game.Teams;

public sealed record TeamOperation(
    Guid Id,
    TeamOperationKind Kind,
    PlayerAddress Initiator,
    PlayerAddress Responder,
    PlayerAddress JoiningPlayer,
    Guid TeamId,
    double ExpiresAt
);

using Game.Protocol.Identity;

namespace Game.Commands;

public sealed record CreateTeamCommand(string Name) : IGameCommand;

public sealed record RequestJoinTeamCommand(Guid TeamId) : IGameCommand;

public sealed record InvitePlayerToTeamCommand(PlayerAddress Player) : IGameCommand;

public sealed record RespondTeamRequestCommand(
    Guid OperationId,
    bool Accepted
) : IGameCommand;

public sealed record LeaveTeamCommand : IGameCommand;

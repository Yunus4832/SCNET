using Game.Protocol.Identity;

using MessagePack;

namespace Game.Commands;

[MessagePackObject]
public sealed record CreateTeamCommand([property: Key(0)] string Name) : IGameCommand;

[MessagePackObject]
public sealed record RequestJoinTeamCommand([property: Key(0)] Guid TeamId) : IGameCommand;

[MessagePackObject]
public sealed record InvitePlayerToTeamCommand([property: Key(0)] PlayerAddress Player) : IGameCommand;

[MessagePackObject]
public sealed record RespondTeamRequestCommand([property: Key(0)] Guid OperationId, [property: Key(1)] bool Accepted)
    : IGameCommand;

[MessagePackObject]
public sealed record LeaveTeamCommand : IGameCommand;

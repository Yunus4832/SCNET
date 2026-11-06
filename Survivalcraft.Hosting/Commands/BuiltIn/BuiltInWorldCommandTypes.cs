using MessagePack;

namespace Game.Commands.BuiltIn;

[MessagePackObject]
public sealed record GetCommandCatalogCommand : IGameCommand;

[MessagePackObject]
public sealed record ShowCommandHelpCommand([property: Key(0)] string? CommandName = null) : IGameCommand;

[MessagePackObject]
public sealed record GetWorldTimeCommand : IGameCommand;

[MessagePackObject]
public sealed record SetWorldTimeCommand([property: Key(0)] string Preset) : IGameCommand;

[MessagePackObject]
public sealed record AdvanceWorldTimeCommand : IGameCommand;

[MessagePackObject]
public sealed record SetPrecipitationCommand([property: Key(0)] bool Enabled) : IGameCommand;

[MessagePackObject]
public sealed record SetFogCommand([property: Key(0)] bool Enabled) : IGameCommand;

[MessagePackObject]
public sealed record TriggerPlayerLightningCommand : IGameCommand;

[MessagePackObject]
public sealed record TriggerLightningCommand([property: Key(0)] Vector3 Position, [property: Key(1)] Vector3 Direction)
    : IGameCommand;

[MessagePackObject]
public sealed record SetSeasonCommand([property: Key(0)] Season Season, [property: Key(1)] float Progress)
    : IGameCommand;

[MessagePackObject]
public sealed record ListPlayersCommand : IGameCommand;

[MessagePackObject]
public sealed record StopServerCommand : IGameCommand;

[MessagePackObject]
public sealed record ShowServerAuthHelpCommand : IGameCommand;

[MessagePackObject]
public sealed record ClaimServerAdministrationCommand([property: Key(0)] string Code) : IGameCommand;

[MessagePackObject]
public sealed record GetServerAuthStatusCommand : IGameCommand;

[MessagePackObject]
public sealed record GetServerAuthCodeCommand : IGameCommand;

[MessagePackObject]
public sealed record RegenerateServerAuthCodeCommand : IGameCommand;

[MessagePackObject]
public sealed record ShowPermissionHelpCommand : IGameCommand;

[MessagePackObject]
public sealed record ListPermissionPlayersCommand : IGameCommand;

[MessagePackObject]
public sealed record ListPermissionNodesCommand : IGameCommand;

[MessagePackObject]
public sealed record ListOwnPermissionsCommand : IGameCommand;

[MessagePackObject]
public sealed record ListPlayerPermissionsCommand([property: Key(0)] string Player) : IGameCommand;

[MessagePackObject]
public sealed record GrantPlayerPermissionCommand(
    [property: Key(0)] string Player,
    [property: Key(1)] string Permission,
    [property: Key(2)] bool CanDelegate
) : IGameCommand;

[MessagePackObject]
public sealed record RevokePlayerPermissionCommand(
    [property: Key(0)] string Player,
    [property: Key(1)] string Permission
)
    : IGameCommand;

[MessagePackObject]
public sealed record BanPlayerCommand([property: Key(0)] string Player, [property: Key(1)] string Reason)
    : IGameCommand;

[MessagePackObject]
public sealed record UnbanPlayerCommand([property: Key(0)] string Player) : IGameCommand;

[MessagePackObject]
public sealed record ListPlayerBansCommand : IGameCommand;

[MessagePackObject]
public sealed record SetPlayerMuteCommand([property: Key(0)] string Player, [property: Key(1)] bool IsMuted)
    : IGameCommand;

[MessagePackObject]
public sealed record ListMutedPlayersCommand : IGameCommand;

namespace Game.Commands;

public sealed record ShowCommandHelpCommand(string? CommandName = null) : IGameCommand;

public sealed record GetWorldTimeCommand : IGameCommand;

public sealed record SetWorldTimeCommand(string Preset) : IGameCommand;

public sealed record AdvanceWorldTimeCommand : IGameCommand;

public sealed record SetPrecipitationCommand(bool Enabled) : IGameCommand;

public sealed record SetFogCommand(bool Enabled) : IGameCommand;

public sealed record TriggerPlayerLightningCommand : IGameCommand;

public sealed record TriggerLightningCommand(
    Vector3 Position,
    Vector3 Direction) : IGameCommand;

public sealed record SetSeasonCommand(
    Season Season,
    float Progress) : IGameCommand;

public sealed record ListPlayersCommand : IGameCommand;

public sealed record GetRunModeCommand : IGameCommand;

public sealed record SetRunModeCommand(
    RunModeType TargetMode,
    SessionInfo? RestartSession = null) : IGameCommand;

public sealed record RestartApplicationCommand(
    SessionInfo? RestartSession = null) : IGameCommand;

public sealed record SwitchInstanceCommand(string InstanceId) : IGameCommand;

public sealed record CreateInstanceCommand(string InstanceId) : IGameCommand;

public sealed record DeleteInstanceCommand(string InstanceId) : IGameCommand;

public sealed record ExitApplicationCommand : IGameCommand;

public sealed record GetLanguageCommand : IGameCommand;

public sealed record SetLanguageCommand(string LanguageType) : IGameCommand;

public sealed record StopServerCommand : IGameCommand;

public sealed record ShowServerAuthHelpCommand : IGameCommand;

public sealed record ClaimServerAdministrationCommand(string Code) : IGameCommand;

public sealed record GetServerAuthStatusCommand : IGameCommand;

public sealed record GetServerAuthCodeCommand : IGameCommand;

public sealed record RegenerateServerAuthCodeCommand : IGameCommand;

public sealed record ShowPermissionHelpCommand : IGameCommand;

public sealed record ListPermissionPlayersCommand : IGameCommand;

public sealed record ListPermissionNodesCommand : IGameCommand;

public sealed record ListOwnPermissionsCommand : IGameCommand;

public sealed record ListPlayerPermissionsCommand(string Player) : IGameCommand;

public sealed record GrantPlayerPermissionCommand(
    string Player,
    string Permission,
    bool CanDelegate) : IGameCommand;

public sealed record RevokePlayerPermissionCommand(
    string Player,
    string Permission) : IGameCommand;

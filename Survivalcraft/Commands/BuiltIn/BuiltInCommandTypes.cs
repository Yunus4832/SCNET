using Engine.Input;

using MessagePack;

namespace Game.Commands;

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

public sealed record GetRunModeCommand : IGameCommand;

public sealed record GetNetworkSendStatisticsCommand : IGameCommand;

public sealed record SetRunModeCommand(
    RunModeType TargetMode,
    SessionInfo? RestartSession = null
) : IGameCommand;

public sealed record RestartApplicationCommand(SessionInfo? RestartSession = null) : IGameCommand;

public sealed record SwitchInstanceCommand(string InstanceId) : IGameCommand;

public sealed record CreateInstanceCommand(string InstanceId) : IGameCommand;

public sealed record DeleteInstanceCommand(string InstanceId) : IGameCommand;

public sealed record CloneInstanceCommand(
    string SourceInstanceId,
    string TargetInstanceId
) : IGameCommand;

public sealed record ExitApplicationCommand : IGameCommand;

public sealed record GetLanguageCommand : IGameCommand;

public sealed record SetLanguageCommand(string LanguageType) : IGameCommand;

[MessagePackObject]
public sealed record StopServerCommand : IGameCommand;

public sealed record GetAutomationUiContextCommand : IGameCommand;

public sealed record TapAutomationUiCommand(string Selector) : IGameCommand;

public sealed record DragAutomationUiCommand(string SourceSelector, string TargetSelector, double DurationSeconds)
    : IGameCommand;

public sealed record ScrollAutomationUiCommand(string Selector, float Delta) : IGameCommand;

public sealed record SwipeAutomationUiCommand(
    string Selector,
    float DeltaX,
    float DeltaY,
    int DurationFrames
) : IGameCommand;

public sealed record MoveAutomationMouseCommand(int DeltaX, int DeltaY) : IGameCommand;

public sealed record PressAutomationKeyCommand(Key Key) : IGameCommand;

public sealed record CaptureAutomationScreenshotCommand : IGameCommand;

public sealed record RunAutomationInputCommand(Automation.AutomationInputAction Action) : IGameCommand;

public sealed record GetAutomationActionStatusCommand : IGameCommand;

public sealed record GetAutomationGameplayContextCommand : IGameCommand;

public sealed record NavigateAutomationPlayerCommand(Vector3 Destination, float Range, double TimeoutSeconds)
    : IGameCommand;

public sealed record GetAutomationNavigationStatusCommand : IGameCommand;

public sealed record CancelAutomationNavigationCommand : IGameCommand;

public sealed record LookAtAutomationTargetCommand(Vector3 Target, float ToleranceDegrees, double TimeoutSeconds)
    : IGameCommand;

public sealed record GetAutomationViewStatusCommand : IGameCommand;

public sealed record SetAutomationViewAnglesCommand(
    float YawDegrees,
    float PitchDegrees,
    bool Relative,
    float ToleranceDegrees,
    double TimeoutSeconds
) : IGameCommand;

public sealed record CancelAutomationViewCommand : IGameCommand;

public sealed record CancelAutomationActionCommand : IGameCommand;

public sealed record EnterAutomationTextCommand(string Text) : IGameCommand;

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
public sealed record ListPermissionUsersCommand : IGameCommand;

[MessagePackObject]
public sealed record ListPermissionNodesCommand : IGameCommand;

[MessagePackObject]
public sealed record ListOwnPermissionsCommand : IGameCommand;

[MessagePackObject]
public sealed record ListUserPermissionsCommand([property: Key(0)] string User) : IGameCommand;

[MessagePackObject]
public sealed record GrantUserPermissionCommand(
    [property: Key(0)] string User,
    [property: Key(1)] string Permission,
    [property: Key(2)] bool CanDelegate
) : IGameCommand;

[MessagePackObject]
public sealed record RevokeUserPermissionCommand([property: Key(0)] string User, [property: Key(1)] string Permission)
    : IGameCommand;

[MessagePackObject]
public sealed record BanUserCommand([property: Key(0)] string User, [property: Key(1)] string Reason) : IGameCommand;

[MessagePackObject]
public sealed record UnbanUserCommand([property: Key(0)] string User) : IGameCommand;

[MessagePackObject]
public sealed record ListPlayerBansCommand : IGameCommand;

[MessagePackObject]
public sealed record SetUserMuteCommand([property: Key(0)] string User, [property: Key(1)] bool IsMuted) : IGameCommand;

[MessagePackObject]
public sealed record ListMutedUsersCommand : IGameCommand;

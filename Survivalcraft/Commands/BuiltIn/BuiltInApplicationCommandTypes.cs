using Engine.Input;

namespace Game.Commands.BuiltIn;

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

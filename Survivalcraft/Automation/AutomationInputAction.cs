using Engine.Input;

namespace Game.Automation;

/// <summary>A bounded simultaneous input gesture, delivered through normal engine input.</summary>
public sealed record AutomationInputAction(
    Key[] Keys,
    MouseButton[] MouseButtons,
    int DurationFrames,
    int MouseDeltaX = 0,
    int MouseDeltaY = 0,
    double? DurationSeconds = null);

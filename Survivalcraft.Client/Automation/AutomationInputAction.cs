using Engine.Input;

namespace Game.Automation;

/// <summary>通过正常引擎输入流程投递的一组有界并发手势输入。</summary>
public sealed record AutomationInputAction(
    Key[] Keys,
    MouseButton[] MouseButtons,
    int DurationFrames,
    int MouseDeltaX = 0,
    int MouseDeltaY = 0,
    double? DurationSeconds = null
);

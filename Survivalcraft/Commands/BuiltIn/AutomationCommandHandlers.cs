using System.Text.Json;

using Game.Automation;

namespace Game.Commands;

internal static class AutomationCommandHandlers
{
    public static CommandResult GetContext(CommandContext _, GetAutomationUiContextCommand __) =>
        new(true, "automation.ui.context", "UI context captured.",
            Data: JsonSerializer.SerializeToNode(AutomationUiContext.Capture()));

    public static CommandResult Tap(CommandContext _, TapAutomationUiCommand command)
    {
        if (!AutomationUiContext.TryFindTarget(command.Selector, out var target))
        {
            return CommandResult.Fail("automation.ui.target_not_found", "UI target was not found.");
        }

        AutomationInputController.Tap(new Vector2(target.X + target.Width / 2f, target.Y + target.Height / 2f));
        return CommandResult.Ok("UI tap queued.", "automation.ui.tap_queued");
    }

    public static CommandResult PressKey(CommandContext _, PressAutomationKeyCommand command)
    {
        AutomationInputController.PressKey(command.Key);
        return CommandResult.Ok("UI key press queued.", "automation.ui.key_queued");
    }

    public static CommandResult Scroll(CommandContext _, ScrollAutomationUiCommand command)
    {
        if (!TryGetScrollableTarget(command.Selector, out var target, out var failure))
        {
            return failure;
        }

        if (!float.IsFinite(command.Delta) || command.Delta == 0f)
        {
            return CommandResult.Fail("automation.ui.invalid_scroll", "Scroll delta must be finite and non-zero.");
        }

        AutomationInputController.Scroll(Center(target), command.Delta);
        return CommandResult.Ok("UI mouse-wheel scroll queued.", "automation.ui.scroll_queued");
    }

    public static CommandResult Swipe(CommandContext _, SwipeAutomationUiCommand command)
    {
        if (!TryGetScrollableTarget(command.Selector, out var target, out var failure))
        {
            return failure;
        }

        if (!float.IsFinite(command.DeltaX) || !float.IsFinite(command.DeltaY) ||
            command.DeltaX == 0f && command.DeltaY == 0f ||
            command.DurationFrames is < 1 or > 120)
        {
            return CommandResult.Fail("automation.ui.invalid_swipe",
                "Swipe delta must be finite and non-zero, and durationFrames must be between 1 and 120.");
        }

        var start = Center(target);
        var end = start + new Vector2(command.DeltaX, command.DeltaY);
        AutomationInputController.Swipe(start, end, command.DurationFrames);
        return CommandResult.Ok("UI touch swipe queued.", "automation.ui.swipe_queued");
    }

    public static CommandResult MoveMouse(CommandContext _, MoveAutomationMouseCommand command)
    {
        if (command.DeltaX == 0 && command.DeltaY == 0)
        {
            return CommandResult.Fail("automation.input.invalid_mouse_movement",
                "Mouse movement must be non-zero.");
        }

        AutomationInputController.MoveMouse(new Point2(command.DeltaX, command.DeltaY));
        return CommandResult.Ok("Relative mouse movement queued.", "automation.input.mouse_movement_queued");
    }

    public static CommandResult Screenshot(CommandContext _, CaptureAutomationScreenshotCommand __)
    {
        var result = AutomationScreenshot.Capture();
        return new CommandResult(true, "automation.ui.screenshot", "UI screenshot captured.",
            Data: JsonSerializer.SerializeToNode(result));
    }

    private static Vector2 Center(AutomationTarget target) =>
        new(target.X + target.Width / 2f, target.Y + target.Height / 2f);

    private static bool TryGetScrollableTarget(
        string selector,
        out AutomationTarget target,
        out CommandResult failure)
    {
        if (!AutomationUiContext.TryFindTarget(selector, out target))
        {
            failure = CommandResult.Fail("automation.ui.target_not_found", "UI target was not found.");
            return false;
        }

        if (!target.Actions.Contains("scroll", StringComparer.Ordinal))
        {
            failure = CommandResult.Fail("automation.ui.target_not_scrollable", "UI target is not scrollable.");
            return false;
        }

        failure = null!;
        return true;
    }
}

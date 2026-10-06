using System.Text.Json;

using Game.Automation;

namespace Game.Commands;

internal static class AutomationCommandHandlers
{
    private static ComponentPlayer? FindLocalPlayer() => GameManager.Project?
        .FindSubsystem<SubsystemPlayers>()?.ComponentPlayers.FirstOrDefault(player => player.IsLocallyControlled);

    public static CommandResult LookAt(CommandContext _, LookAtAutomationTargetCommand command)
    {
        try
        {
            var player = FindLocalPlayer() ?? throw new InvalidOperationException("No local character is available.");
            var id = player.ComponentInput.ViewControl.Start(player, command.Target, command.ToleranceDegrees, command.TimeoutSeconds);
            return new CommandResult(true, "automation.view.started", "View control started.",
                Data: JsonSerializer.SerializeToNode(new { Id = id }));
        }
        catch (ArgumentException exception)
        {
            return CommandResult.Fail("automation.view.invalid_target", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return CommandResult.Fail("automation.view.unavailable", exception.Message);
        }
    }

    public static CommandResult ViewStatus(CommandContext _, GetAutomationViewStatusCommand __) =>
        new(true, "automation.view.status", "View control status captured.",
            Data: JsonSerializer.SerializeToNode(FindLocalPlayer()?.ComponentInput.ViewControl.Capture() ?? new { Status = "unavailable" }));

    public static CommandResult SetViewAngles(CommandContext _, SetAutomationViewAnglesCommand command)
    {
        try
        {
            var player = FindLocalPlayer() ?? throw new InvalidOperationException("No local character is available.");
            var id = player.ComponentInput.ViewControl.StartAngles(player, command.YawDegrees, command.PitchDegrees,
                command.Relative, command.ToleranceDegrees, command.TimeoutSeconds);
            return new CommandResult(true, "automation.view.started", "View control started.",
                Data: JsonSerializer.SerializeToNode(new { Id = id }));
        }
        catch (ArgumentException exception)
        {
            return CommandResult.Fail("automation.view.invalid_target", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return CommandResult.Fail("automation.view.unavailable", exception.Message);
        }
    }

    public static CommandResult CancelView(CommandContext _, CancelAutomationViewCommand __)
    {
        FindLocalPlayer()?.ComponentInput.ViewControl.Cancel();
        return CommandResult.Ok("View control cancelled.", "automation.view.cancelled");
    }

    public static CommandResult Navigate(CommandContext _, NavigateAutomationPlayerCommand command)
    {
        try
        {
            var id = GetNavigation().Start(command.Destination, command.Range, command.TimeoutSeconds);
            return new CommandResult(true, "automation.navigation.started", "Navigation started.",
                Data: JsonSerializer.SerializeToNode(new { Id = id }));
        }
        catch (ArgumentException exception)
        {
            return CommandResult.Fail("automation.navigation.invalid_destination", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return CommandResult.Fail("automation.navigation.unavailable", exception.Message);
        }
    }

    public static CommandResult NavigationStatus(CommandContext _, GetAutomationNavigationStatusCommand __)
    {
        var navigation = FindNavigation();
        return new CommandResult(true, "automation.navigation.status", "Navigation status captured.",
            Data: JsonSerializer.SerializeToNode(navigation?.Capture() ?? new { Status = "unavailable" }));
    }

    public static CommandResult CancelNavigation(CommandContext _, CancelAutomationNavigationCommand __)
    {
        FindNavigation()?.Cancel();
        return CommandResult.Ok("Navigation cancelled.", "automation.navigation.cancelled");
    }

    private static AutomationNavigationController? FindNavigation() => GameManager.Project?
        .FindSubsystem<SubsystemPlayers>()?.ComponentPlayers.FirstOrDefault(player => player.IsLocallyControlled)
        ?.ComponentInput.Navigation;

    private static AutomationNavigationController GetNavigation() => FindNavigation() ??
        throw new InvalidOperationException("No local character is available.");

    public static CommandResult GameplayContext(CommandContext _, GetAutomationGameplayContextCommand __) =>
        new(true, "automation.gameplay.context", "Gameplay context captured.",
            Data: JsonSerializer.SerializeToNode(AutomationGameplayContext.Capture()));

    public static CommandResult RunInput(CommandContext _, RunAutomationInputCommand command)
    {
        var action = command.Action;
        if (action.DurationFrames is < 1 or > 600 || action.Keys == null || action.MouseButtons == null ||
            action.Keys.Any(key => !Enum.IsDefined(key)) ||
            action.MouseButtons.Any(button => !Enum.IsDefined(button)))
        {
            return CommandResult.Fail("automation.input.invalid_action", "Valid keys/buttons and 1–600 durationFrames are required.");
        }

        try
        {
            var id = AutomationInputController.StartAction(action);
            return new CommandResult(true, "automation.input.started", "Input gesture started.",
                Data: JsonSerializer.SerializeToNode(new { Id = id }));
        }
        catch (InvalidOperationException exception)
        {
            return CommandResult.Fail("automation.input.busy", exception.Message);
        }
        catch (ArgumentOutOfRangeException)
        {
            return CommandResult.Fail("automation.input.invalid_action", "The input gesture contains invalid or excessive values.");
        }
    }

    public static CommandResult ActionStatus(CommandContext _, GetAutomationActionStatusCommand __) =>
        new(true, "automation.input.status", "Input gesture status captured.",
            Data: JsonSerializer.SerializeToNode(AutomationInputController.GetActionStatus()));

    public static CommandResult CancelAction(CommandContext _, CancelAutomationActionCommand __)
    {
        AutomationInputController.CancelAction();
        return CommandResult.Ok("Input gesture cancelled.", "automation.input.cancelled");
    }

    public static CommandResult EnterText(CommandContext _, EnterAutomationTextCommand command)
    {
        if (command.Text.Length > 4096)
        {
            return CommandResult.Fail("automation.input.text_too_long", "Text must not exceed 4096 characters.");
        }

        Engine.Input.InputSimulation.EnqueueText(command.Text);
        return CommandResult.Ok("Text input queued.", "automation.input.text_queued");
    }

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

    public static CommandResult Drag(CommandContext _, DragAutomationUiCommand command)
    {
        if (!AutomationUiContext.TryFindTarget(command.SourceSelector, out var source) ||
            !AutomationUiContext.TryFindTarget(command.TargetSelector, out var target))
        {
            return CommandResult.Fail("automation.ui.target_not_found", "The drag source or destination is no longer available.");
        }

        if (!source.Actions.Contains("drag") || !double.IsFinite(command.DurationSeconds) ||
            command.DurationSeconds is < 0.1 or > 3)
        {
            return CommandResult.Fail("automation.ui.invalid_drag", "The source must support drag and durationSeconds must be 0.1–3.");
        }

        try
        {
            var id = AutomationInputController.Drag(Center(source), Center(target), command.DurationSeconds);
            return new CommandResult(true, "automation.ui.drag_queued", "UI drag queued.",
                Data: JsonSerializer.SerializeToNode(new { Id = id }));
        }
        catch (InvalidOperationException exception)
        {
            return CommandResult.Fail("automation.input.busy", exception.Message);
        }

    }

    public static CommandResult PressKey(CommandContext _, PressAutomationKeyCommand command)
    {
        return RunInput(_, new RunAutomationInputCommand(new AutomationInputAction([command.Key], [], 1)));
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

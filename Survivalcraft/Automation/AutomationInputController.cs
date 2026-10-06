using Engine.Input;

namespace Game.Automation;

/// <summary>Schedules synthetic UI input across engine frames.</summary>
public static class AutomationInputController
{
    private sealed record PendingTap(int TouchId, Vector2 Position, int ReleaseFrame);

    private sealed class PendingSwipe(int touchId, Vector2 start, Vector2 end, int durationFrames)
    {
        public int TouchId { get; } = touchId;
        public Vector2 Start { get; } = start;
        public Vector2 End { get; } = end;
        public int DurationFrames { get; } = durationFrames;
        public int Step { get; set; } = 1;
        public int NextFrame { get; set; } = Time.FrameIndex + 1;
    }

    private static readonly Queue<PendingTap> _pendingTaps = [];

    private static readonly List<PendingSwipe> _pendingSwipes = [];

    private static readonly AutomationActionScheduler _actionScheduler = new();
    private static AutomationInputAction? _heldAction;
    private static string _actionScreen = string.Empty;
    private static double _actionDeadline;
    private static ComponentPlayer? _actionPlayer;
    private static PendingMouseDrag? _mouseDrag;

    private sealed class PendingMouseDrag(Vector2 start, Vector2 end, double durationSeconds)
    {
        public Vector2 Start { get; } = start;
        public Vector2 End { get; } = end;
        public double DurationSeconds { get; } = durationSeconds;
        public double StartTime { get; } = Time.RealTime;
        public string Screen { get; } = ScreensManager.GetCurrentScreenName();
    }

    private static Point2 MouseCoordinates(Vector2 position) => new(
        (int)MathF.Round(position.X / Window.Scale), (int)MathF.Round(position.Y / Window.Scale));

    private static Point2 CurrentMouseCoordinates() => Mouse.MousePosition is { } point
        ? MouseCoordinates(new Vector2(point.X, point.Y))
        : Point2.Zero;

    public static long Drag(Vector2 start, Vector2 end, double durationSeconds)
    {
        if (_mouseDrag != null || _heldAction != null)
        {
            throw new InvalidOperationException("Another mouse gesture is active.");
        }

        var id = StartAction(new AutomationInputAction([], [], 1, DurationSeconds: durationSeconds));
        _mouseDrag = new PendingMouseDrag(start, end, durationSeconds);
        var position = MouseCoordinates(start);
        InputSimulation.EnqueueMouseMove(position);
        InputSimulation.EnqueueMouseDown(MouseButton.Left, position);
        return id;
    }

    public static object GetActionStatus() => new
    {
        _actionScheduler.Id,
        _actionScheduler.Status,
        _actionScheduler.ElapsedFrames,
        _actionScheduler.ElapsedSeconds,
        _actionScheduler.CancellationReason,
        UiDragActive = _mouseDrag != null
    };

    public static long StartAction(AutomationInputAction action)
    {
        if (!Window.IsActive || ScreensManager.IsAnimating || _mouseDrag != null)
        {
            throw new InvalidOperationException("The window must be active and the screen stable.");
        }

        var id = _actionScheduler.Start(action);
        GetLocalPlayer()?.ComponentInput.Navigation.Cancel("input_action_started");
        GetLocalPlayer()?.ComponentInput.ViewControl.Cancel("input_action_started");
        _heldAction = _actionScheduler.Action!;
        _actionScreen = ScreensManager.GetCurrentScreenName();
        _actionPlayer = GetLocalPlayer();
        _actionDeadline = Time.RealTime + 30;
        foreach (var key in _heldAction.Keys)
        {
            InputSimulation.EnqueueKeyDown(key);
        }

        foreach (var button in _heldAction.MouseButtons)
        {
            InputSimulation.EnqueueMouseDown(button, CurrentMouseCoordinates());
        }

        InputSimulation.EnqueueMouseMovement(new Point2(action.MouseDeltaX, action.MouseDeltaY));
        return id;
    }

    public static void CancelAction(string reason = "requested")
    {
        _actionScheduler.Cancel(reason);
        ReleaseAction();
    }

    private static void ReleaseAction()
    {
        if (_mouseDrag != null)
        {
            InputSimulation.EnqueueMouseUp(MouseButton.Left, CurrentMouseCoordinates());
            _mouseDrag = null;
        }

        if (_heldAction == null)
        {
            return;
        }

        foreach (var key in _heldAction.Keys)
        {
            InputSimulation.EnqueueKeyUp(key);
        }

        foreach (var button in _heldAction.MouseButtons)
        {
            InputSimulation.EnqueueMouseUp(button, CurrentMouseCoordinates());
        }

        _heldAction = null;
        _actionPlayer = null;
    }

    private static ComponentPlayer? GetLocalPlayer() => GameManager.Project?
        .FindSubsystem<SubsystemPlayers>()?.ComponentPlayers.FirstOrDefault(player => player.IsLocallyControlled);

    public static void Tap(Vector2 position)
    {
        var touchId = InputSimulation.AllocateTouchId();
        InputSimulation.EnqueueTouchPressed(touchId, position);
        _pendingTaps.Enqueue(new PendingTap(touchId, position, Time.FrameIndex + 1));
    }

    public static void PressKey(Key key)
    {
        StartAction(new AutomationInputAction([key], [], 1));
    }

    public static void Scroll(Vector2 position, float delta)
    {
        InputSimulation.EnqueueMouseMove(MouseCoordinates(position));
        InputSimulation.EnqueueMouseWheel(delta * 120f);
    }

    public static void MoveMouse(Point2 delta) => InputSimulation.EnqueueMouseMovement(delta);

    public static void Swipe(Vector2 start, Vector2 end, int durationFrames)
    {
        var touchId = InputSimulation.AllocateTouchId();
        InputSimulation.EnqueueTouchPressed(touchId, start);
        _pendingSwipes.Add(new PendingSwipe(touchId, start, end, durationFrames));
    }

    public static void Update()
    {
        GetLocalPlayer()?.ComponentInput.Navigation.UpdateSafety();
        GetLocalPlayer()?.ComponentInput.ViewControl.UpdateSafety();
        if (_mouseDrag is { } drag)
        {
            var elapsed = Time.RealTime - drag.StartTime;
            var position = MouseCoordinates(Vector2.Lerp(drag.Start, drag.End,
                (float)Math.Clamp(elapsed / drag.DurationSeconds, 0, 1)));
            InputSimulation.EnqueueMouseMove(position);
            if (elapsed > drag.DurationSeconds || !Window.IsActive ||
                ScreensManager.GetCurrentScreenName() != drag.Screen || ScreensManager.IsAnimating)
            {
                InputSimulation.EnqueueMouseUp(MouseButton.Left, position);
                _mouseDrag = null;
            }
        }

        if (_heldAction != null)
        {
            if (!Window.IsActive)
            {
                CancelAction("window_inactive");
            }
            else if (Time.RealTime >= _actionDeadline)
            {
                CancelAction("deadline_exceeded");
            }
            else if (ScreensManager.IsAnimating || ScreensManager.GetCurrentScreenName() != _actionScreen)
            {
                CancelAction("screen_changed");
            }
            else if (_actionPlayer != null && GetLocalPlayer() != _actionPlayer)
            {
                CancelAction("character_changed");
            }
            else if (!_actionScheduler.Advance(Time.FrameDuration))
            {
                ReleaseAction();
            }
        }

        while (_pendingTaps.TryPeek(out var tap) && Time.FrameIndex >= tap.ReleaseFrame)
        {
            _pendingTaps.Dequeue();
            InputSimulation.EnqueueTouchReleased(tap.TouchId, tap.Position);
        }

        for (var index = _pendingSwipes.Count - 1; index >= 0; index--)
        {
            var swipe = _pendingSwipes[index];
            if (Time.FrameIndex < swipe.NextFrame)
            {
                continue;
            }

            if (swipe.Step <= swipe.DurationFrames)
            {
                var position = Vector2.Lerp(swipe.Start, swipe.End,
                    swipe.Step / (float)swipe.DurationFrames);
                InputSimulation.EnqueueTouchMoved(swipe.TouchId, position);
                swipe.Step++;
                swipe.NextFrame = Time.FrameIndex + 1;
            }
            else
            {
                InputSimulation.EnqueueTouchReleased(swipe.TouchId, swipe.End);
                _pendingSwipes.RemoveAt(index);
            }
        }
    }

    public static void Clear()
    {
        GetLocalPlayer()?.ComponentInput.Navigation.Cancel("cleared");
        GetLocalPlayer()?.ComponentInput.ViewControl.Cancel("cleared");
        CancelAction("cleared");
        foreach (var tap in _pendingTaps)
        {
            InputSimulation.EnqueueTouchReleased(tap.TouchId, tap.Position);
        }

        foreach (var swipe in _pendingSwipes)
        {
            InputSimulation.EnqueueTouchReleased(swipe.TouchId, Vector2.Lerp(swipe.Start, swipe.End,
                Math.Clamp(swipe.Step / (float)swipe.DurationFrames, 0f, 1f)));
        }

        _pendingTaps.Clear();
        _pendingSwipes.Clear();
    }
}

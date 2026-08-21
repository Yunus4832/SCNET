using Engine.Input;

namespace Game.Automation;

/// <summary>Schedules synthetic UI input across engine frames.</summary>
public static class AutomationInputController
{
    private sealed record PendingTap(int TouchId, Vector2 Position, int ReleaseFrame);

    private sealed record PendingKey(Key Key, int ReleaseFrame);

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

    private static readonly Queue<PendingKey> _pendingKeys = [];

    private static readonly List<PendingSwipe> _pendingSwipes = [];

    public static void Tap(Vector2 position)
    {
        var touchId = InputSimulation.AllocateTouchId();
        InputSimulation.EnqueueTouchPressed(touchId, position);
        _pendingTaps.Enqueue(new PendingTap(touchId, position, Time.FrameIndex + 1));
    }

    public static void PressKey(Key key)
    {
        InputSimulation.EnqueueKeyDown(key);
        _pendingKeys.Enqueue(new PendingKey(key, Time.FrameIndex + 1));
    }

    public static void Scroll(Vector2 position, float delta)
    {
        InputSimulation.EnqueueMouseMove(new Point2((int)MathF.Round(position.X), (int)MathF.Round(position.Y)));
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
        while (_pendingTaps.TryPeek(out var tap) && Time.FrameIndex >= tap.ReleaseFrame)
        {
            _pendingTaps.Dequeue();
            InputSimulation.EnqueueTouchReleased(tap.TouchId, tap.Position);
        }

        while (_pendingKeys.TryPeek(out var key) && Time.FrameIndex >= key.ReleaseFrame)
        {
            _pendingKeys.Dequeue();
            InputSimulation.EnqueueKeyUp(key.Key);
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
        _pendingTaps.Clear();
        _pendingKeys.Clear();
        _pendingSwipes.Clear();
    }
}

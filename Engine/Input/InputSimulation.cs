using System.Collections.Concurrent;

using Engine.Core;

namespace Engine.Input;

/// <summary>
/// Queues synthetic input for delivery during the engine input phase.
/// Synthetic and physical button/key states are merged, so releasing a
/// simulated input never releases a still-held physical device input.
/// </summary>
public static class InputSimulation
{
    private interface IInputEvent
    {
        void Deliver();
    }

    private sealed record KeyEvent(Key Key, bool IsDown) : IInputEvent
    {
        public void Deliver()
        {
            if (IsDown)
            {
                Keyboard.ProcessSimulatedKeyDown(Key);
            }
            else
            {
                Keyboard.ProcessSimulatedKeyUp(Key);
            }
        }
    }

    private sealed record CharacterEvent(char Character) : IInputEvent
    {
        public void Deliver() => Keyboard.ProcessSimulatedCharacter(Character);
    }

    private sealed record MouseButtonInputEvent(MouseButton Button, Point2 Position, bool IsDown) : IInputEvent
    {
        public void Deliver()
        {
            if (IsDown)
            {
                Mouse.ProcessSimulatedMouseDown(Button, Position);
            }
            else
            {
                Mouse.ProcessSimulatedMouseUp(Button, Position);
            }
        }
    }

    private sealed record MouseMoveInputEvent(Point2 Position) : IInputEvent
    {
        public void Deliver() => Mouse.ProcessSimulatedMouseMove(Position);
    }

    private sealed record MouseWheelInputEvent(float Value) : IInputEvent
    {
        public void Deliver() => Mouse.ProcessMouseWheel(Value);
    }

    private sealed record TouchInputEvent(int Id, Vector2 Position, TouchLocationState State) : IInputEvent
    {
        public void Deliver()
        {
            switch (State)
            {
                case TouchLocationState.Pressed:
                    Touch.ProcessTouchPressed(Id, Position);
                    break;
                case TouchLocationState.Moved:
                    Touch.ProcessTouchMoved(Id, Position);
                    break;
                case TouchLocationState.Released:
                    Touch.ProcessTouchReleased(Id, Position);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(State));
            }
        }
    }

    private static readonly ConcurrentQueue<IInputEvent> _events = [];

    private static int _nextTouchId = -1;

    public static void EnqueueKeyDown(Key key) => _events.Enqueue(new KeyEvent(key, true));

    public static void EnqueueKeyUp(Key key) => _events.Enqueue(new KeyEvent(key, false));

    public static void EnqueueText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var character in text)
        {
            _events.Enqueue(new CharacterEvent(character));
        }
    }

    public static void EnqueueMouseMove(Point2 position) => _events.Enqueue(new MouseMoveInputEvent(position));

    public static void EnqueueMouseDown(MouseButton button, Point2 position) =>
        _events.Enqueue(new MouseButtonInputEvent(button, position, true));

    public static void EnqueueMouseUp(MouseButton button, Point2 position) =>
        _events.Enqueue(new MouseButtonInputEvent(button, position, false));

    public static void EnqueueMouseWheel(float value) => _events.Enqueue(new MouseWheelInputEvent(value));

    public static int AllocateTouchId() => Interlocked.Decrement(ref _nextTouchId);

    public static void EnqueueTouchPressed(int id, Vector2 position) =>
        _events.Enqueue(new TouchInputEvent(id, position, TouchLocationState.Pressed));

    public static void EnqueueTouchMoved(int id, Vector2 position) =>
        _events.Enqueue(new TouchInputEvent(id, position, TouchLocationState.Moved));

    public static void EnqueueTouchReleased(int id, Vector2 position) =>
        _events.Enqueue(new TouchInputEvent(id, position, TouchLocationState.Released));

    public static void Clear()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }

    internal static void BeforeFrame()
    {
        while (_events.TryDequeue(out var inputEvent))
        {
            inputEvent.Deliver();
        }
    }
}

#if ANDROID
using System.Collections.Concurrent;

using Android.Views;
#endif

using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static class Touch
{
#if ANDROID
    public struct TouchInfo(int pointerId, Vector2 position, int actionMasked)
    {
        public readonly int PointerId = pointerId;

        public Vector2 Position = position;

        public readonly int ActionMasked = actionMasked;
    }
#endif

    private static readonly List<TouchLocation> _touchLocations = [];

    public static ReadOnlyList<TouchLocation> TouchLocations => new(_touchLocations);

    public static event Action<TouchLocation>? TouchPressed;

    public static event Action<TouchLocation>? TouchReleased;

    public static event Action<TouchLocation>? TouchMoved;

    public static bool IsTouched;

    internal static void Initialize()
    {
    }

    internal static void Dispose()
    {
    }

#if ANDROID
    public static ConcurrentQueue<TouchInfo> CachedTouchEvents = [];

    public static void EnqueueTouchEvent(int pointerId, Vector2 position, int actionMasked) =>
        CachedTouchEvents.Enqueue(new TouchInfo(pointerId, position, actionMasked));

    internal static void HandleTouchEvent(MotionEvent e)
    {
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
            case MotionEventActions.Pointer1Down:
                CachedTouchEvents.Enqueue(
                    new TouchInfo(e.GetPointerId(e.ActionIndex),
                        new Vector2(e.GetX(e.ActionIndex), e.GetY(e.ActionIndex)), 1)
                ); break;
            case MotionEventActions.Move:
                for (var i = 0; i < e.PointerCount; i++)
                {
                    CachedTouchEvents.Enqueue(new TouchInfo(e.GetPointerId(i), new Vector2(e.GetX(i), e.GetY(i)), 2));
                }

                break;
            case MotionEventActions.Up:
            case MotionEventActions.Pointer1Up:
            case MotionEventActions.Cancel:
            case MotionEventActions.Outside:
                CachedTouchEvents.Enqueue(
                    new TouchInfo(e.GetPointerId(e.ActionIndex),
                        new Vector2(e.GetX(e.ActionIndex), e.GetY(e.ActionIndex)), 3)
                ); break;
        }
    }
#endif

    public static void Clear() => _touchLocations.Clear();

    internal static void BeforeFrame()
    {
#if ANDROID
        while (!CachedTouchEvents.IsEmpty)
        {
            if (CachedTouchEvents.TryDequeue(out var touchInfo))
            {
                switch (touchInfo.ActionMasked)
                {
                    case 1: ProcessTouchPressed(touchInfo.PointerId, touchInfo.Position); break;
                    case 2: ProcessTouchMoved(touchInfo.PointerId, touchInfo.Position); break;
                    case 3: ProcessTouchReleased(touchInfo.PointerId, touchInfo.Position); break;
                }
            }
            else
            {
                Thread.Yield();
            }
        }
#endif
    }

    internal static void AfterFrame()
    {
        for (var i = 0; i < _touchLocations.Count; i++)
        {
            if (_touchLocations[i].State == TouchLocationState.Released)
            {
                _touchLocations.RemoveAt(i);
                continue;
            }

            if (_touchLocations[i].releaseQueued)
            {
                _touchLocations[i] = new TouchLocation
                {
                    Id = _touchLocations[i].Id, Position = _touchLocations[i].Position,
                    State = TouchLocationState.Released
                };
            }
            else if (_touchLocations[i].State == TouchLocationState.Pressed)
            {
                _touchLocations[i] = new TouchLocation
                {
                    Id = _touchLocations[i].Id, Position = _touchLocations[i].Position,
                    State = TouchLocationState.Moved
                };
            }
        }
    }

    static int FindTouchLocationIndex(int id)
    {
        for (var i = 0; i < _touchLocations.Count; i++)
        {
            if (_touchLocations[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    public static void ProcessTouchPressed(int id, Vector2 position) => ProcessTouchMoved(id, position);

    public static void ProcessTouchMoved(int id, Vector2 position)
    {
        if (!Window.IsActive
            || Keyboard.IsKeyboardVisible)
        {
            return;
        }

        IsTouched = true;
        var num = FindTouchLocationIndex(id);
        if (num >= 0)
        {
            if (_touchLocations[num].State == TouchLocationState.Moved)
            {
                _touchLocations[num] = new TouchLocation
                    { Id = id, Position = position, State = TouchLocationState.Moved };
            }

            TouchMoved?.Invoke(_touchLocations[num]);
        }
        else
        {
            _touchLocations.Add(new TouchLocation
                { Id = id, Position = position, State = TouchLocationState.Pressed });
            TouchPressed?.Invoke(_touchLocations[^1]);
        }
    }

    public static void ProcessTouchReleased(int id, Vector2 position)
    {
        if (!Window.IsActive || Keyboard.IsKeyboardVisible)
        {
            return;
        }

        var num = FindTouchLocationIndex(id);
        if (num < 0)
        {
            return;
        }

        if (_touchLocations[num].State == TouchLocationState.Pressed)
        {
            _touchLocations[num] = new TouchLocation
            {
                Id = id, Position = position, State = TouchLocationState.Pressed, releaseQueued = true
            };
        }
        else
        {
            _touchLocations[num] = new TouchLocation
                { Id = id, Position = position, State = TouchLocationState.Released };
        }

        TouchReleased?.Invoke(_touchLocations[num]);
    }
}

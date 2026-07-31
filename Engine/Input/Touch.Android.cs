#if ANDROID
using System.Collections.Concurrent;

using Android.Views;

using Engine.Core;

namespace Engine.Input;

public static partial class Touch
{
    public struct TouchInfo(int pointerId, Vector2 position, int actionMasked)
    {
        public readonly int PointerId = pointerId;
        public Vector2 Position = position;
        public readonly int ActionMasked = actionMasked;
    }

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
                );
                break;
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
                );
                break;
        }
    }

    static partial void BeforeFramePlatform()
    {
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
    }
}
#endif

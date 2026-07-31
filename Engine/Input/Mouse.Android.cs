#if ANDROID
using System.Collections.Concurrent;

using Android.OS;
using Android.Views;

using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static partial class Mouse
{
    public struct MouseButtonInfo(MouseButton button, bool press, Point2 position)
    {
        public readonly MouseButton Button = button;
        public readonly bool Press = press;
        public Point2 Position = position;
    }

    public static ConcurrentQueue<MouseButtonInfo> CachedMouseButtonEvents = [];

    private static Vector2 _queuedMouseMovement;

    private static float _queuedMouseWheelMovement;

    private static bool _pointerCaptureRequested;

    static partial void InitializePlatform()
    {
        if (Build.VERSION.SdkInt >= (BuildVersionCodes)26)
        {
            Window.Surface.SetOnCapturedPointerListener(new OnCapturedPointerListener());
        }
    }

    static partial void BeforeFramePlatform()
    {
        if (IsMouseVisible)
        {
            if (_pointerCaptureRequested)
            {
                _pointerCaptureRequested = false;
                if (Build.VERSION.SdkInt >= (BuildVersionCodes)26)
                {
                    Window.Surface?.ReleasePointerCapture();
                }

                Clear();
            }

            MouseMovement = Point2.Zero;
            _lastMousePosition = null;
        }
        else
        {
            if (!_pointerCaptureRequested)
            {
                _pointerCaptureRequested = true;
                if (Build.VERSION.SdkInt >= (BuildVersionCodes)26)
                {
                    Window.Surface?.RequestPointerCapture();
                }
            }

            if (_lastMousePosition.HasValue)
            {
                MouseMovement = Point2.Round(_queuedMouseMovement.X, _queuedMouseMovement.Y);
            }

            _lastMousePosition = Point2.Zero;
            _queuedMouseMovement = Vector2.Zero;
        }

        MouseWheelMovement = (int)MathUtils.Round(_queuedMouseWheelMovement) * 120;
        _queuedMouseWheelMovement = 0f;
        while (!CachedMouseButtonEvents.IsEmpty)
        {
            if (CachedMouseButtonEvents.TryDequeue(out var buttonInfo))
            {
                if (buttonInfo.Press)
                {
                    ProcessMouseDown(buttonInfo.Button, buttonInfo.Position);
                }
                else
                {
                    ProcessMouseUp(buttonInfo.Button, buttonInfo.Position);
                }
            }
            else
            {
                Thread.Yield();
            }
        }
    }

    static partial void AfterFramePlatform()
    {
        if (!IsMouseVisible)
        {
            MousePosition = new Point2();
        }
    }

    static partial void SetCursorTypePlatform(CursorType cursorType)
    {
        if (Build.VERSION.SdkInt >= (BuildVersionCodes)24)
        {
            Window.Surface?.PointerIcon = PointerIcon.GetSystemIcon(
                Application.Context,
                TranslateCursorType(cursorType)
            );
        }
    }

    public static void EnqueueMouseButtonEvent(MouseButton button, bool press, Point2 position) =>
        CachedMouseButtonEvents.Enqueue(new MouseButtonInfo(button, press, position));

    internal static void HandleMotionEvent(MotionEvent e)
    {
        switch (e.Action)
        {
            case MotionEventActions.Move:
            {
                for (var num = e.HistorySize - 1; num >= 0; num--)
                {
                    _queuedMouseMovement += new Vector2(e.GetHistoricalX(num), e.GetHistoricalY(num));
                }

                MousePosition = Point2.Round(e.GetX(), e.GetY());
                break;
            }
            case MotionEventActions.HoverMove: MousePosition = Point2.Round(e.GetX(), e.GetY()); break;
            case MotionEventActions.ButtonPress:
                EnqueueMouseButtonEvent(TranslateMouseButton(e.ActionButton), true,
                    Point2.Round(e.GetX(), e.GetY())); break;
            case MotionEventActions.ButtonRelease:
                EnqueueMouseButtonEvent(TranslateMouseButton(e.ActionButton), false,
                    Point2.Round(e.GetX(), e.GetY())); break;
            case MotionEventActions.PointerIdShift:
            {
                for (var num2 = e.HistorySize - 1; num2 >= 0; num2--)
                {
                    _queuedMouseWheelMovement += MathUtils.Sign(e.GetHistoricalAxisValue(Axis.Vscroll, num2));
                }

                _queuedMouseWheelMovement += MathUtils.Sign(e.GetAxisValue(Axis.Vscroll));
                break;
            }
        }
    }

    public static MouseButton TranslateMouseButton(MotionEventButtonState state) => state switch
    {
        MotionEventButtonState.Primary => MouseButton.Left,
        MotionEventButtonState.Secondary => MouseButton.Right,
        MotionEventButtonState.Tertiary => MouseButton.Middle,
        MotionEventButtonState.Back => MouseButton.Ext1,
        MotionEventButtonState.Forward => MouseButton.Ext2,
        _ => MouseButton.Left
    };

    public static PointerIconType TranslateCursorType(CursorType cursorType) => cursorType switch
    {
        CursorType.Arrow => PointerIconType.Arrow,
        CursorType.IBeam => PointerIconType.Text,
        CursorType.Crosshair => PointerIconType.Crosshair,
        CursorType.Hand => PointerIconType.Hand,
        CursorType.HResize => PointerIconType.HorizontalDoubleArrow,
        CursorType.VResize => PointerIconType.VerticalDoubleArrow,
        CursorType.NwseResize => PointerIconType.TopLeftDiagonalDoubleArrow,
        CursorType.NeswResize => PointerIconType.TopRightDiagonalDoubleArrow,
        CursorType.ResizeAll => PointerIconType.AllScroll,
        CursorType.NotAllowed => PointerIconType.NoDrop,
        CursorType.Grab => PointerIconType.Grab,
        CursorType.Grabbing => PointerIconType.Grabbing,
        _ => PointerIconType.Default
    };

    public class OnCapturedPointerListener : Java.Lang.Object, View.IOnCapturedPointerListener
    {
        public bool OnCapturedPointer(View? view, MotionEvent? e)
        {
            if (e == null)
            {
                return true;
            }

            if ((e.Source & InputSourceType.MouseRelative) == InputSourceType.MouseRelative)
            {
                HandleMotionEvent(e);
            }

            return true;
        }
    }
}
#endif

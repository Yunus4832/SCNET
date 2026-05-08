#if ANDROID
using System.Collections.Concurrent;
using Android.OS;
using Android.Views;
#endif
#if DESKTOP
using Silk.NET.Input;
#endif
using Engine.Core;
using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static class Mouse
{
#if ANDROID
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

#endif
#if DESKTOP
    public static IMouse Default = null!;
#endif

    private static Point2? _lastMousePosition;

    private static readonly bool[] _mouseButtonsDownArray;

    private static readonly bool[] _mouseButtonsDownOnceArray;

    private static readonly bool[] _mouseButtonsDelayedUpArray;

    private static readonly bool[] _mouseButtonsUpOnceArray;

    static Mouse()
    {
        _mouseButtonsDownArray = new bool[Enum.GetValues<MouseButton>().Length];
        _mouseButtonsDelayedUpArray = new bool[Enum.GetValues<MouseButton>().Length];
        _mouseButtonsDownOnceArray = new bool[Enum.GetValues<MouseButton>().Length];
        _mouseButtonsUpOnceArray = new bool[Enum.GetValues<MouseButton>().Length];
        IsMouseVisible = true;
    }

    public static Point2 MouseMovement { get; private set; }

    public static int MouseWheelMovement { get; private set; }

    public static Point2? MousePosition { get; private set; }

    public static bool IsMouseVisible { get; set; }

    public static event Action<MouseEvent>? MouseMove;

    public static event Action<MouseButtonEvent>? MouseDown;

    public static event Action<MouseButtonEvent>? MouseUp;

    public static void SetMousePosition(int x, int y)
    {
#if DESKTOP
        Default.Position = new System.Numerics.Vector2(x, y);
#endif
    }

    internal static void Initialize()
    {
#if ANDROID
        if (Build.VERSION.SdkInt >= (BuildVersionCodes)26)
        {
            Window.Surface.SetOnCapturedPointerListener(new OnCapturedPointerListener());
        }
#endif
#if DESKTOP
        if (Window.InputContext is null)
        {
            throw new InvalidOperationException("Window.InputContext is not set");
        }

        Default = Window.InputContext.Mice[0];
        Default.MouseDown += MouseDownHandler;
        Default.MouseUp += MouseUpHandler;
        Default.MouseMove += MouseMoveHandler;
        Default.Scroll += MouseWheelHandler;
#endif
    }

    internal static void Dispose()
    {
    }

    internal static void BeforeFrame()
    {
#if ANDROID
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
#endif
#if DESKTOP
        var position = Point2.Round(Default.Position.X, Default.Position.Y);
        if (Window.IsActive)
        {
            ProcessMouseMove(position);
            if (IsMouseVisible)
            {
                Default.Cursor.CursorMode = CursorMode.Normal;
                MouseMovement = Point2.Zero;
                _lastMousePosition = null;
            }
            else
            {
                Default.Cursor.CursorMode = CursorMode.Raw;
                if (_lastMousePosition.HasValue)
                {
                    MouseMovement = new Point2(
                        position.X - _lastMousePosition.Value.X,
                        position.Y - _lastMousePosition.Value.Y) * Window.Scale;
                }

                var windowSize = Window.Size;
                if (position.X < 0
                    || position.X >= windowSize.X
                    || position.Y < 0
                    || position.Y >= windowSize.Y)
                {
                    position = new Point2(windowSize.X / 2, windowSize.Y / 2);
                    SetMousePosition(position.X, position.Y);
                }

                _lastMousePosition = position;
            }
        }
        else
        {
            _lastMousePosition = null;
        }
#endif
    }

#if ANDROID
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
#endif

#if DESKTOP
    private static void MouseDownHandler(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        var mouseButton = TranslateMouseButton(button);
        if (mouseButton is null)
        {
            return;
        }

        var position = Point2.Round(mouse.Position.X, mouse.Position.Y);
        ProcessMouseDown(mouseButton.Value, position);
    }

    private static void MouseUpHandler(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        var mouseButton = TranslateMouseButton(button);
        if (mouseButton is null)
        {
            return;
        }

        var position = Point2.Round(mouse.Position.X, mouse.Position.Y);
        ProcessMouseUp(mouseButton.Value, position);
    }

    private static void MouseMoveHandler(IMouse mouse, System.Numerics.Vector2 position)
    {
        ProcessMouseMove(new Point2((int)position.X, (int)position.Y));
    }

    private static void MouseWheelHandler(IMouse mouse, ScrollWheel scrollWheel) => ProcessMouseWheel(scrollWheel.Y);

    private static MouseButton? TranslateMouseButton(Silk.NET.Input.MouseButton mouseButton)
    {
        return mouseButton switch
        {
            Silk.NET.Input.MouseButton.Left => MouseButton.Left,
            Silk.NET.Input.MouseButton.Right => MouseButton.Right,
            Silk.NET.Input.MouseButton.Middle => MouseButton.Middle,
            _ => null
        };
    }

    public static StandardCursor TranslateCursorType(CursorType cursorType) => cursorType switch
    {
        CursorType.Arrow => StandardCursor.Arrow,
        CursorType.IBeam => StandardCursor.IBeam,
        CursorType.Crosshair => StandardCursor.Crosshair,
        CursorType.Hand or CursorType.Grab or CursorType.Grabbing => StandardCursor.Hand,
        CursorType.HResize => StandardCursor.HResize,
        CursorType.VResize => StandardCursor.VResize,
        CursorType.NwseResize => StandardCursor.NwseResize,
        CursorType.NeswResize => StandardCursor.NeswResize,
        CursorType.ResizeAll => StandardCursor.ResizeAll,
        CursorType.NotAllowed => StandardCursor.NotAllowed,
        CursorType.Wait => StandardCursor.Wait,
        CursorType.WaitArrow => StandardCursor.WaitArrow,
        _ => StandardCursor.Default
    };
#endif

    public static bool IsMouseButtonDown(MouseButton mouseButton) => _mouseButtonsDownArray[(int)mouseButton];

    public static bool IsMouseButtonDownOnce(MouseButton mouseButton) => _mouseButtonsDownOnceArray[(int)mouseButton];

    public static bool IsMouseButtonUpOnce(MouseButton mouseButton) => _mouseButtonsUpOnceArray[(int)mouseButton];

    public static void Clear()
    {
        for (var i = 0; i < _mouseButtonsDownArray.Length; i++)
        {
            _mouseButtonsDownArray[i] = false;
            _mouseButtonsDelayedUpArray[i] = false;
            _mouseButtonsDownOnceArray[i] = false;
            _mouseButtonsUpOnceArray[i] = false;
        }
    }

    internal static void AfterFrame()
    {
        for (var i = 0; i < _mouseButtonsDownArray.Length; i++)
        {
            _mouseButtonsDownOnceArray[i] = false;
            if (_mouseButtonsDelayedUpArray[i])
            {
                _mouseButtonsDelayedUpArray[i] = false;
                _mouseButtonsDownArray[i] = false;
                _mouseButtonsUpOnceArray[i] = true;
            }
            else
            {
                _mouseButtonsUpOnceArray[i] = false;
            }
        }

#if ANDROID
        if (!IsMouseVisible)
        {
            MousePosition = new Point2();
        }
#endif
#if DESKTOP
        if (!IsMouseVisible)
        {
            MousePosition = new Point2();
            Default.Cursor.CursorMode = Window.IsActive ? CursorMode.Raw : CursorMode.Normal;
        }
        else
        {
            Default.Cursor.CursorMode = CursorMode.Normal;
        }
#endif

        MouseWheelMovement = 0;
    }

    private static void ProcessMouseDown(MouseButton mouseButton, Point2 position)
    {
        if (!Window.IsActive || Keyboard.IsKeyboardVisible)
        {
            return;
        }

        _mouseButtonsDownArray[(int)mouseButton] = true;
        _mouseButtonsDownOnceArray[(int)mouseButton] = true;

        var scaledPosition = position * Window.Scale;

        if (IsMouseVisible && MouseDown != null)
        {
            MouseDown(new MouseButtonEvent
            {
                Button = mouseButton,
                Position = scaledPosition,
            });
        }
    }

    private static void ProcessMouseUp(MouseButton mouseButton, Point2 position)
    {
        if (!Window.IsActive || Keyboard.IsKeyboardVisible)
        {
            return;
        }

        _mouseButtonsDownArray[(int)mouseButton] = false;
        var scaledPosition = position * Window.Scale;
        if (IsMouseVisible && MouseUp != null)
        {
            MouseUp(new MouseButtonEvent
            {
                Button = mouseButton,
                Position = scaledPosition,
            });
        }
    }

    private static void ProcessMouseMove(Point2 position)
    {
        if (!Window.IsActive || Keyboard.IsKeyboardVisible || !IsMouseVisible)
        {
            return;
        }

        var scaledPosition = position * Window.Scale;
        MousePosition = scaledPosition;
        MouseMove?.Invoke(new MouseEvent
        {
            Position = scaledPosition,
        });
    }

    public static void ProcessMouseWheel(float value)
    {
        if (Window.IsActive
            && !Keyboard.IsKeyboardVisible)
        {
            MouseWheelMovement += (int)(120 * value);
        }
    }

    public static void SetCursorType(CursorType cursorType)
    {
#if ANDROID
        if (Build.VERSION.SdkInt >= (BuildVersionCodes)24)
        {
            Window.Surface?.PointerIcon = PointerIcon.GetSystemIcon(
                Application.Context,
                TranslateCursorType(cursorType)
            );
        }
#endif
#if DESKTOP
        Default.Cursor.StandardCursor = TranslateCursorType(cursorType);
#endif
    }
}

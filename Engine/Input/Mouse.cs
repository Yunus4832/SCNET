using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static partial class Mouse
{
    private static Point2? _lastMousePosition;

    private static readonly bool[] _mouseButtonsDownArray;

    private static readonly bool[] _physicalMouseButtonsDownArray;

    private static readonly bool[] _simulatedMouseButtonsDownArray;

    private static readonly bool[] _mouseButtonsDownOnceArray;

    private static readonly bool[] _mouseButtonsDelayedUpArray;

    private static readonly bool[] _mouseButtonsUpOnceArray;

    static Mouse()
    {
        _mouseButtonsDownArray = new bool[Enum.GetValues<MouseButton>().Length];
        _physicalMouseButtonsDownArray = new bool[Enum.GetValues<MouseButton>().Length];
        _simulatedMouseButtonsDownArray = new bool[Enum.GetValues<MouseButton>().Length];
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
        SetMousePositionPlatform(x, y);
    }

    internal static void Initialize()
    {
        InitializePlatform();
    }

    internal static void Dispose()
    {
    }

    internal static void BeforeFrame()
    {
        BeforeFramePlatform();
    }

    public static bool IsMouseButtonDown(MouseButton mouseButton) => _mouseButtonsDownArray[(int)mouseButton];

    public static bool IsMouseButtonDownOnce(MouseButton mouseButton) => _mouseButtonsDownOnceArray[(int)mouseButton];

    public static bool IsMouseButtonUpOnce(MouseButton mouseButton) => _mouseButtonsUpOnceArray[(int)mouseButton];

    public static void Clear()
    {
        for (var i = 0; i < _mouseButtonsDownArray.Length; i++)
        {
            _mouseButtonsDownArray[i] = false;
            _physicalMouseButtonsDownArray[i] = false;
            _simulatedMouseButtonsDownArray[i] = false;
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

        AfterFramePlatform();

        MouseWheelMovement = 0;
    }

    private static void ProcessMouseDown(MouseButton mouseButton, Point2 position, bool simulated = false)
    {
        if (!Window.IsActive)
        {
            return;
        }

        var index = (int)mouseButton;
        var wasDown = _mouseButtonsDownArray[index];
        if (simulated)
        {
            _simulatedMouseButtonsDownArray[index] = true;
        }
        else
        {
            _physicalMouseButtonsDownArray[index] = true;
        }

        _mouseButtonsDownArray[index] =
            _physicalMouseButtonsDownArray[index] || _simulatedMouseButtonsDownArray[index];
        if (!wasDown && _mouseButtonsDownArray[index])
        {
            _mouseButtonsDownOnceArray[index] = true;
        }

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

    private static void ProcessMouseUp(MouseButton mouseButton, Point2 position, bool simulated = false)
    {
        if (!Window.IsActive)
        {
            return;
        }

        var index = (int)mouseButton;
        if (simulated)
        {
            _simulatedMouseButtonsDownArray[index] = false;
        }
        else
        {
            _physicalMouseButtonsDownArray[index] = false;
        }

        _mouseButtonsDownArray[index] =
            _physicalMouseButtonsDownArray[index] || _simulatedMouseButtonsDownArray[index];
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
        if (!Window.IsActive || !IsMouseVisible)
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
        if (Window.IsActive)
        {
            MouseWheelMovement += (int)(120 * value);
        }
    }

    internal static void ProcessSimulatedMouseDown(MouseButton button, Point2 position) =>
        ProcessMouseDown(button, position, simulated: true);

    internal static void ProcessSimulatedMouseUp(MouseButton button, Point2 position) =>
        ProcessMouseUp(button, position, simulated: true);

    internal static void ProcessSimulatedMouseMove(Point2 position) => ProcessMouseMove(position);

    public static void SetCursorType(CursorType cursorType)
    {
        SetCursorTypePlatform(cursorType);
    }

    static partial void SetMousePositionPlatform(int x, int y);

    static partial void InitializePlatform();

    static partial void BeforeFramePlatform();

    static partial void AfterFramePlatform();

    static partial void SetCursorTypePlatform(CursorType cursorType);
}

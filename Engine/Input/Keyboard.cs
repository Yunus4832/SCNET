using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static partial class Keyboard
{
    private const double _keyFirstRepeatTime = 0.2;

    private const double _keyNextRepeatTime = 0.033;

    private static readonly bool[] _keysDownArray = new bool[Enum.GetValues(typeof(Key)).Length];

    private static readonly bool[] _keysDownOnceArray = new bool[Enum.GetValues(typeof(Key)).Length];

    private static readonly double[] _keysDownRepeatArray = new double[Enum.GetValues(typeof(Key)).Length];

    public static string? LastString;

    public static Key? LastKey { get; private set; }

    public static char? LastChar { get; private set; }

    public static bool IsKeyboardVisible => TextInputManager.IsNativeDialogVisible;

    public static bool BackButtonQuitsApp { get; set; }

    public static event Action<Key>? KeyDown;

    public static event Action<Key>? KeyUp;

    public static event Action<char>? CharacterEntered;

    public static bool IsKeyDown(Key key)
    {
        return _keysDownArray[(int)key];
    }

    public static bool IsKeyDownOnce(Key key)
    {
        return _keysDownOnceArray[(int)key];
    }

    public static bool IsKeyDownRepeat(Key key)
    {
        var num = _keysDownRepeatArray[(int)key];
        if (num < 0.0)
        {
            return true;
        }

        if (num != 0.0)
        {
            return Time.FrameStartTime >= num;
        }

        return false;
    }

    public static void ShowKeyboard(string title, string description, string defaultText, bool passwordMode,
        Action<string>? enter, Action? cancel)
    {
        Clear();
        Touch.Clear();
        Mouse.Clear();
        TextInputManager.ShowKeyboard(title, description, defaultText, passwordMode, enter, cancel);
    }

    public static void Clear()
    {
        LastKey = null;
        LastChar = null;
        for (var i = 0; i < _keysDownArray.Length; i++)
        {
            _keysDownArray[i] = false;
            _keysDownOnceArray[i] = false;
            _keysDownRepeatArray[i] = 0.0;
        }
    }

    internal static void BeforeFrame()
    {
        BeforeFramePlatform();
    }

    internal static void AfterFrame()
    {
        if (BackButtonQuitsApp && IsKeyDownOnce(Key.Back))
        {
            Window.Close();
        }

        KeyboardInput.ClearKeyActions();
        LastKey = null;
        LastChar = null;
        for (var i = 0; i < _keysDownOnceArray.Length; i++)
        {
            _keysDownOnceArray[i] = false;
        }

        for (var j = 0; j < _keysDownRepeatArray.Length; j++)
        {
            if (_keysDownArray[j])
            {
                if (_keysDownRepeatArray[j] < 0.0)
                {
                    _keysDownRepeatArray[j] = Time.FrameStartTime + 0.2;
                }
                else if (Time.FrameStartTime >= _keysDownRepeatArray[j])
                {
                    _keysDownRepeatArray[j] = MathUtils.Max(Time.FrameStartTime, _keysDownRepeatArray[j] + 0.033);
                }
            }
            else
            {
                _keysDownRepeatArray[j] = 0.0;
            }
        }
    }
    private static bool ProcessKeyDown(Key key)
    {
        if (!Window.IsActive || IsKeyboardVisible)
        {
            return false;
        }

        if (key is Key.BackSpace)
        {
            KeyboardInput.BackspacePressed = true;
        }
        else if (key is Key.Delete)
        {
            KeyboardInput.DeletePressed = true;
        }

        LastKey = key;
        if (!_keysDownArray[(int)key])
        {
            _keysDownArray[(int)key] = true;
            _keysDownOnceArray[(int)key] = true;
            _keysDownRepeatArray[(int)key] = -1.0;
        }

        KeyDown?.Invoke(key);
        return true;
    }

    private static bool ProcessKeyUp(Key key)
    {
        if (!Window.IsActive || IsKeyboardVisible)
        {
            return false;
        }

        if (_keysDownArray[(int)key])
        {
            _keysDownArray[(int)key] = false;
        }

        KeyUp?.Invoke(key);
        return true;
    }

    private static bool ProcessCharacterEntered(char ch)
    {
        if (!Window.IsActive || IsKeyboardVisible || char.IsControl(ch))
        {
            return false;
        }

        KeyboardInput.Chars.Add(ch);
        LastChar = ch;
        CharacterEntered?.Invoke(ch);
        return true;
    }

    internal static void Initialize()
    {
        InitializePlatform();
    }

    internal static void Dispose()
    {
    }

    static partial void BeforeFramePlatform();

    static partial void InitializePlatform();
}

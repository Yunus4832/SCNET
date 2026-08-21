using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static partial class Keyboard
{
    private const double _keyFirstRepeatTime = 0.2;

    private const double _keyNextRepeatTime = 0.033;

    private static readonly bool[] _keysDownArray = new bool[Enum.GetValues(typeof(Key)).Length];

    private static readonly bool[] _physicalKeysDownArray = new bool[Enum.GetValues(typeof(Key)).Length];

    private static readonly bool[] _simulatedKeysDownArray = new bool[Enum.GetValues(typeof(Key)).Length];

    private static readonly bool[] _keysDownOnceArray = new bool[Enum.GetValues(typeof(Key)).Length];

    private static readonly double[] _keysDownRepeatArray = new double[Enum.GetValues(typeof(Key)).Length];

    public static string? LastString;

    public static Key? LastKey { get; private set; }

    public static char? LastChar { get; private set; }

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

    public static void Clear()
    {
        LastKey = null;
        LastChar = null;
        for (var i = 0; i < _keysDownArray.Length; i++)
        {
            _keysDownArray[i] = false;
            _physicalKeysDownArray[i] = false;
            _simulatedKeysDownArray[i] = false;
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

    private static bool ProcessKeyDown(Key key, bool simulated = false)
    {
        if (!Window.IsActive)
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
        var index = (int)key;
        var wasDown = _keysDownArray[index];
        if (simulated)
        {
            _simulatedKeysDownArray[index] = true;
        }
        else
        {
            _physicalKeysDownArray[index] = true;
        }

        _keysDownArray[index] = _physicalKeysDownArray[index] || _simulatedKeysDownArray[index];
        if (!wasDown && _keysDownArray[index])
        {
            _keysDownOnceArray[index] = true;
            _keysDownRepeatArray[index] = -1.0;
        }

        KeyDown?.Invoke(key);
        return true;
    }

    private static bool ProcessKeyUp(Key key, bool simulated = false)
    {
        if (!Window.IsActive)
        {
            return false;
        }

        var index = (int)key;
        if (simulated)
        {
            _simulatedKeysDownArray[index] = false;
        }
        else
        {
            _physicalKeysDownArray[index] = false;
        }

        _keysDownArray[index] = _physicalKeysDownArray[index] || _simulatedKeysDownArray[index];

        KeyUp?.Invoke(key);
        return true;
    }

    private static bool ProcessCharacterEntered(char ch)
    {
        if (!Window.IsActive || char.IsControl(ch))
        {
            return false;
        }

        KeyboardInput.Chars.Add(ch);
        LastChar = ch;
        CharacterEntered?.Invoke(ch);
        return true;
    }

    internal static bool ProcessSimulatedKeyDown(Key key) => ProcessKeyDown(key, simulated: true);

    internal static bool ProcessSimulatedKeyUp(Key key) => ProcessKeyUp(key, simulated: true);

    internal static bool ProcessSimulatedCharacter(char character) => ProcessCharacterEntered(character);

    private static void ProcessPlatformKeyDown(Key key, int scanCode)
    {
        var handledByTextInput = TextInputManager.ProcessKey(CreateTextInputKeyEvent(key, scanCode, false));
        if (!handledByTextInput || key is Key.Tilde)
        {
            ProcessKeyDown(key);
        }
    }

    private static void ProcessPlatformKeyUp(Key key, int scanCode)
    {
        var handledByTextInput = TextInputManager.ProcessKey(CreateTextInputKeyEvent(key, scanCode, true));
        if (!handledByTextInput || key is Key.Tilde)
        {
            ProcessKeyUp(key);
        }
    }

    private static void ProcessPlatformCharacter(char character)
    {
        if (!TextInputManager.SuppressDirectText)
        {
            ProcessCharacterEntered(character);
        }
    }

    private static TextInputKeyEvent CreateTextInputKeyEvent(Key key, int scanCode, bool isRelease)
    {
        var modifiers = TextInputModifiers.None;
        if ((IsKeyDown(Key.Shift) || key is Key.Shift) && !(isRelease && key is Key.Shift))
        {
            modifiers |= TextInputModifiers.Shift;
        }

        if ((IsKeyDown(Key.Control) || key is Key.Control) && !(isRelease && key is Key.Control))
        {
            modifiers |= TextInputModifiers.Control;
        }

        if ((IsKeyDown(Key.Alt) || key is Key.Alt) && !(isRelease && key is Key.Alt))
        {
            modifiers |= TextInputModifiers.Alt;
        }

        return new TextInputKeyEvent(key, scanCode, isRelease, modifiers);
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

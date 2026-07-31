#if ANDROID
using System.Collections.Concurrent;

using Android.Views;
#endif
#if DESKTOP
using Silk.NET.Input;
#endif

using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static class Keyboard
{
#if ANDROID
    public struct KeyInfo(Key? key, bool press, int? unicodeChar)
    {
        public Key? Key = key;
        public readonly bool Press = press;
        public int? UnicodeChar = unicodeChar;
    }

    public static ConcurrentQueue<KeyInfo> CachedKeyEvents = [];
#endif
#if DESKTOP
    public static IKeyboard Default = null!;
#endif

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
#if ANDROID
        while (!CachedKeyEvents.IsEmpty)
        {
            if (CachedKeyEvents.TryDequeue(out var keyInfo))
            {
                if (keyInfo.Press)
                {
                    if (keyInfo.Key is not null)
                    {
                        ProcessKeyDown(keyInfo.Key.Value);
                    }

                    if (keyInfo.UnicodeChar.HasValue)
                    {
                        ProcessCharacterEntered((char)keyInfo.UnicodeChar.Value);
                    }
                }
                else if (keyInfo.Key is not null)
                {
                    ProcessKeyUp(keyInfo.Key.Value);
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
#if ANDROID
    public static void EnqueueMouseButtonEvent(Key? key, bool press, int? unicodeChar)
    {
        CachedKeyEvents.Enqueue(new KeyInfo(key, press, unicodeChar));
    }
#endif

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
#if DESKTOP
        if (Window.InputContext is null)
        {
            throw new InvalidOperationException("Window.InputContext is not set");
        }

        Default = Window.InputContext.Keyboards[0];
        Default.KeyDown += KeyDownHandler;
        Default.KeyUp += KeyUpHandler;
        Default.KeyChar += KeyPressHandler;
#endif
    }

    internal static void Dispose()
    {
    }

#if ANDROID
    public static void HandleKeyEvent(KeyEvent keyEvent)
    {
        EnqueueMouseButtonEvent(TranslateKey(keyEvent.KeyCode), keyEvent.Action == KeyEventActions.Down,
            keyEvent.UnicodeChar);
    }
#endif

#if DESKTOP
    private static void KeyDownHandler(IKeyboard keyboard, Silk.NET.Input.Key key, int scancode)
    {
        var translatedKey = TranslateKey(key);
        if (translatedKey is not null)
        {
            if (TextInputManager.ProcessKey(CreateTextInputKeyEvent(translatedKey.Value, scancode, false)))
            {
                return;
            }

            ProcessKeyDown(translatedKey.Value);
        }
        else if (scancode == 270)
        {
            ProcessKeyDown(Key.Back);
        }
    }

    private static void KeyUpHandler(IKeyboard keyboard, Silk.NET.Input.Key key, int scancode)
    {
        var translatedKey = TranslateKey(key);
        if (translatedKey is not null)
        {
            if (TextInputManager.ProcessKey(CreateTextInputKeyEvent(translatedKey.Value, scancode, true)))
            {
                return;
            }

            ProcessKeyUp(translatedKey.Value);
        }
        else if (scancode == 270)
        {
            ProcessKeyUp(Key.Back);
        }
    }

    private static void KeyPressHandler(IKeyboard keyboard, char c)
    {
        if (!TextInputManager.SuppressDirectText)
        {
            ProcessCharacterEntered(c);
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
#endif

#if ANDROID
    public static Key? TranslateKey(Keycode keyCode) => keyCode switch
    {
        Keycode.Home => Key.Home,
        Keycode.Back => Key.Back,
        Keycode.Num0 or Keycode.Numpad0 => Key.Number0,
        Keycode.Num1 or Keycode.Numpad1 => Key.Number1,
        Keycode.Num2 or Keycode.Numpad2 => Key.Number2,
        Keycode.Num3 or Keycode.Numpad3 => Key.Number3,
        Keycode.Num4 or Keycode.Numpad4 => Key.Number4,
        Keycode.Num5 or Keycode.Numpad5 => Key.Number5,
        Keycode.Num6 or Keycode.Numpad6 => Key.Number6,
        Keycode.Num7 or Keycode.Numpad7 => Key.Number7,
        Keycode.Num8 or Keycode.Numpad8 => Key.Number8,
        Keycode.Num9 or Keycode.Numpad9 => Key.Number9,
        Keycode.A => Key.A,
        Keycode.B => Key.B,
        Keycode.C => Key.C,
        Keycode.D => Key.D,
        Keycode.E => Key.E,
        Keycode.F => Key.F,
        Keycode.G => Key.G,
        Keycode.H => Key.H,
        Keycode.I => Key.I,
        Keycode.J => Key.J,
        Keycode.K => Key.K,
        Keycode.L => Key.L,
        Keycode.M => Key.M,
        Keycode.N => Key.N,
        Keycode.O => Key.O,
        Keycode.P => Key.P,
        Keycode.Q => Key.Q,
        Keycode.R => Key.R,
        Keycode.S => Key.S,
        Keycode.T => Key.T,
        Keycode.U => Key.U,
        Keycode.V => Key.V,
        Keycode.W => Key.W,
        Keycode.X => Key.X,
        Keycode.Y => Key.Y,
        Keycode.Z => Key.Z,
        Keycode.Comma => Key.Comma,
        Keycode.Period or Keycode.NumpadDot => Key.Period,
        Keycode.ShiftLeft or Keycode.ShiftRight => Key.Shift,
        Keycode.Tab => Key.Tab,
        Keycode.Space => Key.Space,
        Keycode.Enter or Keycode.NumpadEnter => Key.Enter,
        Keycode.Del => Key.BackSpace,
        Keycode.Minus or Keycode.NumpadSubtract => Key.Minus,
        Keycode.LeftBracket => Key.LeftBracket,
        Keycode.RightBracket => Key.RightBracket,
        Keycode.Semicolon => Key.Semicolon,
        Keycode.Slash or Keycode.NumpadDivide => Key.Slash,
        Keycode.Backslash => Key.BackSlash,
        Keycode.Equals or Keycode.Plus => Key.Plus,
        Keycode.PageUp => Key.PageUp,
        Keycode.PageDown => Key.PageDown,
        Keycode.Escape => Key.Escape,
        Keycode.ForwardDel => Key.Delete,
        Keycode.CtrlLeft or Keycode.CtrlRight => Key.Control,
        Keycode.CapsLock => Key.CapsLock,
        Keycode.Insert => Key.Insert,
        Keycode.F1 => Key.F1,
        Keycode.F2 => Key.F2,
        Keycode.F3 => Key.F3,
        Keycode.F4 => Key.F4,
        Keycode.F5 => Key.F5,
        Keycode.F6 => Key.F6,
        Keycode.F7 => Key.F7,
        Keycode.F8 => Key.F8,
        Keycode.F9 => Key.F9,
        Keycode.F10 => Key.F10,
        Keycode.F11 => Key.F11,
        Keycode.F12 => Key.F12,
        Keycode.AltLeft or Keycode.AltRight => Key.Alt,
        _ => null
    };
#endif

#if DESKTOP
    private static Key? TranslateKey(Silk.NET.Input.Key key)
    {
        return key switch
        {
            Silk.NET.Input.Key.ShiftLeft => Key.Shift,
            Silk.NET.Input.Key.ShiftRight => Key.Shift,
            Silk.NET.Input.Key.ControlLeft => Key.Control,
            Silk.NET.Input.Key.ControlRight => Key.Control,
            Silk.NET.Input.Key.F1 => Key.F1,
            Silk.NET.Input.Key.F2 => Key.F2,
            Silk.NET.Input.Key.F3 => Key.F3,
            Silk.NET.Input.Key.F4 => Key.F4,
            Silk.NET.Input.Key.F5 => Key.F5,
            Silk.NET.Input.Key.F6 => Key.F6,
            Silk.NET.Input.Key.F7 => Key.F7,
            Silk.NET.Input.Key.F8 => Key.F8,
            Silk.NET.Input.Key.F9 => Key.F9,
            Silk.NET.Input.Key.F10 => Key.F10,
            Silk.NET.Input.Key.F11 => Key.F11,
            Silk.NET.Input.Key.F12 => Key.F12,
            Silk.NET.Input.Key.Up => Key.UpArrow,
            Silk.NET.Input.Key.Down => Key.DownArrow,
            Silk.NET.Input.Key.Left => Key.LeftArrow,
            Silk.NET.Input.Key.Right => Key.RightArrow,
            Silk.NET.Input.Key.Enter => Key.Enter,
            Silk.NET.Input.Key.KeypadEnter => Key.Enter,
            Silk.NET.Input.Key.Escape => Key.Escape,
            Silk.NET.Input.Key.Space => Key.Space,
            Silk.NET.Input.Key.Tab => Key.Tab,
            Silk.NET.Input.Key.Backspace => Key.BackSpace,
            Silk.NET.Input.Key.Insert => Key.Insert,
            Silk.NET.Input.Key.Delete => Key.Delete,
            Silk.NET.Input.Key.PageUp => Key.PageUp,
            Silk.NET.Input.Key.PageDown => Key.PageDown,
            Silk.NET.Input.Key.Home => Key.Home,
            Silk.NET.Input.Key.End => Key.End,
            Silk.NET.Input.Key.CapsLock => Key.CapsLock,
            Silk.NET.Input.Key.A => Key.A,
            Silk.NET.Input.Key.B => Key.B,
            Silk.NET.Input.Key.C => Key.C,
            Silk.NET.Input.Key.D => Key.D,
            Silk.NET.Input.Key.E => Key.E,
            Silk.NET.Input.Key.F => Key.F,
            Silk.NET.Input.Key.G => Key.G,
            Silk.NET.Input.Key.H => Key.H,
            Silk.NET.Input.Key.I => Key.I,
            Silk.NET.Input.Key.J => Key.J,
            Silk.NET.Input.Key.K => Key.K,
            Silk.NET.Input.Key.L => Key.L,
            Silk.NET.Input.Key.M => Key.M,
            Silk.NET.Input.Key.N => Key.N,
            Silk.NET.Input.Key.O => Key.O,
            Silk.NET.Input.Key.P => Key.P,
            Silk.NET.Input.Key.Q => Key.Q,
            Silk.NET.Input.Key.R => Key.R,
            Silk.NET.Input.Key.S => Key.S,
            Silk.NET.Input.Key.T => Key.T,
            Silk.NET.Input.Key.U => Key.U,
            Silk.NET.Input.Key.V => Key.V,
            Silk.NET.Input.Key.W => Key.W,
            Silk.NET.Input.Key.X => Key.X,
            Silk.NET.Input.Key.Y => Key.Y,
            Silk.NET.Input.Key.Z => Key.Z,
            Silk.NET.Input.Key.Number0 => Key.Number0,
            Silk.NET.Input.Key.Number1 => Key.Number1,
            Silk.NET.Input.Key.Number2 => Key.Number2,
            Silk.NET.Input.Key.Number3 => Key.Number3,
            Silk.NET.Input.Key.Number4 => Key.Number4,
            Silk.NET.Input.Key.Number5 => Key.Number5,
            Silk.NET.Input.Key.Number6 => Key.Number6,
            Silk.NET.Input.Key.Number7 => Key.Number7,
            Silk.NET.Input.Key.Number8 => Key.Number8,
            Silk.NET.Input.Key.Number9 => Key.Number9,
            Silk.NET.Input.Key.GraveAccent => Key.Tilde,
            Silk.NET.Input.Key.Minus or Silk.NET.Input.Key.KeypadSubtract => Key.Minus,
            Silk.NET.Input.Key.Equal or Silk.NET.Input.Key.KeypadAdd => Key.Plus,
            Silk.NET.Input.Key.LeftBracket => Key.LeftBracket,
            Silk.NET.Input.Key.RightBracket => Key.RightBracket,
            Silk.NET.Input.Key.Semicolon => Key.Semicolon,
            Silk.NET.Input.Key.Apostrophe => Key.Quote,
            Silk.NET.Input.Key.Comma => Key.Comma,
            Silk.NET.Input.Key.Period => Key.Period,
            Silk.NET.Input.Key.Slash => Key.Slash,
            Silk.NET.Input.Key.BackSlash => Key.BackSlash,
            Silk.NET.Input.Key.AltLeft or Silk.NET.Input.Key.AltRight => Key.Alt,
            _ => null
        };
    }
#endif
}

#if ANDROID
using System.Collections.Concurrent;

using Android.Views;

namespace Engine.Input;

public static partial class Keyboard
{
    public struct KeyInfo(Key? key, bool press, int? unicodeChar)
    {
        public Key? Key = key;
        public readonly bool Press = press;
        public int? UnicodeChar = unicodeChar;
    }

    public static ConcurrentQueue<KeyInfo> CachedKeyEvents = [];

    static partial void BeforeFramePlatform()
    {
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
    }

    public static void EnqueueMouseButtonEvent(Key? key, bool press, int? unicodeChar)
    {
        CachedKeyEvents.Enqueue(new KeyInfo(key, press, unicodeChar));
    }

    public static void HandleKeyEvent(KeyEvent keyEvent)
    {
        EnqueueMouseButtonEvent(TranslateKey(keyEvent.KeyCode), keyEvent.Action == KeyEventActions.Down,
            keyEvent.UnicodeChar);
    }

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
}
#endif

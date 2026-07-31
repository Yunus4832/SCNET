#if DESKTOP
using Silk.NET.Input;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static partial class Keyboard
{
    public static IKeyboard Default = null!;

    static partial void InitializePlatform()
    {
        if (Window.InputContext is null)
        {
            throw new InvalidOperationException("Window.InputContext is not set");
        }

        Default = Window.InputContext.Keyboards[0];
        Default.KeyDown += KeyDownHandler;
        Default.KeyUp += KeyUpHandler;
        Default.KeyChar += KeyPressHandler;
    }

    private static void KeyDownHandler(IKeyboard keyboard, Silk.NET.Input.Key key, int scancode)
    {
        var translatedKey = TranslateKey(key);
        if (translatedKey is not null)
        {
            ProcessPlatformKeyDown(translatedKey.Value, scancode);
        }
        else if (scancode == 270)
        {
            ProcessPlatformKeyDown(Key.Back, scancode);
        }
    }

    private static void KeyUpHandler(IKeyboard keyboard, Silk.NET.Input.Key key, int scancode)
    {
        var translatedKey = TranslateKey(key);
        if (translatedKey is not null)
        {
            ProcessPlatformKeyUp(translatedKey.Value, scancode);
        }
        else if (scancode == 270)
        {
            ProcessPlatformKeyUp(Key.Back, scancode);
        }
    }

    private static void KeyPressHandler(IKeyboard keyboard, char c)
    {
        ProcessPlatformCharacter(c);
    }

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
}
#endif

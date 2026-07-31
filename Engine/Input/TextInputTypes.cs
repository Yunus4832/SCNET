namespace Engine.Input;

public enum TextInputStyle
{
    Inline,
    NativeDialog
}

[Flags]
public enum TextInputModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    CapsLock = 8
}

public readonly record struct TextInputOptions(
    string Title,
    string Description,
    string InitialText,
    bool PasswordMode = false);

public readonly record struct TextComposition(
    string Text,
    int CaretPosition,
    int SelectionLength = 0)
{
    public static TextComposition Empty { get; } = new(string.Empty, 0);
}

public readonly record struct TextInputRectangle(int X, int Y, int Width, int Height);

public readonly record struct TextInputKeyEvent(
    Key Key,
    int ScanCode,
    bool IsRelease,
    TextInputModifiers Modifiers);

using System.Text;

using Engine.Core;

using Silk.NET.Maths;
using Silk.NET.SDL;

namespace Engine.Input;

public sealed unsafe class SdlTextInputBackend(bool processEditingKeyEvents = false) : ITextInputBackend
{
    private const int _sdlBackspaceKeycode = '\b';

    private readonly Lock _stateLock = new();

    private Sdl? _sdl;

    private PfnEventFilter _eventFilter;

    private ITextInputSink? _sink;

    private bool _active;

    private bool _windowFocused = true;

    private bool _isComposing;

    private TextInputRectangle? _pendingCursorRectangle;

    public bool IsAvailable { get; private set; }

    public bool SuppressDirectText => IsAvailable && _active && _windowFocused;

    public void Initialize()
    {
        var sdl = Sdl.GetApi();
        if (sdl.WasInit(Sdl.InitVideo) == 0)
        {
            sdl.Dispose();
            Log.Warning("SDL text input is unavailable because the video subsystem is not initialized.");
            return;
        }

        _sdl = sdl;
        _eventFilter = new PfnEventFilter(OnSdlEvent);
        sdl.AddEventWatch(_eventFilter, null);
        IsAvailable = true;
        Log.Information($"SDL text input connected through '{sdl.GetCurrentVideoDriverS()}'.");
    }

    public void BeginInput(ITextInputSink sink)
    {
        lock (_stateLock)
        {
            _sink = sink;
            _active = true;
            _isComposing = false;
        }

        if (IsAvailable && _windowFocused)
        {
            if (_pendingCursorRectangle is { } rectangle)
            {
                ApplyCursorRectangle(rectangle);
            }

            StartTextInput();
        }
    }

    public void EndInput()
    {
        ITextInputSink? sink;
        lock (_stateLock)
        {
            sink = _sink;
            _sink = null;
            _active = false;
            _isComposing = false;
        }

        sink?.UpdateComposition(TextComposition.Empty);
        if (IsAvailable)
        {
            _sdl!.StopTextInput();
        }
    }

    public void SetCursorRectangle(TextInputRectangle rectangle)
    {
        _pendingCursorRectangle = rectangle;
        if (!IsAvailable || !_active)
        {
            return;
        }

        ApplyCursorRectangle(rectangle);
    }

    private void ApplyCursorRectangle(TextInputRectangle rectangle)
    {
        var sdlRectangle = new Rectangle<int>(
            rectangle.X,
            rectangle.Y,
            Math.Max(rectangle.Width, 1),
            Math.Max(rectangle.Height, 1));
        _sdl!.SetTextInputRect(in sdlRectangle);
    }

    public bool ProcessKey(TextInputKeyEvent keyEvent)
    {
        if (!IsAvailable ||
            !_active ||
            keyEvent.Key is Key.Shift or Key.Control or Key.Alt ||
            (keyEvent.Modifiers & (TextInputModifiers.Control | TextInputModifiers.Alt)) != 0)
        {
            return false;
        }

        return _isComposing || IsTextKey(keyEvent.Key);
    }

    public void Update()
    {
    }

    public void OnWindowFocusChanged(bool focused)
    {
        _windowFocused = focused;
        if (!IsAvailable)
        {
            return;
        }

        if (focused && _active)
        {
            StartTextInput();
        }
        else
        {
            _sdl!.StopTextInput();
        }
    }

    public void Dispose()
    {
        EndInput();
        var sdl = _sdl;
        _sdl = null;
        if (sdl is not null)
        {
            sdl.DelEventWatch(_eventFilter, null);
            _eventFilter.Dispose();
            sdl.Dispose();
        }

        IsAvailable = false;
    }

    private void StartTextInput()
    {
        _sdl!.StartTextInput();
    }

    private int OnSdlEvent(void* _, Event* sdlEvent)
    {
        if (!IsAvailable || !_active || !_windowFocused || sdlEvent is null)
        {
            return 1;
        }

        switch ((EventType)sdlEvent->Type)
        {
            case EventType.Keydown when
                processEditingKeyEvents &&
                sdlEvent->Key.Keysym.Sym == _sdlBackspaceKeycode:
                Backspace();
                break;
            case EventType.Textinput:
                CommitText(ReadUtf8(sdlEvent->Text.Text, 32));
                break;
            case EventType.Textediting:
                UpdateComposition(
                    ReadUtf8(sdlEvent->Edit.Text, 32),
                    sdlEvent->Edit.Start,
                    sdlEvent->Edit.Length);
                break;
            case EventType.TexteditingExt:
                UpdateComposition(
                    ReadUtf8(sdlEvent->EditExt.Text),
                    sdlEvent->EditExt.Start,
                    sdlEvent->EditExt.Length);
                break;
        }

        return 1;
    }

    private void Backspace()
    {
        ITextInputSink? sink;
        lock (_stateLock)
        {
            sink = _isComposing ? null : _sink;
        }

        sink?.Backspace();
    }

    private void CommitText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ITextInputSink? sink;
        lock (_stateLock)
        {
            sink = _sink;
            _isComposing = false;
        }

        sink?.UpdateComposition(TextComposition.Empty);
        sink?.CommitText(text);
    }

    private void UpdateComposition(string text, int caretPosition, int selectionLength)
    {
        ITextInputSink? sink;
        lock (_stateLock)
        {
            sink = _sink;
            _isComposing = !string.IsNullOrEmpty(text);
        }

        sink?.UpdateComposition(
            new TextComposition(
                text,
                MathUtils.Clamp(caretPosition, 0, text.Length),
                MathUtils.Clamp(selectionLength, 0, text.Length)));
    }

    private static string ReadUtf8(byte* bytes, int maximumLength = int.MaxValue)
    {
        if (bytes is null)
        {
            return string.Empty;
        }

        var length = 0;
        while (length < maximumLength && bytes[length] != 0)
        {
            length++;
        }

        return length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes, length);
    }

    private static bool IsTextKey(Key key)
    {
        return key is
            (>= Key.A and <= Key.Z) or
            (>= Key.Number0 and <= Key.Number9) or
            Key.Space or
            Key.Tilde or
            Key.Minus or
            Key.Plus or
            Key.LeftBracket or
            Key.RightBracket or
            Key.Semicolon or
            Key.Quote or
            Key.Comma or
            Key.Period or
            Key.Slash or
            Key.BackSlash;
    }
}

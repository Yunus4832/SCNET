using System.Collections.Concurrent;

using Engine.Core;

namespace Engine.Input;

public static class TextInputManager
{
    private sealed class SessionSink(TextInputSession session) : ITextInputSink
    {
        public void CommitText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _pendingActions.Enqueue(() => session.CommitText(text));
            }
        }

        public void Backspace()
        {
            _pendingActions.Enqueue(session.Backspace);
        }

        public void UpdateComposition(TextComposition composition)
        {
            var text = composition.Text ?? string.Empty;
            var caret = MathUtils.Clamp(composition.CaretPosition, 0, text.Length);
            var selectionLength = MathUtils.Clamp(composition.SelectionLength, 0, text.Length - caret);
            var normalized = new TextComposition(text, caret, selectionLength);
            _pendingActions.Enqueue(() => session.UpdateComposition(normalized));
        }

    }

    private static readonly ConcurrentQueue<Action> _pendingActions = [];

    private static ITextInputBackend _backend = CreateDefaultBackend();

    private static TextInputSession? _activeSession;

    private static TextInputRectangle? _cursorRectangle;

    private static bool _isInitialized;

    public static bool SuppressDirectText =>
        _activeSession is { IsDisposed: false } && _backend.SuppressDirectText;

    public static void RegisterBackend(ITextInputBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        if (_isInitialized)
        {
            throw new InvalidOperationException("Text input backend must be registered before the window is created.");
        }

        _backend.Dispose();
        _backend = backend;
    }

    public static TextInputSession BeginInput(
        Action<string>? commitText = null,
        Action? backspace = null,
        Action<TextComposition>? updateComposition = null,
        TextInputRectangle? initialRectangle = null)
    {
        _activeSession?.Dispose();

        var session = new TextInputSession(
            commitText ?? delegate { },
            backspace ?? delegate { },
            updateComposition ?? delegate { });
        _activeSession = session;
        _cursorRectangle = initialRectangle;

        try
        {
            if (initialRectangle is { } rectangle)
            {
                TryInvokeBackend(() => _backend.SetCursorRectangle(rectangle));
            }

            _backend.BeginInput(new SessionSink(session));
        }
        catch (Exception ex)
        {
            Log.Warning($"Text input backend failed to begin input: {ex}");
            session.Dispose();
        }

        return session;
    }

    public static void SetCursorRectangle(TextInputRectangle rectangle)
    {
        if (_activeSession is null || _cursorRectangle == rectangle)
        {
            return;
        }

        _cursorRectangle = rectangle;
        TryInvokeBackend(() => _backend.SetCursorRectangle(rectangle));
    }

    public static bool ProcessKey(TextInputKeyEvent keyEvent)
    {
        if (_activeSession is null)
        {
            return false;
        }

        try
        {
            return _backend.ProcessKey(keyEvent);
        }
        catch (Exception ex)
        {
            Log.Warning($"Text input backend failed to process a key: {ex}");
            return false;
        }
    }

    internal static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            _backend.Initialize();
            _isInitialized = true;
            Log.Information($"Text input backend initialized: {_backend.GetType().Name}, available={_backend.IsAvailable}.");
        }
        catch (Exception ex)
        {
            Log.Warning($"Text input backend initialization failed, direct keyboard input will be used: {ex}");
            _backend.Dispose();
            _backend = new NullTextInputBackend();
            _backend.Initialize();
            _isInitialized = true;
        }
    }

    internal static void BeforeFrame()
    {
        TryInvokeBackend(_backend.Update);
        while (_pendingActions.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error($"Text input callback failed: {ex}");
            }
        }
    }

    internal static void OnWindowFocusChanged(bool focused)
    {
        TryInvokeBackend(() => _backend.OnWindowFocusChanged(focused));
    }

    internal static void EndInput(TextInputSession session)
    {
        if (!ReferenceEquals(_activeSession, session))
        {
            return;
        }

        _activeSession = null;
        _cursorRectangle = null;
        TryInvokeBackend(_backend.EndInput);
    }

    internal static void Dispose()
    {
        _activeSession?.Dispose();
        _activeSession = null;
        while (_pendingActions.TryDequeue(out _))
        {
        }

        _backend.Dispose();
        _backend = CreateDefaultBackend();
        _cursorRectangle = null;
        _isInitialized = false;
    }

    private static ITextInputBackend CreateDefaultBackend()
    {
        return new NullTextInputBackend();
    }

    private static void TryInvokeBackend(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Warning($"Text input backend operation failed: {ex}");
        }
    }
}

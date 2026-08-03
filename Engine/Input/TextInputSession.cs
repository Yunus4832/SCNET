namespace Engine.Input;

public sealed class TextInputSession : IDisposable
{
    private readonly Action<string> _commitText;

    private readonly Action _backspace;

    private readonly Action<TextComposition> _updateComposition;

    private bool _isDisposed;

    internal TextInputSession(
        Action<string> commitText,
        Action backspace,
        Action<TextComposition> updateComposition)
    {
        _commitText = commitText;
        _backspace = backspace;
        _updateComposition = updateComposition;
    }

    public bool IsDisposed => _isDisposed;

    internal void CommitText(string text)
    {
        if (!_isDisposed)
        {
            _commitText(text);
        }
    }

    internal void Backspace()
    {
        if (!_isDisposed)
        {
            _backspace();
        }
    }

    internal void UpdateComposition(TextComposition composition)
    {
        if (!_isDisposed)
        {
            _updateComposition(composition);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        TextInputManager.EndInput(this);
    }
}

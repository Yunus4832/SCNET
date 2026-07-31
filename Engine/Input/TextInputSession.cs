namespace Engine.Input;

public sealed class TextInputSession : IDisposable
{
    private readonly Action<string> _commitText;

    private readonly Action<TextComposition> _updateComposition;

    private readonly Action<string> _complete;

    private readonly Action _cancel;

    private bool _isDisposed;

    internal TextInputSession(
        TextInputStyle inputStyle,
        Action<string> commitText,
        Action<TextComposition> updateComposition,
        Action<string> complete,
        Action cancel)
    {
        InputStyle = inputStyle;
        _commitText = commitText;
        _updateComposition = updateComposition;
        _complete = complete;
        _cancel = cancel;
    }

    public TextInputStyle InputStyle { get; }

    public bool IsDisposed => _isDisposed;

    internal void CommitText(string text)
    {
        if (!_isDisposed)
        {
            _commitText(text);
        }
    }

    internal void UpdateComposition(TextComposition composition)
    {
        if (!_isDisposed)
        {
            _updateComposition(composition);
        }
    }

    internal void Complete(string text)
    {
        if (!_isDisposed)
        {
            _complete(text);
        }
    }

    internal void Cancel()
    {
        if (!_isDisposed)
        {
            _cancel();
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

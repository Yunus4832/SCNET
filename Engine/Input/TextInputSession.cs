namespace Engine.Input;

public sealed class TextInputSession : IDisposable
{
    private readonly Action<string> _commitText;

    private readonly Action<TextComposition> _updateComposition;

    private bool _isDisposed;

    internal TextInputSession(
        Action<string> commitText,
        Action<TextComposition> updateComposition)
    {
        _commitText = commitText;
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

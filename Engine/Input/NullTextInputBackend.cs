namespace Engine.Input;

internal sealed class NullTextInputBackend : ITextInputBackend
{
    public TextInputStyle InputStyle => TextInputStyle.Inline;

    public bool IsAvailable => false;

    public bool SuppressDirectText => false;

    public void Initialize()
    {
    }

    public void BeginInput(TextInputOptions options, ITextInputSink sink)
    {
    }

    public void EndInput()
    {
    }

    public void SetCursorRectangle(TextInputRectangle rectangle)
    {
    }

    public bool ProcessKey(TextInputKeyEvent keyEvent) => false;

    public void Update()
    {
    }

    public void OnWindowFocusChanged(bool focused)
    {
    }

    public void Dispose()
    {
    }
}

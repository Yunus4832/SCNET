namespace Engine.Input;

public interface ITextInputBackend : IDisposable
{
    bool IsAvailable { get; }

    bool SuppressDirectText { get; }

    void Initialize();

    void BeginInput(ITextInputSink sink);

    void EndInput();

    void SetCursorRectangle(TextInputRectangle rectangle);

    bool ProcessKey(TextInputKeyEvent keyEvent);

    void Update();

    void OnWindowFocusChanged(bool focused);
}

public interface ITextInputSink
{
    void CommitText(string text);

    void UpdateComposition(TextComposition composition);
}

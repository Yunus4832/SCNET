namespace Engine.Input;

public interface ITextInputBackend : IDisposable
{
    TextInputStyle InputStyle { get; }

    bool IsAvailable { get; }

    bool SuppressDirectText { get; }

    void Initialize();

    void BeginInput(TextInputOptions options, ITextInputSink sink);

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

    void Complete(string text);

    void Cancel();
}

using Engine.Input;

namespace Engine.Test.Input;

[Collection(nameof(TextInputManagerCollection))]
public class TextInputManagerTest : IDisposable
{
    private sealed class TestBackend(TextInputStyle inputStyle, bool suppressDirectText) : ITextInputBackend
    {
        public ITextInputSink? Sink { get; private set; }

        public int EndInputCount { get; private set; }

        public TextInputStyle InputStyle => inputStyle;

        public bool IsAvailable => true;

        public bool SuppressDirectText => suppressDirectText;

        public void Initialize()
        {
        }

        public void BeginInput(TextInputOptions options, ITextInputSink sink)
        {
            Sink = sink;
        }

        public void EndInput()
        {
            EndInputCount++;
            Sink = null;
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

    public TextInputManagerTest()
    {
        TextInputManager.Dispose();
    }

    [Fact]
    public void BackendCallbacksAreDeliveredOnBeforeFrame()
    {
        var backend = new TestBackend(TextInputStyle.Inline, true);
        TextInputManager.RegisterBackend(backend);
        TextInputManager.Initialize();

        var committed = string.Empty;
        var composition = TextComposition.Empty;
        using var session = TextInputManager.BeginInput(
            new TextInputOptions(string.Empty, string.Empty, string.Empty),
            commitText: text => committed = text,
            updateComposition: value => composition = value);

        backend.Sink!.CommitText("中文");
        backend.Sink.UpdateComposition(new TextComposition("拼音", 100, 100));

        Assert.Equal(string.Empty, committed);
        Assert.Equal(TextComposition.Empty, composition);

        TextInputManager.BeforeFrame();

        Assert.Equal("中文", committed);
        Assert.Equal(new TextComposition("拼音", 2, 0), composition);
        Assert.True(TextInputManager.SuppressDirectText);
    }

    [Fact]
    public void CompletingNativeDialogEndsSession()
    {
        var backend = new TestBackend(TextInputStyle.NativeDialog, true);
        TextInputManager.RegisterBackend(backend);
        TextInputManager.Initialize();

        var completed = string.Empty;
        TextInputManager.BeginInput(
            new TextInputOptions(string.Empty, string.Empty, "old"),
            complete: text => completed = text);

        Assert.True(TextInputManager.IsNativeDialogVisible);
        backend.Sink!.Complete("new");

        TextInputManager.BeforeFrame();

        Assert.Equal("new", completed);
        Assert.False(TextInputManager.HasActiveSession);
        Assert.False(TextInputManager.IsNativeDialogVisible);
        Assert.Equal(1, backend.EndInputCount);
    }

    [Fact]
    public void StartingNewSessionEndsPreviousSession()
    {
        var backend = new TestBackend(TextInputStyle.Inline, false);
        TextInputManager.RegisterBackend(backend);
        TextInputManager.Initialize();

        var first = TextInputManager.BeginInput(
            new TextInputOptions(string.Empty, string.Empty, string.Empty));
        using var second = TextInputManager.BeginInput(
            new TextInputOptions(string.Empty, string.Empty, string.Empty));

        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.Equal(1, backend.EndInputCount);
    }

    public void Dispose()
    {
        TextInputManager.Dispose();
    }
}

[CollectionDefinition(nameof(TextInputManagerCollection), DisableParallelization = true)]
public sealed class TextInputManagerCollection;

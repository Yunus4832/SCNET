using Engine.Input;

namespace Engine.Test.Input;

[Collection(nameof(TextInputManagerCollection))]
public class TextInputManagerTest : IDisposable
{
    private sealed class TestBackend(bool suppressDirectText) : ITextInputBackend
    {
        public ITextInputSink? Sink { get; private set; }

        public int EndInputCount { get; private set; }

        public bool IsAvailable => true;

        public bool SuppressDirectText => suppressDirectText;

        public void Initialize()
        {
        }

        public void BeginInput(ITextInputSink sink)
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
        var backend = new TestBackend(true);
        TextInputManager.RegisterBackend(backend);
        TextInputManager.Initialize();

        var committed = string.Empty;
        var composition = TextComposition.Empty;
        using var session = TextInputManager.BeginInput(
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
    public void StartingNewSessionEndsPreviousSession()
    {
        var backend = new TestBackend(false);
        TextInputManager.RegisterBackend(backend);
        TextInputManager.Initialize();

        var first = TextInputManager.BeginInput();
        using var second = TextInputManager.BeginInput();

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

using Engine.Graphics;
using Engine.Input;
using Engine.Media;

namespace Game.Widgets;

public class TextBoxWidget : Widget
{
    public Func<string, int, float, Vector2, float>? CalculateCharacterPosition;

    public Func<int, string, string>? DeleteOneText;

    private double _focusStartTime;

    private bool _hasFocus;

    private float _scroll;

    private Vector2? _size;

    private string _text = string.Empty;

    private TextInputSession? _textInputSession;

    private string _compositionText = string.Empty;

    private int _compositionCaretPosition;


    public bool MoveNextFlag;

    public Vector2 Size
    {
        get => _size ?? Vector2.Zero;
        set => _size = value;
    }

    public string Text
    {
        get => _text;
        set
        {
            var text = value.Length > MaximumLength ? value[..MaximumLength] : value;
            if (text == _text)
            {
                return;
            }

            _text = text;
            CaretPosition = CaretPosition;
            TextChanged?.Invoke(this);
        }
    }

    public int MaximumLength
    {
        get;
        set
        {
            field = MathUtils.Max(value, 0);
            if (Text.Length > field)
            {
                Text = Text.Substring(0, field);
            }
        }
    } = 512;

    public bool OverwriteMode { get; set; }

    public bool HasFocus
    {
        get => _hasFocus;
        set
        {
            if (value == _hasFocus)
            {
                return;
            }

            _hasFocus = value;
            if (value)
            {
                BeginTextInput();
            }
            else
            {
                EndTextInput();
            }
        }
    }

    public BitmapFont Font { get; set; }

    public float FontScale { get; set; }

    public Vector2 FontSpacing { get; set; }

    public Color Color { get; set; }

    public bool HideText { get; set; }

    public bool HasDisplayText => _text.Length > 0 || _compositionText.Length > 0;

    public bool TextureLinearFilter { get; set; }

    public int CaretPosition
    {
        get;
        set
        {
            field = MathUtils.Clamp(value, 0, Text.Length);
            _focusStartTime = Time.RealTime;
        }
    }

    public event Action<int, string>? MoveCursor;

    public event Action<TextBoxWidget>? TextChanged;

    public event Action<TextBoxWidget>? Enter;

    public event Action<TextBoxWidget>? Escape;

    public event Action<TextBoxWidget>? FocusLost;


    public TextBoxWidget()
    {
        ClampToBounds = true;
        Color = Color.White;
        TextureLinearFilter = true;
        Font = ContentManager.Get<BitmapFont>("Fonts/Pericles");
        FontScale = 1f;
    }

    private void BeginTextInput()
    {
        ClearPendingTextInput();
        CaretPosition = _text.Length;
        _textInputSession?.Dispose();
        _textInputSession = TextInputManager.BeginInput(
            commitText: text =>
            {
                if (HasFocus)
                {
                    ClearComposition();
                    EnterText(text);
                }
            },
            backspace: () =>
            {
                if (HasFocus)
                {
                    ClearComposition();
                    DeleteCharacterBeforeCaret();
                }
            },
            updateComposition: composition =>
            {
                if (HasFocus)
                {
                    _compositionText = composition.Text;
                    _compositionCaretPosition = composition.CaretPosition;
                    _focusStartTime = Time.RealTime;
                }
            });
    }

    private void EndTextInput()
    {
        _textInputSession?.Dispose();
        _textInputSession = null;
        ClearComposition();
        FocusLost?.Invoke(this);
    }

    private static void ClearPendingTextInput()
    {
        KeyboardInput.GetInput();
    }

    public override void Update()
    {
        if (HasFocus)
        {
            HandleBackspace();
            var inputString = ReadTextInput();
            if (!string.IsNullOrEmpty(inputString))
            {
                EnterText(inputString);
            }
        }

        // 处理电脑键盘输入时会处理成游戏输入
        if (Input.Click.HasValue)
        {
            HasFocus = HitTestGlobal(Input.Click.Value.Start) == this && HitTestGlobal(Input.Click.Value.End) == this;
        }

        if (!HasFocus)
        {
            return;
        }

        // 处理复制粘贴事件
        if (Input.IsKeyDown(Key.Control))
        {
            if (Input.IsKeyDownOnce(Key.V))
            {
                EnterText(ClipboardManager.ClipboardString);
            }
            else if (Input.IsKeyDownOnce(Key.C))
            {
                ClipboardManager.ClipboardString = Text;
            }
            else if (Input.IsKeyDownOnce(Key.X))
            {
                ClipboardManager.ClipboardString = Text;
                Text = string.Empty;
            }
        }

        if (Input.IsKeyDownOnce(Key.Tab))
        {
            MoveNext(ScreensManager.CurrentScreen!.Children);
        }

        if (Input.IsKeyDownRepeat(Key.LeftArrow))
        {
            CaretPosition = MathUtils.Max(0, --CaretPosition);
            MoveCursor?.Invoke(CaretPosition, Text);
        }

        if (Input.IsKeyDownRepeat(Key.RightArrow))
        {
            CaretPosition = MathUtils.Min(Text.Length, ++CaretPosition);
            MoveCursor?.Invoke(CaretPosition, Text);
        }

        if (IsDeletePressed())
        {
            DeleteCharacterAtCaret();
        }

        if (Input.IsKeyDownOnce(Key.Home))
        {
            CaretPosition = 0;
        }

        if (Input.IsKeyDownOnce(Key.End))
        {
            CaretPosition = Text.Length;
        }

        if (Input.IsKeyDownRepeat(Key.UpArrow))
        {
            CaretPosition = 0;
        }

        if (Input.IsKeyDownRepeat(Key.DownArrow))
        {
            CaretPosition = Text.Length;
        }

        if (Input.IsKeyDownRepeat(Key.Enter))
        {
            SubmitText();
        }

        if (Input.IsKeyDownRepeat(Key.Escape))
        {
            CancelText();
        }
    }

    private string ReadTextInput()
    {
        var input = KeyboardInput.GetInput();
        return Input.IsKeyDown(Key.Control) ? string.Empty : input;
    }

    private void HandleBackspace()
    {
        if (!IsBackspacePressed())
        {
            return;
        }

        DeleteCharacterBeforeCaret();
    }

    private bool IsBackspacePressed()
    {
        return KeyboardInput.BackspacePressed || Input.IsKeyDownRepeat(Key.BackSpace);
    }

    private bool IsDeletePressed()
    {
        return KeyboardInput.DeletePressed || Input.IsKeyDownRepeat(Key.Delete);
    }

    private void DeleteCharacterBeforeCaret()
    {
        if (CaretPosition == 0)
        {
            return;
        }

        CaretPosition--;
        if (Text.Length > 0)
        {
            Text = DeleteOneText != null
                ? DeleteOneText(CaretPosition, Text)
                : Text.Remove(CaretPosition, 1);
        }

        var num = Font.CalculateCharacterPosition(Text, 0, new Vector2(FontScale), FontSpacing);
        _scroll = MathUtils.Max(0, num - ActualSize.X);
    }

    private void DeleteCharacterAtCaret()
    {
        if (CaretPosition >= Text.Length)
        {
            return;
        }

        Text = Text.Remove(CaretPosition, 1);
    }

    private void SubmitText()
    {
        Enter?.Invoke(this);
    }

    private void CancelText()
    {
        Escape?.Invoke(this);
    }

    public void MoveNext(WidgetsList widgets)
    {
        foreach (var widget in widgets)
        {
            if (widget is TextBoxWidget textBox)
            {
                if (!MoveNextFlag && textBox == this)
                {
                    MoveNextFlag = true;
                }
                else if (MoveNextFlag)
                {
                    textBox.HasFocus = true;
                    HasFocus = false;
                    MoveNextFlag = false;
                }
            }

            if (widget is ContainerWidget container)
            {
                MoveNext(container.Children);
            }
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
        if (_size.HasValue)
        {
            DesiredSize = _size.Value;
            return;
        }

        var displayText = GetDisplayText();
        DesiredSize = Font.MeasureText(displayText.Length == 0 ? " " : displayText, new Vector2(FontScale), FontSpacing);

        DesiredSize += new Vector2(1f * FontScale * Font.Scale, 0f);
    }

    public override void Draw(DrawContext dc)
    {
        var color = Color * GlobalColorTransform;
        var displayText = GetDisplayText();
        if (!string.IsNullOrEmpty(displayText) && !HideText)
        {
            var position = new Vector2(0f - _scroll, ActualSize.Y / 2f);
            var samplerState = TextureLinearFilter ? SamplerState.LinearClamp : SamplerState.PointClamp;
            var fontBatch2D =
                dc.PrimitivesRenderer2D.FontBatch(Font, 1, DepthStencilState.None, null, null, samplerState);
            var count = fontBatch2D.TriangleVertices.Count;
            fontBatch2D.QueueText(displayText, position, 0f, color, TextAnchor.VerticalCenter, new Vector2(FontScale),
                FontSpacing);
            fontBatch2D.TransformTriangles(GlobalTransform, count);

            DrawCompositionUnderline(dc, color, displayText);
        }

        if (!_hasFocus)
        {
            return;
        }

        var displayCaretPosition = CaretPosition + _compositionCaretPosition;
        var num = CalculateCharacterPosition?.Invoke(
                      displayText,
                      displayCaretPosition,
                      FontScale,
                      FontSpacing) ??
                  Font.CalculateCharacterPosition(
                      displayText,
                      displayCaretPosition,
                      new Vector2(FontScale),
                      FontSpacing);

        var v = new Vector2(0f, ActualSize.Y / 2f) + new Vector2(num - _scroll, 0f);

        if (v.X < 0f)
        {
            _scroll = MathUtils.Max(_scroll + v.X, 0f);
        }

        if (v.X > ActualSize.X)
        {
            _scroll += v.X - ActualSize.X + 1f;
        }

        UpdateTextInputRectangle(num);
        if (!(MathUtils.Remainder(Time.RealTime - _focusStartTime, 0.5) < 0.25))
        {
            return;
        }

        var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(1, DepthStencilState.None);
        var count2 = flatBatch2D.TriangleVertices.Count;
        flatBatch2D.QueueQuad(v - new Vector2(0f, Font.GlyphHeight / 2f * FontScale * Font.Scale),
            v + new Vector2(1f, Font.GlyphHeight / 2f * FontScale * Font.Scale), 0f, color);
        flatBatch2D.TransformTriangles(GlobalTransform, count2);
    }

    public void EnterText(string s)
    {
        if (OverwriteMode)
        {
            if (CaretPosition + s.Length > MaximumLength)
            {
                return;
            }

            if (CaretPosition < _text.Length)
            {
                var text = Text;
                text = text.Remove(CaretPosition, s.Length);
                Text = text.Insert(CaretPosition, s);
            }
            else
            {
                Text = _text + s;
            }

            CaretPosition += s.Length;
        }
        else if (_text.Length + s.Length <= MaximumLength)
        {
            if (CaretPosition < _text.Length)
            {
                Text = Text.Insert(CaretPosition, s);
            }
            else
            {
                Text = _text + s;
            }

            CaretPosition += s.Length;
        }
    }

    private string GetDisplayText()
    {
        return string.IsNullOrEmpty(_compositionText)
            ? Text
            : Text.Insert(CaretPosition, _compositionText);
    }

    private void ClearComposition()
    {
        _compositionText = string.Empty;
        _compositionCaretPosition = 0;
    }

    private void DrawCompositionUnderline(DrawContext dc, Color color, string displayText)
    {
        if (string.IsNullOrEmpty(_compositionText))
        {
            return;
        }

        var start = Font.CalculateCharacterPosition(
            displayText,
            CaretPosition,
            new Vector2(FontScale),
            FontSpacing);
        var end = Font.CalculateCharacterPosition(
            displayText,
            CaretPosition + _compositionText.Length,
            new Vector2(FontScale),
            FontSpacing);
        var y = ActualSize.Y / 2f + Font.GlyphHeight * FontScale * Font.Scale / 2f;
        var flatBatch = dc.PrimitivesRenderer2D.FlatBatch(1, DepthStencilState.None);
        var count = flatBatch.LineVertices.Count;
        flatBatch.QueueLine(
            new Vector2(start - _scroll, y),
            new Vector2(end - _scroll, y),
            0f,
            color);
        flatBatch.TransformLines(GlobalTransform, count);
    }

    private void UpdateTextInputRectangle(float caretX)
    {
        if (!_hasFocus || _textInputSession is null)
        {
            return;
        }

        var localPosition = new Vector2(
            caretX - _scroll,
            ActualSize.Y / 2f + Font.GlyphHeight * FontScale * Font.Scale / 2f);
        var screenPosition = WidgetToScreen(localPosition);
        var windowScale = MathUtils.Max(Window.Scale, 0.0001f);
        TextInputManager.SetCursorRectangle(
            new TextInputRectangle(
                (int)MathF.Round(screenPosition.X / windowScale),
                (int)MathF.Round(screenPosition.Y / windowScale),
                1,
                Math.Max(1, (int)MathF.Round(Font.GlyphHeight * FontScale * Font.Scale / windowScale))));
    }
}

using Engine.Graphics;
using Engine.Input;
using Engine.Media;

namespace Game.Widgets;

public class TextBoxWidget : Widget
{
    public Func<string, int, float, Vector2, float>? CalculateCharacterPosition;

    public Func<int, string, string>? DeleteOneText;

    public bool JustOpened;

    private double _focusStartTime;

    private bool _hasFocus;

    private float _scroll;

    private Vector2? _size;

    private string _text = string.Empty;


    public bool MoveNextFlag;

    public Vector2 Size
    {
        get => _size ?? Vector2.Zero;
        set => _size = value;
    }

    public override string Title { get; set; } = string.Empty;

    public string Description { get; set; }

    public string Text
    {
        get => _text;
        set
        {
            var text = value.Length > MaximumLength ? value.Substring(0, MaximumLength) : value;
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
        get { return _hasFocus; }
        set
        {
            if (value == _hasFocus)
            {
                return;
            }

            _hasFocus = value;
            if (value)
            {
#if DESKTOP
                if (_hasFocus && Text == string.Empty)
                    //清空之前的输入
                {
                    KeyboardInput.GetInput();
                }
#endif
                CaretPosition = _text.Length;
                Keyboard.ShowKeyboard(
                    Title,
                    Description,
                    Text,
                    false,
                    delegate(string text) { Text = text; },
                    null
                );
            }
            else
            {
                FocusLost?.Invoke(this);
            }
        }
    }

    public BitmapFont Font { get; set; }

    public float FontScale { get; set; }

    public Vector2 FontSpacing { get; set; }

    public Color Color { get; set; }

    public bool HideText { get; set; }

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
        Description = string.Empty;
        JustOpened = true;
    }

    public override void Update()
    {
        if (HasFocus)
        {
#if ANDROID
            if (Input.LastChar.HasValue && !Input.IsKeyDown(Key.Control) && !char.IsControl(Input.LastChar.Value))
            {
                EnterText(new string(Input.LastChar.Value, 1));
                Input.Clear();
            }

            if (Input.LastKey.HasValue)
            {
                var flag = false;
                var value = Input.LastKey.Value;
                if (value == Key.V && Input.IsKeyDown(Key.Control))
                {
                    EnterText(ClipboardManager.ClipboardString);
                    flag = true;
                }
                else if (value == Key.BackSpace && CaretPosition > 0)
                {
                    CaretPosition--;
                    Text = Text.Remove(CaretPosition, 1);
                    flag = true;
                }
                else
                {
                    switch (value)
                    {
                        case Key.Delete:
                            if (CaretPosition < _text.Length)
                            {
                                Text = Text.Remove(CaretPosition, 1);
                                flag = true;
                            }

                            break;
                        case Key.LeftArrow:
                            CaretPosition--;
                            flag = true;
                            break;
                        case Key.RightArrow:
                            CaretPosition++;
                            flag = true;
                            break;
                        case Key.Home:
                            CaretPosition = 0;
                            flag = true;
                            break;
                        case Key.End:
                            CaretPosition = _text.Length;
                            flag = true;
                            break;
                        case Key.Enter:
                            flag = true;
                            HasFocus = false;
                            Enter?.Invoke(this);
                            break;
                        case Key.Escape:
                            flag = true;
                            HasFocus = false;
                            Escape?.Invoke(this);
                            break;
                    }
                }

                if (flag)
                {
                    Input.Clear();
                }
            }
#else
            //处理文字删除
            if (KeyboardInput.DeletePressed)
            {
                if (CaretPosition != 0)
                {
                    CaretPosition--;
                    CaretPosition = Math.Max(0, CaretPosition);
                    if (Text.Length > 0)
                    {
                        Text = DeleteOneText != null
                            ? DeleteOneText(CaretPosition, Text)
                            : Text.Remove(CaretPosition, 1);
                    }

                    var num = Font.CalculateCharacterPosition(Text, 0, new Vector2(FontScale), FontSpacing);
                    _scroll = num - ActualSize.X;
                    _scroll = MathUtils.Max(0, _scroll);
                }
            }

            //处理文字输入
            var inputString = KeyboardInput.GetInput();
            if (JustOpened)
            {
                inputString = string.Empty;
                JustOpened = false;
            }

            if (!string.IsNullOrEmpty(inputString))
            {
                EnterText(inputString);
            }
#endif
        }

        if (Input.Click.HasValue)
            //处理电脑键盘输入时会处理成游戏输入
        {
            HasFocus = HitTestGlobal(Input.Click.Value.Start) == this && HitTestGlobal(Input.Click.Value.End) == this;
        }

        if (!HasFocus)
        {
            return;
        }

        //处理复制粘贴事件
        if (Input.IsKeyDown(Key.Control))
        {
            if (Input.IsKeyDownOnce(Key.V))
            {
                Text += ClipboardManager.ClipboardString;
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
            Enter?.Invoke(this);
        }

        if (Input.IsKeyDownRepeat(Key.Escape))
        {
            Escape?.Invoke(this);
        }
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

        DesiredSize = Font.MeasureText(Text.Length == 0 ? " " : Text, new Vector2(FontScale), FontSpacing);

        DesiredSize += new Vector2(1f * FontScale * Font.Scale, 0f);
    }

    public override void Draw(DrawContext dc)
    {
        var color = Color * GlobalColorTransform;
        if (!string.IsNullOrEmpty(_text) && !HideText)
        {
            var position = new Vector2(0f - _scroll, ActualSize.Y / 2f);
            var samplerState = TextureLinearFilter ? SamplerState.LinearClamp : SamplerState.PointClamp;
            var fontBatch2D =
                dc.PrimitivesRenderer2D.FontBatch(Font, 1, DepthStencilState.None, null, null, samplerState);
            var count = fontBatch2D.TriangleVertices.Count;
            fontBatch2D.QueueText(Text, position, 0f, color, TextAnchor.VerticalCenter, new Vector2(FontScale),
                FontSpacing);
            fontBatch2D.TransformTriangles(GlobalTransform, count);
        }

        if (!_hasFocus || !(MathUtils.Remainder(Time.RealTime - _focusStartTime, 0.5) < 0.25))
        {
            return;
        }

        var num = CalculateCharacterPosition?.Invoke(Text, CaretPosition, FontScale, FontSpacing) ??
                  Font.CalculateCharacterPosition(Text, CaretPosition, new Vector2(FontScale), FontSpacing);

        var v = new Vector2(0f, ActualSize.Y / 2f) + new Vector2(num - _scroll, 0f);

        if (_hasFocus)
        {
            if (v.X < 0f)
            {
                _scroll = MathUtils.Max(_scroll + v.X, 0f);
            }

            if (v.X > ActualSize.X)
            {
                _scroll += v.X - ActualSize.X + 1f;
            }
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
}

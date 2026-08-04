using Engine.Graphics;
using Engine.Media;

namespace Game.Widgets;

public class LabelWidget : Widget
{
    public static BitmapFont BitmapFont
    {
        get => field is not null ? field : throw new InvalidOperationException("BitmapFont is not initialized");
        set;
    } = null!;

    private List<string> _lines = [];

    private float? _linesAvailableHeight;

    private float? _linesAvailableWidth;

    private Vector2 _linesSize;

    private int _languageRevision = -1;

    private string _resolvedText = string.Empty;

    private string _sourceText = string.Empty;

    public override bool IsHitTestVisible { get; set; } = false;

    public LabelWidget()
    {
        Font = ContentManager.Get<BitmapFont>("Fonts/Pericles");
        Text = string.Empty;
        FontScale = 1f;
        Color = Color.White;
        TextureLinearFilter = true;
    }

    public Vector2 Size { get; set; } = new(-1f);

    public string Text
    {
        get
        {
            RefreshText();
            return _resolvedText;
        }
        set
        {
            if (_sourceText == value)
            {
                RefreshText();
                return;
            }

            _sourceText = value;
            _languageRevision = -1;
            RefreshText();
        }
    }

    private void RefreshText()
    {
        var languageRevision = LanguageManager.Revision;
        if (_languageRevision == languageRevision)
        {
            return;
        }

        _languageRevision = languageRevision;
        var resolvedText = ResolveText(_sourceText);
        if (_resolvedText == resolvedText)
        {
            return;
        }

        _resolvedText = resolvedText;

        _linesSize = Vector2.Zero;
        _linesAvailableWidth = null;
        _linesAvailableHeight = null;
    }

    private static string ResolveText(string value)
    {
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            var parts = value.Substring(1, value.Length - 2).Split([':']);
            if (parts.Length == 3 && parts[0] == "Help")
            {
                return LanguageManager.GetHelpTopic(parts[1], parts[2]);
            }

            return parts.Length > 1
                ? LanguageManager.GetContentWidgets(parts[0], parts[1])
                : value;
        }

        return value;
    }

    public TextAnchor TextAnchor { get; set; }

    public TextOrientation TextOrientation
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _linesSize = Vector2.Zero;
            _linesAvailableWidth = null;
            _linesAvailableHeight = null;
        }
    }

    public BitmapFont Font
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _linesSize = Vector2.Zero;
            _linesAvailableWidth = null;
            _linesAvailableHeight = null;
        }
    }

    public float FontScale
    {
        get;
        set
        {
            if (value.CloseTo(field))
            {
                return;
            }

            field = value;
            _linesSize = Vector2.Zero;
            _linesAvailableWidth = null;
            _linesAvailableHeight = null;
        }
    }

    public Vector2 FontSpacing
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _linesSize = Vector2.Zero;
            _linesAvailableWidth = null;
            _linesAvailableHeight = null;
        }
    }

    public bool WordWrap
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _linesSize = Vector2.Zero;
            _linesAvailableWidth = null;
            _linesAvailableHeight = null;
        }
    }

    public bool Ellipsis
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _linesSize = Vector2.Zero;
            _linesAvailableWidth = null;
            _linesAvailableHeight = null;
        }
    }

    public int MaxLines
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _linesSize = Vector2.Zero;
            _linesAvailableWidth = null;
            _linesAvailableHeight = null;
        }
    } = int.MaxValue;

    public Color Color { get; set; }

    public bool DropShadow { get; set; }

    public bool TextureLinearFilter { get; set; }

    public override void Draw(DrawContext dc)
    {
        if (string.IsNullOrEmpty(Text) || Color.A == 0)
        {
            return;
        }

        var samplerState = TextureLinearFilter ? SamplerState.LinearClamp : SamplerState.PointClamp;
        var fontBatch2D =
            dc.PrimitivesRenderer2D.FontBatch(Font, 1, DepthStencilState.None, null, null, samplerState);
        var count = fontBatch2D.TriangleVertices.Count;
        var num = 0f;
        if ((TextAnchor & TextAnchor.VerticalCenter) != 0)
        {
            var num2 = Font.GlyphHeight * FontScale * Font.Scale + (_lines.Count - 1) *
                ((Font.GlyphHeight + Font.Spacing.Y) * FontScale * Font.Scale + FontSpacing.Y);
            num = (ActualSize.Y - num2) / 2f;
        }
        else if ((TextAnchor & TextAnchor.Bottom) != 0)
        {
            var num3 = Font.GlyphHeight * FontScale * Font.Scale + (_lines.Count - 1) *
                ((Font.GlyphHeight + Font.Spacing.Y) * FontScale * Font.Scale + FontSpacing.Y);
            num = ActualSize.Y - num3;
        }

        var anchor = TextAnchor & ~(TextAnchor.VerticalCenter | TextAnchor.Bottom);
        var color = Color * GlobalColorTransform;
        var num4 = CalculateLineHeight();
        foreach (var line in _lines)
        {
            var x = 0f;
            if ((TextAnchor & TextAnchor.HorizontalCenter) != 0)
            {
                x = ActualSize.X / 2f;
            }
            else if ((TextAnchor & TextAnchor.Right) != 0)
            {
                x = ActualSize.X;
            }

            var flag = true;
            var vector = Vector2.Zero;
            var angle = 0f;
            if (TextOrientation == TextOrientation.Horizontal)
            {
                vector = new Vector2(x, num);
                angle = 0f;
                flag = true;
            }
            else if (TextOrientation == TextOrientation.VerticalLeft)
            {
                vector = new Vector2(x, ActualSize.Y + num);
                angle = MathUtils.DegToRad(-90f);
                flag = true;
            }

            if (flag)
            {
                if (DropShadow)
                {
                    fontBatch2D.QueueText(line, vector + Margin + 1f * new Vector2(FontScale), 0f,
                        new Color((byte)0, (byte)0, (byte)0, color.A), anchor, new Vector2(FontScale), FontSpacing,
                        angle);
                }

                fontBatch2D.QueueText(line, vector + Margin, 0f, color, anchor, new Vector2(FontScale), FontSpacing,
                    angle);
            }

            num += num4;
        }

        fontBatch2D.TransformTriangles(GlobalTransform, count);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = !string.IsNullOrEmpty(Text) && Color.A != 0;
        if (TextOrientation == TextOrientation.Horizontal)
        {
            UpdateLines(parentAvailableSize.X, parentAvailableSize.Y);
            DesiredSize = new Vector2(Size.X < 0f ? _linesSize.X : Size.X,
                Size.Y < 0f ? _linesSize.Y : Size.Y);
        }
        else if (TextOrientation == TextOrientation.VerticalLeft)
        {
            UpdateLines(parentAvailableSize.Y, parentAvailableSize.X);
            DesiredSize = new Vector2(Size.X < 0f ? _linesSize.Y : Size.X,
                Size.Y < 0f ? _linesSize.X : Size.Y);
        }
    }

    public float CalculateLineHeight()
    {
        return (Font.GlyphHeight + Font.Spacing.Y + FontSpacing.Y) * FontScale * Font.Scale;
    }

    public void UpdateLines(float availableWidth, float availableHeight)
    {
        if (_linesAvailableHeight.HasValue && _linesAvailableHeight.Value.CloseTo(availableHeight) &&
            _linesAvailableWidth.HasValue)
        {
            var num = MathUtils.Min(_linesSize.X, _linesAvailableWidth.Value) - 0.1f;
            var num2 = MathUtils.Max(_linesSize.X, _linesAvailableWidth.Value) + 0.1f;
            if (availableWidth >= num && availableWidth <= num2)
            {
                return;
            }
        }

        availableWidth += 0.1f;
        _lines.Clear();
        var array = Text.Split(['\n'], StringSplitOptions.None);
        const string text = "...";
        var x = Font.MeasureText(text, new Vector2(FontScale), FontSpacing).X;
        if (WordWrap)
        {
            var num3 = (int)MathUtils.Min(MathUtils.Floor(availableHeight / CalculateLineHeight()), MaxLines);
            foreach (var item in array)
            {
                var text2 = item.TrimEnd();
                if (text2.Length == 0)
                {
                    _lines.Add(string.Empty);
                    continue;
                }

                while (text2.Length > 0)
                {
                    bool flag;
                    int num4;
                    if (Ellipsis && _lines.Count + 1 >= num3)
                    {
                        num4 = Font.FitText(MathUtils.Max(availableWidth - x, 0f), text2, 0, text2.Length, FontScale,
                            FontSpacing.X);
                        flag = true;
                    }
                    else
                    {
                        num4 = Font.FitText(availableWidth, text2, 0, text2.Length, FontScale, FontSpacing.X);
                        num4 = MathUtils.Max(num4, 1);
                        flag = false;
                        if (num4 < text2.Length)
                        {
                            var num5 = num4;
                            var num6 = num5 - 2;
                            while (num6 >= 0 && !char.IsWhiteSpace(text2[num6]) &&
                                   !char.IsPunctuation(text2[num6]))
                            {
                                num6--;
                            }

                            if (num6 < 0)
                            {
                                num6 = num5 - 1;
                            }

                            num4 = num6 + 1;
                        }
                    }

                    string text3;
                    if (num4 == text2.Length)
                    {
                        text3 = text2;
                        text2 = string.Empty;
                    }
                    else
                    {
                        text3 = text2[..num4].TrimEnd();
                        if (flag)
                        {
                            text3 += text;
                        }

                        text2 = text2.Substring(num4, text2.Length - num4).TrimStart();
                    }

                    _lines.Add(text3);
                    if (!flag)
                    {
                        continue;
                    }

                    if (_lines.Count > MaxLines)
                    {
                        _lines = _lines.Take(MaxLines).ToList();
                    }
                }
            }
        }
        else if (Ellipsis)
        {
            foreach (var item in array)
            {
                var text4 = item.TrimEnd();
                var num7 = Font.FitText(MathUtils.Max(availableWidth - x, 0f), text4, 0, text4.Length, FontScale,
                    FontSpacing.X);
                if (num7 < text4.Length)
                {
                    _lines.Add(text4[..num7].TrimEnd() + text);
                }
                else
                {
                    _lines.Add(text4);
                }
            }
        }
        else
        {
            _lines.AddRange(array);
        }

        if (_lines.Count > MaxLines)
        {
            _lines = _lines.Take(MaxLines).ToList();
        }

        var zero = Vector2.Zero;
        for (var k = 0; k < _lines.Count; k++)
        {
            var vector = Font.MeasureText(_lines[k], new Vector2(FontScale), FontSpacing);
            zero.X = MathUtils.Max(zero.X, vector.X);
            if (k < _lines.Count - 1)
            {
                zero.Y += (Font.GlyphHeight + Font.Spacing.Y + FontSpacing.Y) * FontScale * Font.Scale;
            }
            else
            {
                zero.Y += Font.GlyphHeight * FontScale * Font.Scale;
            }
        }

        _linesSize = zero;
        _linesAvailableWidth = availableWidth;
        _linesAvailableHeight = availableHeight;
    }
}

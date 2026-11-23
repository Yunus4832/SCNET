using Engine.Graphics;

namespace Game.Widgets;

public class LabelWidget : Widget
{
    public static BitmapFont BitmapFont
    {
        get => field is not null ? field : throw new InvalidOperationException("BitmapFont is not initialized");
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
            IsFontInitialized = true;
        }
    } = null!;

    public static bool IsFontInitialized { get; private set; }

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
        Font = AssetManager.Get<BitmapFont>("Fonts/Pericles");
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
        var languageRevision = Localization.ClientLanguage.Revision;
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
                return Localization.ClientLanguage.Catalog.GetHelpTopic(parts[1], parts[2]);
            }

            return parts.Length > 1
                ? Localization.ClientLanguage.Catalog.GetContentWidgets(parts[0], parts[1])
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
        var lineY = 0f;
        if ((TextAnchor & TextAnchor.VerticalCenter) != 0)
        {
            var textHeight = Font.GlyphHeight * FontScale * Font.Scale +
                             (_lines.Count - 1) *
                             ((Font.GlyphHeight + Font.Spacing.Y) * FontScale * Font.Scale + FontSpacing.Y);
            lineY = (ActualSize.Y - textHeight) / 2f;
        }
        else if ((TextAnchor & TextAnchor.Bottom) != 0)
        {
            var textHeight = Font.GlyphHeight * FontScale * Font.Scale +
                             (_lines.Count - 1) *
                             ((Font.GlyphHeight + Font.Spacing.Y) * FontScale * Font.Scale + FontSpacing.Y);
            lineY = ActualSize.Y - textHeight;
        }

        var anchor = TextAnchor & ~(TextAnchor.VerticalCenter | TextAnchor.Bottom);
        var color = Color * GlobalColorTransform;
        var lineHeight = CalculateLineHeight();
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

            var drawLine = true;
            var vector = Vector2.Zero;
            var angle = 0f;
            if (TextOrientation == TextOrientation.Horizontal)
            {
                vector = new Vector2(x, lineY);
                angle = 0f;
                drawLine = true;
            }
            else if (TextOrientation == TextOrientation.VerticalLeft)
            {
                vector = new Vector2(x, ActualSize.Y + lineY);
                angle = MathUtils.DegToRad(-90f);
                drawLine = true;
            }

            if (drawLine)
            {
                if (DropShadow)
                {
                    fontBatch2D.QueueText(
                        line,
                        vector + Margin + 1f * new Vector2(FontScale),
                        0f,
                        new Color((byte)0, (byte)0, (byte)0, color.A),
                        anchor,
                        new Vector2(FontScale),
                        FontSpacing,
                        angle
                    );
                }

                fontBatch2D.QueueText(
                    line,
                    vector + Margin,
                    0f,
                    color,
                    anchor,
                    new Vector2(FontScale),
                    FontSpacing,
                    angle
                );
            }

            lineY += lineHeight;
        }

        fontBatch2D.TransformTriangles(GlobalTransform, count);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = !string.IsNullOrEmpty(Text) && Color.A != 0;
        if (TextOrientation == TextOrientation.Horizontal)
        {
            UpdateLines(parentAvailableSize.X, parentAvailableSize.Y);
            DesiredSize = new Vector2(
                Size.X < 0f ? _linesSize.X : Size.X,
                Size.Y < 0f ? _linesSize.Y : Size.Y
            );
        }
        else if (TextOrientation == TextOrientation.VerticalLeft)
        {
            UpdateLines(parentAvailableSize.Y, parentAvailableSize.X);
            DesiredSize = new Vector2(
                Size.X < 0f ? _linesSize.Y : Size.X,
                Size.Y < 0f ? _linesSize.X : Size.Y
            );
        }
    }

    public float CalculateLineHeight()
    {
        return (Font.GlyphHeight + Font.Spacing.Y + FontSpacing.Y) * FontScale * Font.Scale;
    }

    public void UpdateLines(float availableWidth, float availableHeight)
    {
        if (_linesAvailableHeight.HasValue &&
            _linesAvailableHeight.Value.CloseTo(availableHeight) &&
            _linesAvailableWidth.HasValue)
        {
            var minCachedWidth = MathUtils.Min(_linesSize.X, _linesAvailableWidth.Value) - 0.1f;
            var maxCachedWidth = MathUtils.Max(_linesSize.X, _linesAvailableWidth.Value) + 0.1f;
            if (availableWidth >= minCachedWidth && availableWidth <= maxCachedWidth)
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
            var visibleLineCount = (int)MathUtils.Min(
                MathUtils.Floor(availableHeight / CalculateLineHeight()),
                MaxLines
            );
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
                    bool appendEllipsis;
                    int fittingLength;
                    if (Ellipsis && _lines.Count + 1 >= visibleLineCount)
                    {
                        fittingLength = Font.FitText(
                            MathUtils.Max(availableWidth - x, 0f),
                            text2,
                            0,
                            text2.Length,
                            FontScale,
                            FontSpacing.X
                        );
                        appendEllipsis = true;
                    }
                    else
                    {
                        fittingLength = Font.FitText(availableWidth, text2, 0, text2.Length, FontScale, FontSpacing.X);
                        fittingLength = MathUtils.Max(fittingLength, 1);
                        appendEllipsis = false;
                        if (fittingLength < text2.Length)
                        {
                            var originalFit = fittingLength;
                            var breakIndex = originalFit - 2;
                            while (breakIndex >= 0 &&
                                   !char.IsWhiteSpace(text2[breakIndex]) &&
                                   !char.IsPunctuation(text2[breakIndex]))
                            {
                                breakIndex--;
                            }

                            if (breakIndex < 0)
                            {
                                breakIndex = originalFit - 1;
                            }

                            fittingLength = breakIndex + 1;
                        }
                    }

                    string text3;
                    if (fittingLength == text2.Length)
                    {
                        text3 = text2;
                        text2 = string.Empty;
                    }
                    else
                    {
                        text3 = text2[..fittingLength].TrimEnd();
                        if (appendEllipsis)
                        {
                            text3 += text;
                        }

                        text2 = text2.Substring(fittingLength, text2.Length - fittingLength).TrimStart();
                    }

                    _lines.Add(text3);
                    if (!appendEllipsis)
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
                var fittingLength = Font.FitText(
                    MathUtils.Max(availableWidth - x, 0f),
                    text4,
                    0,
                    text4.Length,
                    FontScale,
                    FontSpacing.X
                );
                if (fittingLength < text4.Length)
                {
                    _lines.Add(text4[..fittingLength].TrimEnd() + text);
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
